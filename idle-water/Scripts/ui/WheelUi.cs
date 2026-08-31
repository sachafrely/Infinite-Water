// THIS IS THE NEW WHEEL UI SCRIPT

using Godot;

/// <summary>
/// Owns the six WheelDisplay instances already present under WheelUi.
/// WheelUi is the single source of truth for logical wheel numbering and
/// routes button requests to the authored BuyWindow and UpgradeWindow.
/// </summary>
public partial class WheelUi : Control
{
	private const int WheelCount = 6;

	private readonly WheelDisplay[] wheelDisplays = new WheelDisplay[WheelCount];
	private BuyWindow buyWindow;
	private UpgradeWindow upgradeWindow;
	private FluidSimulator simulator;

	public override void _Ready()
	{
		simulator = GetTree().CurrentScene?.FindChild("FluidSimulation", true, false) as FluidSimulator;
		if (simulator == null)
			simulator = GetTree().Root.FindChild("FluidSimulation", true, false) as FluidSimulator;

		buyWindow = GetNodeOrNull<BuyWindow>("../BuyWindow");
		upgradeWindow = GetNodeOrNull<UpgradeWindow>("../UpgradeWindow");

		for (int index = 0; index < WheelCount; index++)
		{
			int wheelNumber = index + 1;
			WheelDisplay display = GetNodeOrNull<WheelDisplay>($"WheelDisplay{wheelNumber}");
			if (display == null)
				display = GetNodeOrNull<WheelDisplay>($"Wheel{wheelNumber}");

			if (display == null)
			{
				GD.PushWarning($"WheelUi: WheelDisplay{wheelNumber} is missing or is not a WheelDisplay instance.");
				continue;
			}

			wheelDisplays[index] = display;
			display.SetWheelNumber(wheelNumber);
			display.BuyRequested += OnBuyRequested;
			display.UpgradeRequested += OnUpgradeRequested;
		}
	}

	public WheelDisplay GetWheelDisplay(int wheelNumber)
	{
		if (wheelNumber < 1 || wheelNumber > WheelCount)
			return null;

		return wheelDisplays[wheelNumber - 1];
	}

	public int GetWheelNumber(WheelDisplay display)
	{
		if (display == null)
			return 0;

		for (int index = 0; index < wheelDisplays.Length; index++)
		{
			if (wheelDisplays[index] == display)
				return index + 1;
		}

		return 0;
	}

	private void OnBuyRequested(int wheelNumber)
	{
		if (simulator == null || buyWindow == null)
			return;

		if (wheelNumber < 1 || wheelNumber > WheelCount)
			return;

		int wheelIndex = wheelNumber - 1;
		if (simulator.IsWheelUnlocked(wheelIndex))
			return;

		upgradeWindow?.Close();
		buyWindow.Setup(wheelNumber, simulator);
		buyWindow.Open();
	}

	private void OnUpgradeRequested(int wheelNumber)
	{
		if (simulator == null || upgradeWindow == null)
			return;

		if (wheelNumber < 1 || wheelNumber > WheelCount)
			return;

		int wheelIndex = wheelNumber - 1;
		if (!simulator.IsWheelUnlocked(wheelIndex) || !simulator.HasAvailableWheelUpgrades(wheelIndex))
			return;

		buyWindow?.Close();
		upgradeWindow.Setup(wheelNumber, simulator);
		upgradeWindow.Open();
	}
}
