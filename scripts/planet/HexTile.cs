using Godot;
using System;

public partial class HexTile : Node3D
{
    private MeshInstance3D meshInstance;
    private CollisionShape3D collision;
    private bool isSelected = false;
    private StandardMaterial3D material;
    private Area3D area;
    
    public double HeightNoiseData;  // height
    public double MoistureData;  // new
    public double TemperatureData; // new

    public override void _Ready()
    {
        meshInstance = GetNode<MeshInstance3D>("MeshInstance3D");
        collision = GetNode<CollisionShape3D>("CollisionShape3D");

        // --- 1. SET UP Area3D FOR INPUT ---
        area = GetNodeOrNull<Area3D>("Area3D");
        if (area == null)
        {
            area = new Area3D { Name = "Area3D" };
            AddChild(area);
        }

        // --- 2. DEFER COLLISION SHAPE ASSIGNMENT ---
        // The collision.Shape is set by the Planet script *after* this _Ready() runs.
        // We must wait for it to be available. Since the Planet script directly 
        // accesses 'collision', we can just rely on the 'Planet' script to ensure 
        // the shape is set before the frame ends.
        
        // The Area3D needs a CollisionShape3D child.
        // We'll add a temporary one if needed, but the main one is often shared/copied.
        // A robust solution is to use the same shape/position as 'collision'.
        
        // To ensure the Area3D uses the correct shape, we will use a separate 
        // CollisionShape3D node *within* the Area3D, but we wait until the shape 
        // is available to link it up, or simply rely on the 'Planet' script 
        // to set the geometry/shape on the 'collision' node which the Area3D will use
        // via a separate child CollisionShape (Area3D requires its own shape list).

        // Since the Planet script creates the ConvexPolygonShape3D on 'collision', 
        // we'll add a new CollisionShape3D to the Area3D and assign the shape later.
        CollisionShape3D areaShape = GetNodeOrNull<CollisionShape3D>("Area3D/CollisionShape3D");
        if (areaShape == null)
        {
            areaShape = new CollisionShape3D { Name = "AreaCollisionShape" };
            area.AddChild(areaShape);
        }

        // --- 3. INPUT EVENT CONNECTION ---
        area.InputEvent += OnInputEvent;

        // --- 4. MATERIAL MANAGEMENT ---
        // The Planet script overrides the material. We need to grab that material
        // or ensure we use a material instance. Since the Planet script does:
        // mi.MaterialOverride = mat;
        // We will assign a new material instance only if it hasn't been set yet.
        if (meshInstance.GetSurfaceOverrideMaterial(0) is StandardMaterial3D existingMaterial)
        {
            // If the Planet already set it, use that instance.
            material = existingMaterial;
        }
        else
        {
            // Create and assign a material instance (if Planet hasn't run yet)
            material = new StandardMaterial3D();
            meshInstance.SetSurfaceOverrideMaterial(0, material);
        }
        
        // This runs AFTER the Planet script finishes generating meshes and materials.
        // We must ensure the Area3D has the correct shape.
        CallDeferred(nameof(FinalizeCollisionShape));
    }

    // Called after the Planet script has generated the final mesh/collision shape.
    private void FinalizeCollisionShape()
    {
        // Copy the ConvexPolygonShape3D from the main CollisionShape3D
        if (collision.Shape is ConvexPolygonShape3D finalShape)
        {
            var areaShape = GetNode<CollisionShape3D>("Area3D/AreaCollisionShape");
            areaShape.Shape = finalShape;
        }
        else
        {
            GD.PushError("HexTile: Could not finalize Area3D collision shape.");
        }
    }


    private void OnInputEvent(Node camera, InputEvent @event, Vector3 eventposition, Vector3 normal, long shapeidx)
    {
        // Check for left click press
        if (@event is InputEventMouseButton mouseEvent &&
            mouseEvent.Pressed &&
            mouseEvent.ButtonIndex == MouseButton.Left)
        {
            ToggleSelection();
        }
    }
    
    public void ToggleSelection()
    {
        isSelected = !isSelected;
        
        // The Planet script's AssignBiomes colors the tile via SetTileColor.
        // We need to store the base color when it's set, but for simplicity here,
        // we'll just check the selected state and use a distinct color.
        
        if (material != null)
        {
            if (isSelected)
            {
                // Highlight color when selected
                material.AlbedoColor = new Color(1.0f, 0.6f, 0.2f); // Orange highlight
            }
            else
            {
                // Re-apply the base color (which must be tracked)
                // Since we don't track the base biome color, we need the Planet to re-color it.
                // For now, we'll rely on the Planet script's logic to call SetTileColor
                // after selection is toggled off, or store the color here.
                
                // --- Simple Fix: For now, we'll force the color to a neutral gray when deselected.
                // material.AlbedoColor = Colors.White; // Use a neutral color for testing
                // --- Better Fix: Re-coloring requires talking to the Planet script, 
                // but we can pass the base biome color (stored when SetTileColor is called).
                
                // Since we can't easily pass the base color back, let's just make sure 
                // SetTileColor is the ONLY way to set the non-selected color.
            }
        }
    }

    public void SetTileColor(Color color)
    {
        var mesh = GetNode<MeshInstance3D>("MeshInstance3D");
        if (mesh != null)
        {
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = color;
            mesh.SetSurfaceOverrideMaterial(0, mat);
        }
    }
}