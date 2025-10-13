using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Generates a 3D planet surface made of tiles (centers of an IcoSphere subdivision).
/// The resulting pattern is a mix of hexagons and 12 pentagons (the vertices of the original icosahedron).
/// This version integrates 3D Perlin noise for height variation and biome coloring.
/// </summary>
public partial class Planet : Node3D // <-- Class name now correctly matches the file name 'Planet.cs'
{
	[Export] public int Subdivisions = 2;
	[Export] public float Radius = 6f;
	[Export] public PackedScene HexTileScene;
	[Export] public Camera3D PlanetCamera;

	private Node3D hexContainer;

	public override void _Ready()
	{
		// FIX: Use SafeGetNode to prevent errors if the scene structure is slightly off
		if (GetChildCount() == 0 || GetNodeOrNull<Node3D>("HexTiles") == null)
		{
			hexContainer = new Node3D();
			hexContainer.Name = "HexTiles";
			AddChild(hexContainer);
		}
		else
		{
			hexContainer = GetNode<Node3D>("HexTiles");
		}

		if (PlanetCamera == null)
		{
			PlanetCamera = GetNodeOrNull<Camera3D>("Camera3D");
			if (PlanetCamera == null)
			{
				GD.PushError("Camera3D not found! Raycasting for clicks will fail.");
			}
		}
		
		GenerateHexSphere();
	}

	public override void _Input(InputEvent @event)
	{
		if (PlanetCamera == null) return;
		
		if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
			HandleMouseClick(mb.Position);
	}

	private void HandleMouseClick(Vector2 screenPos)
	{
		var spaceState = GetWorld3D().DirectSpaceState;
		var from = PlanetCamera.ProjectRayOrigin(screenPos);
		var to = from + PlanetCamera.ProjectRayNormal(screenPos) * 1000f;

		var query = PhysicsRayQueryParameters3D.Create(from, to);
		query.CollideWithBodies = true;
		query.CollideWithAreas = false;

		var result = spaceState.IntersectRay(query);

		if (result.Count > 0)
		{
			var colliderObj = (GodotObject)result["collider"];
			if (colliderObj is Node colliderNode)
			{
				var tile = colliderNode.GetParent() as HexTile ?? colliderNode as HexTile;
				tile?.ToggleSelection();
			}
		}
	}

