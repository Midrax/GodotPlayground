using Godot;

public partial class PlayerController : CharacterBody3D
{
	[Export(PropertyHint.Range, "1,2")]
	public int PlayerId { get; set; } = 1;

	[Export]
	public float WalkSpeed { get; set; } = 2.0f;

	public override void _PhysicsProcess(double delta)
	{
		Vector2 moveDirection = Input.GetVector(
			"move_left_player" + PlayerId,
			"move_right_player" + PlayerId,
			"move_up_player" + PlayerId,
			"move_down_player" + PlayerId
		);

		Velocity = new Vector3(
			Velocity.X + moveDirection.X * WalkSpeed,
			Velocity.Y,
			Velocity.Z + moveDirection.Y * WalkSpeed
		);

		// Apply friction
		Velocity *= 0.9f;

		MoveAndSlide();
	}
}
