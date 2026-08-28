using Godot;

/// <summary>
/// Controls one WheelDisplay.tscn instance.
///
/// The visual hierarchy is authored in Godot. This script does not create
/// the wheel UI hierarchy at runtime.
/// </summary>
public partial class WheelDisplay : Control
{
    [Export]
    public int WheelNumber { get; set; }

    private Button buyButton;
    private Button upgradeButton;

    public override void _Ready()
    {
        buyButton = GetNodeOrNull<Button>("ButtonContainer/BuyButton");
        upgradeButton = GetNodeOrNull<Button>("ButtonContainer/UpgradeButton");

        if (buyButton == null)
            GD.PushWarning($"WheelDisplay {WheelNumber}: BuyButton was not found.");

        if (upgradeButton == null)
            GD.PushWarning($"WheelDisplay {WheelNumber}: UpgradeButton was not found.");

        UpdateButtonState();
    }

    /// <summary>
    /// Called by WheelUi after assigning this instance's logical number.
    /// </summary>
    public void SetWheelNumber(int wheelNumber)
    {
        WheelNumber = wheelNumber;
        UpdateButtonState();
    }

    public Button GetBuyButton() => buyButton;
    public Button GetUpgradeButton() => upgradeButton;

    private void UpdateButtonState()
    {
        // Button behavior is intentionally kept enabled during this first
        // migration step. Availability and window behavior are migrated next.
        if (buyButton != null)
            buyButton.Disabled = false;

        if (upgradeButton != null)
            upgradeButton.Disabled = false;
    }
}
