using Godot;

[GlobalClass]
public partial class Biome : Resource
{
    [Export] public string Name { get; set; } = "Grassland";
    [Export] public Color BaseColor { get; set; } = new Color(0.2f, 0.8f, 0.2f);
    [Export] public Material MaterialOverride { get; set; }

    // Climate
    [Export(PropertyHint.Range, "0,1,0.01")] public float MinMoisture { get; set; }
    [Export(PropertyHint.Range, "0,1,0.01")] public float MaxMoisture { get; set; }
    [Export(PropertyHint.Range, "-1,1,0.01")] public float MinTemperature { get; set; }
    [Export(PropertyHint.Range, "-1,1,0.01")] public float MaxTemperature { get; set; }

    // Geography
    [Export(PropertyHint.Range, "0,1,0.01")] public float MinHeight { get; set; }
    [Export(PropertyHint.Range, "0,1,0.01")] public float MaxHeight { get; set; }

    // Society
    [Export(PropertyHint.Range, "0,1,0.01")] public float MinPopulation { get; set; }
    [Export(PropertyHint.Range, "0,1,0.01")] public float MaxPopulation { get; set; }

    public bool Matches(float height, float moisture, float temperature, float population)
    {
        return height >= MinHeight && height <= MaxHeight &&
               moisture >= MinMoisture && moisture <= MaxMoisture &&
               temperature >= MinTemperature && temperature <= MaxTemperature &&
               population >= MinPopulation && population <= MaxPopulation;
    }
}