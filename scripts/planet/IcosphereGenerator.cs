using Godot;
using System;
using System.Collections.Generic;

public static class IcosphereGenerator
{
	// Returns a tuple: (vertices, faces)
	// vertices → List<Vector3>
	// faces → List<(int a, int b, int c)>
	public static (List<Vector3> vertices, List<(int a, int b, int c)> faces) Generate(int subdivisions)
	{
		float t = (1.0f + Mathf.Sqrt(5.0f)) / 2.0f;

		var vertices = new List<Vector3>
		{
			new Vector3(-1,  t,  0),
			new Vector3( 1,  t,  0),
			new Vector3(-1, -t,  0),
			new Vector3( 1, -t,  0),
			new Vector3( 0, -1,  t),
			new Vector3( 0,  1,  t),
			new Vector3( 0, -1, -t),
			new Vector3( 0,  1, -t),
			new Vector3( t,  0, -1),
			new Vector3( t,  0,  1),
			new Vector3(-t,  0, -1),
			new Vector3(-t,  0,  1)
		};

		var faces = new List<(int, int, int)>
		{
			(0,11,5), (0,5,1), (0,1,7), (0,7,10), (0,10,11),
			(1,5,9), (5,11,4), (11,10,2), (10,7,6), (7,1,8),
			(3,9,4), (3,4,2), (3,2,6), (3,6,8), (3,8,9),
			(4,9,5), (2,4,11), (6,2,10), (8,6,7), (9,8,1)
		};

		// Normalize initial vertices
		for (int i = 0; i < vertices.Count; i++)
			vertices[i] = vertices[i].Normalized();

		// Subdivide faces to refine geometry
		for (int s = 0; s < subdivisions; s++)
		{
			var midCache = new Dictionary<long, int>();
			var newFaces = new List<(int, int, int)>();

			int GetMidpoint(int i1, int i2)
			{
				long keyA = Math.Min(i1, i2);
				long keyB = Math.Max(i1, i2);
				long key = (keyA << 32) + keyB;

				if (midCache.ContainsKey(key))
					return midCache[key];

				Vector3 midpoint = (vertices[i1] + vertices[i2]) * 0.5f;
				midpoint = midpoint.Normalized();
				vertices.Add(midpoint);

				int idx = vertices.Count - 1;
				midCache[key] = idx;
				return idx;
			}

			foreach (var (a, b, c) in faces)
			{
				int ab = GetMidpoint(a, b);
				int bc = GetMidpoint(b, c);
				int ca = GetMidpoint(c, a);

				newFaces.Add((a, ab, ca));
				newFaces.Add((b, bc, ab));
				newFaces.Add((c, ca, bc));
				newFaces.Add((ab, bc, ca));
			}

			faces = newFaces;
		}

		// Re-normalize all vertices to unit sphere (safety)
		for (int i = 0; i < vertices.Count; i++)
			vertices[i] = vertices[i].Normalized();

		return (vertices, faces);
	}
}
