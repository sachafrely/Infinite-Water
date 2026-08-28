using Godot;

/// <summary>
/// Controls the authored TopUi scene.
/// The display components are authored in the scene and own their own behavior.
/// </summary>
public partial class TopUi : Control
{
    private Control rainDisplay;
    private Control energyDisplay;
    private Control moneyDisplay;

    public override void _Ready()
    {
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
