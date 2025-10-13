using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Generates a seamless 3D planet surface made of hex/pent tiles (icosphere subdivision).
/// Uses FastNoiseLite for biome coloring (not elevation).
/// Tiles share a perfectly smooth surface — no visible seams or cracks.
/// Meshes are outward-facing and the planet interior is culled.
/// </summary>
public partial class Planet : Node3D
{
	[Export] public int Subdivisions = 2;
	[Export] public float Radius = 6f;
	[Export] public PackedScene HexTileScene;
	[Export] public Camera3D PlanetCamera;

	private Node3D hexContainer;
	private FastNoiseLite noise;

	public override void _Ready()
	{
		// Create hex tile container if missing
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

		// Assign camera if missing
		if (PlanetCamera == null)
		{
			PlanetCamera = GetNodeOrNull<Camera3D>("Camera3D");
			if (PlanetCamera == null)
				GD.PushError("Camera3D not found! Raycasting for clicks will fail.");
		}

		SetupNoise();
		GenerateHexSphere();
	}

	private void SetupNoise()
	{
		noise = new FastNoiseLite();
		noise.SetSeed((int)GD.RandRange(0, 10000));
		noise.SetNoiseType(FastNoiseLite.NoiseTypeEnum.Perlin);
		noise.SetFractalType(FastNoiseLite.FractalTypeEnum.Fbm);
		noise.SetFractalOctaves(4);
		noise.SetFractalLacunarity(2f);
		noise.SetFractalGain(0.5f);
		noise.SetFrequency(0.05f);
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
		// Clear previous tiles
		foreach (Node child in hexContainer.GetChildren())
			child.QueueFree();

		var (verts, faces) = IcosphereGenerator.Generate(Subdivisions);

		// Compute face centers
		var faceCenters = new List<Vector3>(faces.Count);
		for (int i = 0; i < faces.Count; i++)
		{
			var (a, b, c) = faces[i];
			Vector3 center = (verts[a] + verts[b] + verts[c]) / 3f;
			center = center.Normalized() * Radius;
			faceCenters.Add(center);
		}

		// Map vertex -> adjacent faces
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

			// Tangent plane basis for vertex sorting
			Vector3 u = vPos.Cross(new Vector3(0.41f, 1f, 0.73f));
			if (u.LengthSquared() < 1e-6f) u = vPos.Cross(Vector3.Right);
			u = u.Normalized();
			Vector3 w = vPos.Cross(u).Normalized();

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

			// Sort vertices counterclockwise
			var idxs = new List<int>();
			for (int i = 0; i < angles.Count; i++) idxs.Add(i);
			idxs.Sort((i1, i2) => angles[i1].CompareTo(angles[i2]));

			var orderedVerts = new List<Vector3>();
			foreach (int i in idxs) orderedVerts.Add(polyVerts[i]);

			// Instantiate tile
			if (HexTileScene == null)
			{
				GD.PushError("HexTileScene is not assigned!");
				return;
			}
			var tileInstance = (HexTile)HexTileScene.Instantiate();
			hexContainer.AddChild(tileInstance);

			// --- Seamless Surface ---
			float tileRadius = Radius; // fixed radius, no per-tile deformation
			tileInstance.Position = vPos * tileRadius;

			// Biome coloring from noise (visual only)
			float n = noise.GetNoise3D(vPos.X * 2f, vPos.Y * 2f, vPos.Z * 2f);
			Color biomeColor = GetBiomeColor(n, vPos);

			// Material with standard backface culling
			var mi = tileInstance.GetNode<MeshInstance3D>("MeshInstance3D");
			var mat = new StandardMaterial3D();
			mat.AlbedoColor = biomeColor;
			mat.CullMode = BaseMaterial3D.CullModeEnum.Back;
			mi.MaterialOverride = mat;

			// Orientation
			Vector3 arbitraryUp = Mathf.Abs(vPos.Dot(Vector3.Up)) > 0.999f ? Vector3.Right : Vector3.Up;
			Vector3 newX = vPos.Cross(arbitraryUp).Normalized();
			Vector3 newY = newX.Cross(vPos).Normalized();
			tileInstance.Basis = new Basis(newX, newY, vPos);

			Transform3D tileTransform = tileInstance.GlobalTransform;

			// Use exact radius (no offset)
			for (int i = 0; i < orderedVerts.Count; i++)
				orderedVerts[i] = orderedVerts[i].Normalized() * tileRadius;

			var localVerts = new List<Vector3>();
			foreach (var p in orderedVerts)
				localVerts.Add(tileTransform.Inverse() * p);

			// Build outward-facing polygon mesh
			mi.Mesh = BuildPolygonMesh(localVerts);

			// Collision
			var cs = tileInstance.GetNode<CollisionShape3D>("CollisionShape3D");
			var convex = new ConvexPolygonShape3D();
			convex.Points = localVerts.ToArray();
			cs.Shape = convex;
		}

		GD.Print($"Generated {vertexToFaces.Count} seamless tiles at radius {Radius}");
	}

	private Color GetBiomeColor(float noiseVal, Vector3 vPos)
	{
		// Normalize noise to [0,1]
		float n = (noiseVal + 1f) * 0.5f;

		Color biomeColor;
		if (n < 0.35f) biomeColor = new Color(0f, 0f, 0.5f);          // Deep ocean
		else if (n < 0.45f) biomeColor = new Color(0.3f, 0.6f, 1f);   // Shallow ocean
		else if (n < 0.5f) biomeColor = new Color(0.94f, 0.87f, 0.62f); // Beach
		else if (n < 0.65f) biomeColor = new Color(0.1f, 0.8f, 0.1f);   // Grassland
		else if (n < 0.8f) biomeColor = new Color(0f, 0.5f, 0f);        // Forest
		else if (n < 0.9f) biomeColor = new Color(0.5f, 0.5f, 0.5f);    // Mountain
		else biomeColor = new Color(1f, 1f, 1f);                        // Snow cap

		// Polar blending
		float latitude = Mathf.Acos(vPos.Y);
		if (latitude < 0.2f || latitude > Mathf.Pi - 0.2f)
			biomeColor = biomeColor.Lerp(Colors.White, 0.7f);

		return biomeColor;
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
			Vector3 vB = polygonVerts[i]; // flipped winding order
			Vector3 vC = polygonVerts[(i + 1) % polygonVerts.Count];

			// Outward-facing (counter-clockwise relative to planet)
			st.AddVertex(vA);
			st.AddVertex(vB);
			st.AddVertex(vC);
		}

		st.GenerateNormals();
		return (ArrayMesh)st.Commit();
	}
}
