using Godot;

/// <summary>
/// Buy button authored in WheelDisplay.tscn.
/// </summary>
public partial class BuyButton : Button
{
	public override void _Ready()
	{
		FocusMode = FocusModeEnum.None;
		RenderedButtonBackground.Apply(this);
		AddThemeFontSizeOverride("font_size", UiSettings.FontSizeMedium);
		Pressed += OnPressed;
	}

	private void OnPressed()
	{
		WheelDisplay wheelDisplay = GetParent() as WheelDisplay;
		if (wheelDisplay == null)
		{
			GD.PushWarning("BuyButton: Could not find owning WheelDisplay.");
			return;
		}

		wheelDisplay.RequestBuy();
	}
}
