using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Generates a 3D planet with Earth-like biomes using FastNoiseLite.
/// Each tile is a seamless hex/pentagon from an icosphere subdivision.
/// Biomes are assigned after noise evaluation based on clusters of similar values.
/// </summary>
public partial class Planet : Node3D
{
    [Export] public int Subdivisions = 6;
    [Export] public float Radius = 18f;
    [Export] public PackedScene HexTileScene;
    [Export] public Camera3D PlanetCamera;
    
    [ExportGroup("Biome Settings")]
    [Export] public BiomeSet BiomeSet { get; set; }
    
    private FastNoiseLite heightNoise;
    private FastNoiseLite moistureNoise;
    private FastNoiseLite temperatureNoise;

   // --- Noise Settings ---
    [ExportGroup("Base Noise")]
    [Export] public FastNoiseLite.NoiseTypeEnum NoiseType { get; set; } = FastNoiseLite.NoiseTypeEnum.ValueCubic;
    [Export(PropertyHint.Range, "0.001,1.0,0.001")] public float Frequency { get; set; } = 0.01f;
    [Export(PropertyHint.Range, "1,10,1")] public int FractalOctaves { get; set; } = 6;
    [Export(PropertyHint.Range, "0.0,4.0,0.01")] public float FractalLacunarity { get; set; } = 4.0f;
    [Export(PropertyHint.Range, "0.0,1.0,0.01")] public float FractalGain { get; set; } = 0.7f;
    [Export(PropertyHint.Range, "0.0,10.0,0.1")] public float FractalPingPongStrength { get; set; } = 2.0f;
    [Export(PropertyHint.Range, "0.0,10.0,0.1")] public float FractalWeightedStrength { get; set; } = 0.0f;
    [Export] public FastNoiseLite.FractalTypeEnum FractalType { get; set; } = FastNoiseLite.FractalTypeEnum.Fbm;
    [Export] public Vector3 Offset { get; set; } = Vector3.Zero;
    [Export] public string Seed { get; set; } = "Earth42";
 
    // --- Domain Warp ---
    [ExportGroup("Domain Warp")]
    [Export] public bool DomainWarpEnabled { get; set; } = false;
    [Export] public FastNoiseLite.DomainWarpTypeEnum DomainWarpType { get; set; } = FastNoiseLite.DomainWarpTypeEnum.SimplexReduced;
    [Export(PropertyHint.Range, "0.0,100.0,0.01")] public float DomainWarpAmplitude { get; set; } = 30f;
    [Export(PropertyHint.Range, "0.001,1.0,0.001")] public float DomainWarpFrequency { get; set; } = 0.05f;
    [Export(PropertyHint.Range, "1,10,1")] public int DomainWarpFractalOctaves { get; set; } = 5;
    [Export(PropertyHint.Range, "0.0,2.0,0.01")] public float DomainWarpFractalLacunarity { get; set; } = 6.0f;
    [Export(PropertyHint.Range, "0.0,1.0,0.01")] public float DomainWarpFractalGain { get; set; } = 0.5f;
    [Export] public FastNoiseLite.DomainWarpFractalTypeEnum DomainWarpFractalType { get; set; } = FastNoiseLite.DomainWarpFractalTypeEnum.Independent;

    // --- Cellular Noise ---
    [ExportGroup("Cellular Noise")]
    [Export] public FastNoiseLite.CellularDistanceFunctionEnum CellularDistanceFunction { get; set; } = FastNoiseLite.CellularDistanceFunctionEnum.Euclidean;
    [Export] public FastNoiseLite.CellularReturnTypeEnum CellularReturnType { get; set; } = FastNoiseLite.CellularReturnTypeEnum.CellValue;
    [Export(PropertyHint.Range, "0.0,1.0,0.01")] public float CellularJitter { get; set; } = 1.0f;
    [Export(PropertyHint.Range, "0.001,1.0,0.001")] public float CellularFrequency { get; set; } = 0.05f;

    private Node3D hexContainer;
    private List<HexTile> allTiles = new List<HexTile>();

