using Godot;
using System;

public partial class CameraController : Node3D
{
	[Export] public float MaxSeparation { get; set; } = 20.0f;
	[Export] public float SplitLineThickness { get; set; } = 3.0f;
	[Export] public Color SplitLineColor { get; set; } = Colors.Black;
	[Export] public bool AdaptiveSplitLineThickness { get; set; } = true;

	[Export] public float BaseFov { get; set; } = 70.0f;   // Default FOV
	[Export] public float MaxExtraFov { get; set; } = 20.0f; // How much to zoom out max

	private Node3D _player1;
	private Node3D _player2;
	private TextureRect _view;
	private SubViewport _viewport1;
	private SubViewport _viewport2;
	private Camera3D _camera1;
	private Camera3D _camera2;

	private Variant _viewportBaseHeight;

	public override void _Ready()
	{
		_player1 = GetNode<Node3D>("../Player1");
		_player2 = GetNode<Node3D>("../Player2");
		_view = GetNode<TextureRect>("View");
		_viewport1 = GetNode<SubViewport>("Viewport1");
		_viewport2 = GetNode<SubViewport>("Viewport2");
		_camera1 = _viewport1.GetNode<Camera3D>("Camera1");
		_camera2 = _viewport2.GetNode<Camera3D>("Camera2");

		_viewportBaseHeight = ProjectSettings.GetSetting("display/window/size/viewport_height");

		OnSizeChanged();
		UpdateSplitscreen();

		GetViewport().SizeChanged += OnSizeChanged;

		if (_view.Material is ShaderMaterial mat)
		{
			mat.SetShaderParameter("viewport1", _viewport1.GetTexture());
			mat.SetShaderParameter("viewport2", _viewport2.GetTexture());
		}
	}

	public override void _Process(double delta)
	{
		MoveCameras();
		UpdateSplitscreen();
	}

	private void MoveCameras()
	{
		Vector3 positionDifference = ComputePositionDifferenceInWorld();
		float distance = Mathf.Clamp(ComputeHorizontalLength(positionDifference), 0, MaxSeparation);
		positionDifference = positionDifference.Normalized() * distance;

		_camera1.Position = new Vector3(
			_player1.Position.X + positionDifference.X / 2.0f,
			_camera1.Position.Y,
			_player1.Position.Z + positionDifference.Z / 2.0f
		);

		_camera2.Position = new Vector3(
			_player2.Position.X - positionDifference.X / 2.0f,
			_camera2.Position.Y,
			_player2.Position.Z - positionDifference.Z / 2.0f
		);

		// ✅ Dynamic FOV adjustment (zoom out if players spread apart)
		float separation = ComputeHorizontalLength(ComputePositionDifferenceInWorld());
		float fovOffset = Mathf.Clamp(separation / MaxSeparation, 0f, 1f) * MaxExtraFov;

		_camera1.Fov = BaseFov + fovOffset;
		_camera2.Fov = BaseFov + fovOffset;
	}

	private void UpdateSplitscreen()
	{
		Vector2 screenSize = GetViewport().GetVisibleRect().Size;

		Vector2 player1Position = _camera1.UnprojectPosition(_player1.Position) / screenSize;
		Vector2 player2Position = _camera2.UnprojectPosition(_player2.Position) / screenSize;

		float thickness;
		if (AdaptiveSplitLineThickness)
		{
			Vector3 positionDifference = ComputePositionDifferenceInWorld();
			float distance = ComputeHorizontalLength(positionDifference);
			thickness = Mathf.Lerp(0, SplitLineThickness, (distance - MaxSeparation) / MaxSeparation);
			thickness = Mathf.Clamp(thickness, 0, SplitLineThickness);
		}
		else
		{
			thickness = SplitLineThickness;
		}

		if (_view.Material is ShaderMaterial mat)
		{
			mat.SetShaderParameter("split_active", GetSplitState());
			mat.SetShaderParameter("player1_position", player1Position);
			mat.SetShaderParameter("player2_position", player2Position);
			mat.SetShaderParameter("split_line_thickness", thickness);
			mat.SetShaderParameter("split_line_color", SplitLineColor);
		}
	}

	private bool GetSplitState()
	{
		Vector3 positionDifference = ComputePositionDifferenceInWorld();
		float separationDistance = ComputeHorizontalLength(positionDifference);
		return separationDistance > MaxSeparation;
	}

	private void OnSizeChanged()
	{
		Vector2 screenSize = GetViewport().GetVisibleRect().Size;

		_viewport1.Size = (Vector2I)screenSize;
		_viewport2.Size = (Vector2I)screenSize;

		if (_view.Material is ShaderMaterial mat)
		{
			mat.SetShaderParameter("viewport_size", screenSize);
		}
	}

	private Vector3 ComputePositionDifferenceInWorld()
	{
		return _player2.Position - _player1.Position;
	}

	private float ComputeHorizontalLength(Vector3 vec)
	{
		return new Vector2(vec.X, vec.Z).Length();
	}
}
