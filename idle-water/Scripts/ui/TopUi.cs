using Godot;

/// <summary>
/// Owns the persistent top UI and coordinates its display components.
/// Layout and alignment are authored in Godot; the display scripts only update content.
/// </summary>
public partial class TopUi : Control
{
	private Control rainDisplay;
	private Control energyDisplay;
	private Control moneyDisplay;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		ZIndex = 1000;

		rainDisplay = GetNodeOrNull<Control>("RainDisplayContainer/RainDisplay")
			?? FindChild("RainDisplay", true, false) as Control;
		energyDisplay = GetNodeOrNull<Control>("CurrenciesContainer/EnergyDisplay")
			?? FindChild("EnergyDisplay", true, false) as Control;
		moneyDisplay = GetNodeOrNull<Control>("CurrenciesContainer/MoneyDisplay")
			?? FindChild("MoneyDisplay", true, false) as Control;
	}
}
