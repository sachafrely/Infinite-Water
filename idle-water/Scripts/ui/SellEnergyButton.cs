using Godot;

/// <summary>
/// Handles the Sell Energy button action.
/// The button's visual style is applied centrally by BottomUi.
/// </summary>
public partial class SellEnergyButton : Button
{
    private EnergySystem energySystem;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        energySystem = EnergySystem.Instance;
        Pressed += OnPressed;
        UpdateAvailability();
    }

    public override void _Process(double delta)
    {
        UpdateAvailability();
    }

    private void UpdateAvailability()
    {
        if (energySystem == null)
            energySystem = EnergySystem.Instance;

        Disabled = energySystem == null || energySystem.Energy < EnergySystem.EnergyPerDollar;
    }

    private void OnPressed()
    {
        if (energySystem == null)
            energySystem = EnergySystem.Instance;

        if (energySystem == null)
            return;

        energySystem.SellAllAvailableEnergy();
        UpdateAvailability();
    }
}