    public override void _Ready()
    {
        // Create container
        if (GetNodeOrNull<Node3D>("HexTiles") == null)
        {
            hexContainer = new Node3D { Name = "HexTiles" };
            AddChild(hexContainer);
        }
        else
        {
            hexContainer = GetNode<Node3D>("HexTiles");
        }

        if (PlanetCamera == null)
            PlanetCamera = GetNodeOrNull<Camera3D>("Camera3D");

        SetupNoise();
        GenerateHexSphere();
        AssignBiomes();

        // --- CAMERA AUTO-POSITIONING ---
        if (PlanetCamera != null)
        {
            // Place camera at 3 × radius so the entire planet fits in view
            float distance = Radius * 3.0f;
            PlanetCamera.Position = new Vector3(0, 0, distance);

            // Aim the camera toward the planet’s center
            PlanetCamera.LookAt(GlobalPosition, Vector3.Up);

            // Optionally adjust FOV to be a bit wider for larger planets
            PlanetCamera.Fov = Mathf.Clamp(30f + (Radius * 0.2f), 40f, 75f);
        }
    }

    
    private int StringToDeterministicSeed(string s)
    {
        if (string.IsNullOrEmpty(s))
            return 0;

        unchecked
        {
            int hash = 23;
            foreach (char c in s)
                hash = hash * 31 + c;
            return hash;
        }
    }
    
    private FastNoiseLite CreateNoise(int seed, float frequency)
    {
        var n = new FastNoiseLite();
        n.SetSeed(seed);
        n.SetNoiseType(NoiseType);
        n.SetFrequency(frequency);
        n.SetFractalType(FractalType);
        n.SetFractalOctaves(FractalOctaves);
        n.SetFractalLacunarity(FractalLacunarity);
        n.SetFractalGain(FractalGain);
        return n;
    }

    private void SetupNoise()
    {
        int seed = StringToDeterministicSeed(Seed);

        heightNoise = CreateNoise(seed, Frequency);
        moistureNoise = CreateNoise(seed + 101, Frequency * 0.8f);
        temperatureNoise = CreateNoise(seed + 202, Frequency * 0.6f);
        
        heightNoise.SetFractalPingPongStrength(FractalPingPongStrength);
        heightNoise.SetFractalWeightedStrength(FractalWeightedStrength);

        // Domain warp
        if (DomainWarpEnabled)
        {
            heightNoise.SetDomainWarpType(DomainWarpType);
            heightNoise.SetDomainWarpAmplitude(DomainWarpAmplitude);
            heightNoise.SetDomainWarpFrequency(DomainWarpFrequency);
            heightNoise.SetDomainWarpFractalOctaves(DomainWarpFractalOctaves);
            heightNoise.SetDomainWarpFractalLacunarity(DomainWarpFractalLacunarity);
            heightNoise.SetDomainWarpFractalGain(DomainWarpFractalGain);
            heightNoise.SetDomainWarpFractalType(DomainWarpFractalType);
        }

        // Cellular
        heightNoise.SetCellularDistanceFunction(CellularDistanceFunction);
        heightNoise.SetCellularReturnType(CellularReturnType);
        heightNoise.SetCellularJitter(CellularJitter);
    }

    private void GenerateHexSphere()
    {
        foreach (Node child in hexContainer.GetChildren())
            child.QueueFree();

        allTiles.Clear();

        var (verts, faces) = IcosphereGenerator.Generate(Subdivisions);

        var faceCenters = new List<Vector3>(faces.Count);
        for (int i = 0; i < faces.Count; i++)
        {
            var (a, b, c) = faces[i];
            Vector3 center = (verts[a] + verts[b] + verts[c]) / 3f;
            center = center.Normalized() * Radius;
            faceCenters.Add(center);
        }

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
                angles.Add(Mathf.Atan2(dir.Dot(w), dir.Dot(u)));
                polyVerts.Add(fc);
            }

            var idxs = new List<int>();
            for (int i = 0; i < angles.Count; i++) idxs.Add(i);
            idxs.Sort((i1, i2) => angles[i1].CompareTo(angles[i2]));

            var orderedVerts = new List<Vector3>();
            foreach (int i in idxs) orderedVerts.Add(polyVerts[i]);

            if (HexTileScene == null)
            {
                GD.PushError("HexTileScene is not assigned!");
                return;
            }
            var tileInstance = (HexTile)HexTileScene.Instantiate();
            hexContainer.AddChild(tileInstance);
            allTiles.Add(tileInstance);

            tileInstance.Position = vPos * Radius;

            tileInstance.HeightNoiseData = heightNoise.GetNoise3D(vPos.X, vPos.Y, vPos.Z);
            float rawMoisture = moistureNoise.GetNoise3D(vPos.X, vPos.Y, vPos.Z);
            float heightNormalized = Mathf.Remap((float)tileInstance.HeightNoiseData, -1f, 1f, 0.0f, 1.0f);

