using Godot;

/// <summary>
/// Visual configuration for the existing BottomUi Sell Energy button.
///
/// This script intentionally does not open, close, toggle, or otherwise
/// control any window. EconomyUi owns the selling action.
/// </summary>
public partial class SellEnergyButton : Node
{
	private Button button;

	public override void _Ready()
	{
		button = FindButton();
		if (button == null)
			return;

		ApplyButtonStyle();
	}

	private Button FindButton()
	{
		if (this is Button selfButton)
			return selfButton;

		Node current = GetParent();
		while (current != null)
		{
			if (current is Button parentButton)
				return parentButton;

			current = current.GetParent();
		}

		return null;
	}

	private void ApplyButtonStyle()
	{
		// Dark gray when released/unselected.
		button.AddThemeStyleboxOverride(
			"normal",
			CreateStyle(UiSettings.ButtonColor)
		);

		// Bright gray only while the button is being pressed.
		button.AddThemeStyleboxOverride(
			"pressed",
			CreateStyle(UiSettings.WindowColor)
		);

		// Keep the button dark when it is not actively being pressed.
		button.AddThemeStyleboxOverride(
			"hover",
			CreateStyle(UiSettings.ButtonColor)
		);

		button.AddThemeStyleboxOverride(
			"focus",
			CreateStyle(UiSettings.ButtonColor)
		);

		button.AddThemeStyleboxOverride(
			"disabled",
			CreateStyle(UiSettings.ButtonColor)
		);
	}

	private StyleBoxFlat CreateStyle(Color backgroundColor)
	{
		StyleBoxFlat style = new StyleBoxFlat();
		style.BgColor = backgroundColor;
		style.BorderColor = UiSettings.BorderColor;
		style.SetBorderWidthAll((int)UiSettings.BorderSize);
		return style;
	}
}
