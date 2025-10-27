using Godot;

[GlobalClass]
public partial class Biome : Resource
{
    [Export] public string Name { get; set; } = "New Biome";

    [Export(PropertyHint.Range, "0.0,1.0,0.01")]
    public float MinThreshold { get; set; } = 0.0f;

    [Export(PropertyHint.Range, "0.0,1.0,0.01")]
    public float MaxThreshold { get; set; } = 1.0f;

    [Export] public Color Color { get; set; } = Colors.White;

    // 🆕 Add a material reference
    [Export] public Material BiomeMaterial { get; set; }
}