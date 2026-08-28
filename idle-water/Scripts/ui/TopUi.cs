using Godot;

/// <summary>
/// Owns the persistent top UI and coordinates its display components.
/// The visual/background implementation lives in RenderedButtonBackground.
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

        rainDisplay = GetNodeOrNull<Control>("RainDisplayContainer/RainDisplay");
        energyDisplay = GetNodeOrNull<Control>("CurrenciesContainer/EnergyDisplay");
        moneyDisplay = GetNodeOrNull<Control>("CurrenciesContainer/MoneyDisplay");

        if (rainDisplay == null)
            GD.PushWarning("TopUi: RainDisplay Node not found.");
        if (energyDisplay == null)
            GD.PushWarning("TopUi: EnergyDisplay Node not found.");
        if (moneyDisplay == null)
            GD.PushWarning("TopUi: MoneyDisplay Node not found.");
    }
}
