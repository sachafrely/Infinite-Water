using Godot;

/// <summary>
/// Controls the BuyButton already present in WheelDisplay.tscn.
/// </summary>
public partial class BuyButton : Button
{
	public override void _Ready()
	{
		FocusMode = FocusModeEnum.None;
		Pressed += OnPressed;
	}

	private void OnPressed()
	{
		WheelDisplay wheelDisplay = GetParent()?.GetParent() as WheelDisplay;
		if (wheelDisplay == null)
		{
			GD.PushWarning("BuyButton: Could not find owning WheelDisplay.");
			return;
		}

		wheelDisplay.RequestBuy();
	}
}
