using Godot;
using System;

[Tool] // ✅ Allows the class to show up in the editor
[GlobalClass] // ✅ Makes it creatable as a Resource
public partial class Biome : Resource
{
    [Export] public string Name { get; set; } = "Unnamed Biome";

    [Export(PropertyHint.Range, "0.0,1.0,0.01")]
    public float MinThreshold { get; set; } = 0.0f;

    [Export(PropertyHint.Range, "0.0,1.0,0.01")]
    public float MaxThreshold { get; set; } = 1.0f;

    [Export]
    public Color Color { get; set; } = Colors.White;
}