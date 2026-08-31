using Godot;

/// <summary>
/// Upgrade button authored in WheelDisplay.tscn.
/// </summary>
public partial class UpgradeButton : Button
{
	public override void _Ready()
	{
		FocusMode = FocusModeEnum.None;
		Pressed += OnPressed;
	}

	private void OnPressed()
	{
		WheelDisplay wheelDisplay = GetParent() as WheelDisplay;
		if (wheelDisplay == null)
		{
			GD.PushWarning("UpgradeButton: Could not find owning WheelDisplay.");
			return;
		}

		wheelDisplay.RequestUpgrade();
	}
}
