using Godot;

/// <summary>
/// Controls one WheelDisplay.tscn instance.
/// The wheel buttons are authored in the WheelDisplay scene; this script
/// supplies their logical wheel number and visibility/state.
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
	private FluidSimulator simulator;

	public override void _Ready()
	{
		buyButton = GetNodeOrNull<Button>("BuyButton");
		upgradeButton = GetNodeOrNull<Button>("UpgradeButton");

		simulator = GetTree().CurrentScene?.FindChild("FluidSimulation", true, false) as FluidSimulator;
		if (simulator == null)
			simulator = GetTree().Root.FindChild("FluidSimulation", true, false) as FluidSimulator;

		if (buyButton == null)
			GD.PushWarning($"WheelDisplay {WheelNumber}: BuyButton was not found.");
		if (upgradeButton == null)
			GD.PushWarning($"WheelDisplay {WheelNumber}: UpgradeButton was not found.");

		SetButtonText();
		Refresh();
	}

	public override void _Process(double delta)
	{
		Refresh();
	}

	/// <summary>
	/// Called by WheelUi after assigning this instance's logical number.
	/// </summary>
	public void SetWheelNumber(int wheelNumber)
	{
		WheelNumber = wheelNumber;
		SetButtonText();
		Refresh();
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

	private void SetButtonText()
	{
		if (WheelNumber < 1)
			return;

		if (buyButton != null)
			buyButton.Text = $"Buy Wheel {WheelNumber}";
		if (upgradeButton != null)
			upgradeButton.Text = $"Upgrade Wheel {WheelNumber}";
	}

	private void Refresh()
	{
		if (simulator == null || WheelNumber < 1)
			return;

		int wheelIndex = WheelNumber - 1;
		bool unlocked = simulator.IsWheelUnlocked(wheelIndex);
		bool hasAvailableUpgrades = unlocked && simulator.HasAvailableWheelUpgrades(wheelIndex);

		if (buyButton != null)
			buyButton.Visible = !unlocked;

		if (upgradeButton != null)
			upgradeButton.Visible = hasAvailableUpgrades;
	}
}