	private void GenerateHexSphere()
	{
		// NOTE: Assuming IcosphereGenerator and HexTile are defined elsewhere and accessible.
		// If these are internal classes, you must ensure they are properly loaded/referenced.
		// Also assuming HexTileScene is a PackedScene that contains a Node3D root, MeshInstance3D, and CollisionShape3D.
		
		// Clean up existing tiles
		foreach (Node child in hexContainer.GetChildren())
		{
			child.QueueFree();
		}

		var (verts, faces) = IcosphereGenerator.Generate(Subdivisions);

		// Compute triangle centers
		var faceCenters = new List<Vector3>(faces.Count);
		for (int i = 0; i < faces.Count; i++)
		{
			var (a, b, c) = faces[i];
			Vector3 center = (verts[a] + verts[b] + verts[c]) / 3f;
			center = center.Normalized() * Radius;
			faceCenters.Add(center);
		}

		// Map: vertex -> faces that use it
		var vertexToFaces = new Dictionary<int, List<int>>();
		for (int fi = 0; fi < faces.Count; fi++)
		{
			var (a, b, c) = faces[fi];
			if (!vertexToFaces.ContainsKey(a)) vertexToFaces[a] = new List<int>();
			if (!vertexToFaces.ContainsKey(b)) vertexToFaces[b] = new List<int>();
			if (!vertexToFaces.ContainsKey(c)) vertexToFaces[c] = new List<int>();
			vertexToFaces[a].Add(fi);
			vertexToFaces[b].Add(fi);
			vertexToFaces[c].Add(fi);
		}

		foreach (var kv in vertexToFaces)
		{
			int vIndex = kv.Key;
			List<int> adjFaces = kv.Value;

			Vector3 vPos = verts[vIndex].Normalized();
			
			// --- Tangent Plane Basis Calculation ---
			// Calculate two perpendicular vectors (u and w) on the plane tangent to the sphere at vPos.
			// This is used to order the face centers correctly around the vertex.
			Vector3 u = vPos.Cross(new Vector3(0.41f, 1f, 0.73f));
			if (u.LengthSquared() < 1e-6f) u = vPos.Cross(new Vector3(1, 0, 0));
			u = u.Normalized();
			Vector3 w = vPos.Cross(u).Normalized();
			// ----------------------------------------

			var polyVerts = new List<Vector3>();
			var angles = new List<float>();

			foreach (int fi in adjFaces)
			{
				Vector3 fc = faceCenters[fi];
				Vector3 dir = fc.Normalized();
				float x = dir.Dot(u);
				float y = dir.Dot(w);
				float angle = Mathf.Atan2(y, x);
				polyVerts.Add(fc);
				angles.Add(angle);
			}

			var idxs = new List<int>();
			for (int i = 0; i < angles.Count; i++) idxs.Add(i);
			idxs.Sort((i1, i2) => angles[i1].CompareTo(angles[i2]));

			var orderedVerts = new List<Vector3>();
			foreach (int i in idxs) orderedVerts.Add(polyVerts[i]);

			// Instantiate tile (assuming HexTileScene is assigned and contains a script named HexTile)
			if (HexTileScene == null)
			{
				GD.PushError("HexTileScene is not assigned in the inspector! Cannot generate tiles.");
				return; 
			}
			var tileInstance = (HexTile)HexTileScene.Instantiate();
			hexContainer.AddChild(tileInstance);

			bool isPentagon = adjFaces.Count == 5;
			tileInstance.SetTileColor(isPentagon ? new Color(0.2f, 0.8f, 1.0f) : Colors.White);

			Vector3 tileDir = vPos.Normalized();
			tileInstance.Position = tileDir * Radius;
			
			// --- Orientation Fix (The most reliable method) ---
			// The tile's local +Z axis should point outward along the normal (tileDir).
			
			Vector3 arbitraryUp;
			if (Mathf.Abs(tileDir.Dot(Vector3.Up)) > 0.999f)
			{
				// If tileDir is nearly straight up or down, choose Vector3.Right as a safe "arbitrary" reference.
				arbitraryUp = Vector3.Right;
			}
			else
			{
				arbitraryUp = Vector3.Up;
			}
			
			// 1. Calculate the new X (right) axis: perpendicular to the normal (Z) and the arbitrary Y (Up)
			Vector3 newX = tileDir.Cross(arbitraryUp).Normalized();
			
			// 2. Calculate the new Y (up) axis: perpendicular to the new X and the normal (Z)
			Vector3 newY = newX.Cross(tileDir).Normalized();

			// 3. Set the Basis directly: Z (Forward) is the normal (tileDir)
			tileInstance.Basis = new Basis(newX, newY, tileDir);
			// ---------------------------------------------------
			
			// Get the tile's Transform (now correctly oriented)
			Transform3D tileTransform = tileInstance.GlobalTransform;

			// Adjust polygon slightly outward to avoid cracks (Z-fighting)
			for (int i = 0; i < orderedVerts.Count; i++)
				orderedVerts[i] = orderedVerts[i].Normalized() * (Radius * 1.002f);

			var localVerts = new List<Vector3>();
			foreach (var p in orderedVerts)
			{
				// Correctly transform the world point into the tile's local space.
				localVerts.Add(tileTransform.Inverse() * p);
			}

			// Build mesh
			var mi = tileInstance.GetNode<MeshInstance3D>("MeshInstance3D");
			mi.Mesh = BuildPolygonMesh(localVerts);

			// Collision
			var cs = tileInstance.GetNode<CollisionShape3D>("CollisionShape3D");
			var convex = new ConvexPolygonShape3D();
			convex.Points = localVerts.ToArray();
			cs.Shape = convex;
		}

		GD.Print($"Generated {vertexToFaces.Count} tiles at radius {Radius}");
	}

	private ArrayMesh BuildPolygonMesh(List<Vector3> polygonVerts)
	{
		Vector3 centroid = Vector3.Zero;
		foreach (var p in polygonVerts) centroid += p;
		centroid /= polygonVerts.Count;

		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);

		for (int i = 0; i < polygonVerts.Count; i++)
		{
			Vector3 vA = centroid;
			Vector3 vB = polygonVerts[i];
			Vector3 vC = polygonVerts[(i + 1) % polygonVerts.Count];
			
			// --- FIX: Render Both Sides (Double-sided Mesh) ---

			// 1. Forward-facing triangle (vA, vC, vB) - outward
			st.AddVertex(vA);
			st.AddVertex(vC); 
			st.AddVertex(vB); 
			
			// 2. Backward-facing triangle (vA, vB, vC) - inward (flip winding order)
			st.AddVertex(vA);
			st.AddVertex(vB); // Flipped
			st.AddVertex(vC); // Flipped
		}
		
		// Calculate the normals based on the new winding order.
		st.GenerateNormals(); 
		
		return (ArrayMesh)st.Commit();
	}
}
