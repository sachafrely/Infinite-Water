using Godot;

/// <summary>
/// Provides the visible BottomUI Sell Energy button.
/// The scene's SellEnergyButton node is a 240x120 Control, so this script
/// creates the actual clickable Button as its child instead of trying to place
/// a separate fallback outside the BottomUI layout.
/// </summary>
public partial class SellEnergyButton : Control
{
	private Button button;

	public override void _Ready()
	{
		CreateButton();
	}

	private void CreateButton()
	{
		button = new Button();
		button.Name = "SellEnergyActionButton";
		button.Text = "Sell Energy";
		button.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		button.MouseFilter = MouseFilterEnum.Stop;
		button.ZIndex = 1001;
		button.CustomMinimumSize = new Vector2(150.0f, 44.0f);
		button.Pressed += OnPressed;
		AddChild(button);

		ApplyButtonStyle();

		GD.Print("SellEnergyButton: Created visible Sell Energy action button inside BottomUI.");
	}

	private void OnPressed()
	{
		if (EnergySystem.Instance == null)
			return;

		// Exactly one 10-energy chunk is sold per click.
		EnergySystem.Instance.TrySellEnergyChunk();
	}

	private void ApplyButtonStyle()
	{
		button.AddThemeFontSizeOverride("font_size", UiSettings.FontSizeMedium);

		button.AddThemeStyleboxOverride(
			"normal",
			CreateStyle(UiSettings.ButtonColor)
		);

		button.AddThemeStyleboxOverride(
			"pressed",
			CreateStyle(UiSettings.WindowColor)
		);

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
