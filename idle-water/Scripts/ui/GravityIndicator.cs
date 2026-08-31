using Godot;

/// <summary>
/// Displays the current gravity direction using the ArrowBlue tile.
/// The arrow points in the same direction as the gravity used by the simulation.
/// </summary>
public partial class GravityIndicator : Sprite2D
{
	private const float RightMargin = 60.0f;

	public override void _Ready()
	{
		UpdateVisual();
	}

	public override void _Process(double delta)
	{
		UpdateVisual();
	}

	private void UpdateVisual()
	{
		Vector2 gravity = TiltController.CurrentGravityAcceleration;

		if (gravity.LengthSquared() >= 0.0001f)
			Rotation = gravity.Angle();

		if (GetParent() is Control parent)
		{
			Position = new Vector2(
				parent.Size.X - RightMargin,
				parent.Size.Y * 0.5f
			);
		}
	}
}
