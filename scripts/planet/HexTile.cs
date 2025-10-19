using Godot;
using System;

public partial class HexTile : Node3D
{
	private MeshInstance3D meshInstance;
	private CollisionShape3D collision;
	private bool isSelected = false;
	private StandardMaterial3D material;
	public float NoiseData { get; set; }

	public override void _Ready()
	{
		meshInstance = GetNode<MeshInstance3D>("MeshInstance3D");
		collision = GetNode<CollisionShape3D>("CollisionShape3D");

		// Each tile gets its own material instance
		material = new StandardMaterial3D();
		material.AlbedoColor = Colors.White;
		meshInstance.SetSurfaceOverrideMaterial(0, material);

		// Optional: add a small offset for better visibility when clicked
		//meshInstance.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
	}

	public void ToggleSelection()
	{
		isSelected = !isSelected;
		material.AlbedoColor = isSelected ? new Color(1.0f, 0.6f, 0.2f) : Colors.White;
	}

	public void SetTileColor(Color color)
	{
		if (material != null)
			material.AlbedoColor = color;
	}
}
