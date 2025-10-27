using Godot;
using System.Collections.Generic;

[Tool]
[GlobalClass]
public partial class BiomeSet : Resource
{
    [Export]
    public Godot.Collections.Array<Biome> Biomes { get; set; } = new();
}