            // More moisture near sea level, less at high altitudes
            float elevationMoisture = Mathf.Clamp(1.0f - Mathf.InverseLerp(0.4f, 0.9f, heightNormalized), 0.0f, 1.0f);

            // Combine with noise (70% noise, 30% altitude bias)
            tileInstance.MoistureData = Mathf.Clamp(rawMoisture * 0.7f + elevationMoisture * 0.3f, 0.0f, 1.0f);
            
            // --- LATITUDE EFFECT ---
            // Dot product with world up gives latitude (1 at equator, 0 at poles)
            float latitude = Mathf.Abs(vPos.Dot(Vector3.Up));

            // Temperature decreases toward poles, so invert:
            float latitudeTemp = 1.0f - latitude;

            // --- ELEVATION EFFECT ---
            // Higher terrain is colder
            float elevationTemp = Mathf.Clamp(1.0f - Mathf.InverseLerp(0.5f, 1.0f, (float)tileInstance.HeightNoiseData), 0f, 1.0f);

            // --- BASE NOISE VARIATION ---
            // Adds local randomness and small variation to prevent symmetry
            float noiseTemp = Mathf.Remap(temperatureNoise.GetNoise3D(vPos.X, vPos.Y, vPos.Z), -1f, 1f, 0.0f, 1.0f);

            // --- COMBINE ALL FACTORS ---
            // Weighted combination: 60% latitude, 30% elevation, 10% noise
            tileInstance.TemperatureData = Mathf.Clamp(latitudeTemp * 0.6f + elevationTemp * 0.3f + noiseTemp * 0.1f, 0f, 1.0f);

            // Orientation
            Vector3 arbitraryUp = Mathf.Abs(vPos.Dot(Vector3.Up)) > 0.999f ? Vector3.Right : Vector3.Up;
            Vector3 newX = vPos.Cross(arbitraryUp).Normalized();
            Vector3 newY = newX.Cross(vPos).Normalized();
            tileInstance.Basis = new Basis(newX, newY, vPos);

            Transform3D tileTransform = tileInstance.GlobalTransform;
            for (int i = 0; i < orderedVerts.Count; i++)
                orderedVerts[i] = orderedVerts[i].Normalized() * Radius;

            var localVerts = new List<Vector3>();
            foreach (var p in orderedVerts)
                localVerts.Add(tileTransform.Inverse() * p);

            var mi = tileInstance.GetNode<MeshInstance3D>("MeshInstance3D");
            mi.Mesh = BuildPolygonMesh(localVerts);

            var cs = tileInstance.GetNode<CollisionShape3D>("CollisionShape3D");
            var convex = new ConvexPolygonShape3D();
            convex.Points = localVerts.ToArray();
            cs.Shape = convex;
        }
    }
    private void AssignBiomes()
    {
        if (allTiles.Count == 0 || BiomeSet == null || BiomeSet.Biomes.Count == 0)
            return;

        var biomes = BiomeSet.Biomes.ToList();

        // Find global min/max for normalization
        float minH = (float)allTiles.Min(t => t.HeightNoiseData);
        float maxH = (float)allTiles.Max(t => t.HeightNoiseData);
        float minM = (float)allTiles.Min(t => t.MoistureData);
        float maxM = (float)allTiles.Max(t => t.MoistureData);
        float minT = (float)allTiles.Min(t => t.TemperatureData);
        float maxT = (float)allTiles.Max(t => t.TemperatureData);

        foreach (var tile in allTiles)
        {
            float h = Mathf.InverseLerp(minH, maxH, (float)tile.HeightNoiseData);
            float m = Mathf.InverseLerp(minM, maxM, (float)tile.MoistureData);
            float t = Mathf.InverseLerp(minT, maxT, (float)tile.TemperatureData);

            // Find biome that matches best
            Biome biome = biomes.FirstOrDefault(b => b.Matches(h, m, t, 0)) ?? biomes.Last();

            var mesh = tile.GetNode<MeshInstance3D>("MeshInstance3D");
            if (biome.MaterialOverride != null)
            {
                mesh.SetSurfaceOverrideMaterial(0, biome.MaterialOverride);
            }
            else
            {
                tile.SetTileColor(biome.BaseColor);
            }
        }
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

            st.AddVertex(vA);
            st.AddVertex(vB);
            st.AddVertex(vC);
        }

        st.GenerateNormals();
        return (ArrayMesh)st.Commit();
    }
}
