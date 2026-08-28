using Godot;

/// <summary>
/// Controls one WheelDisplay.tscn instance.
///
/// The visual hierarchy is authored in Godot. This script does not create
/// the wheel UI hierarchy at runtime.
/// </summary>
public partial class WheelDisplay : Control
{
	[Signal]
	public delegate void BuyRequestedEventHandler(int wheelNumber);

	[Signal]
	public delegate void UpgradeRequestedEventHandler(int wheelNumber);

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

    public void RequestBuy()
    {
        EmitSignal(SignalName.BuyRequested, WheelNumber);
    }

    public void RequestUpgrade()
    {
        EmitSignal(SignalName.UpgradeRequested, WheelNumber);
    }

    private void UpdateButtonState()
    {
        if (buyButton != null)
            buyButton.Disabled = false;

        if (upgradeButton != null)
            upgradeButton.Disabled = false;
    }
}
