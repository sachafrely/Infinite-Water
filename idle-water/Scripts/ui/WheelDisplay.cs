using Godot;

/// <summary>
/// Controls one WheelDisplay.tscn instance.
/// The wheel buttons are authored in the WheelDisplay scene; this script
/// supplies their logical wheel number, position, and visibility/state.
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

		bool canBuyWheel = EnergySystem.Instance != null && EnergySystem.Instance.Dollars >= 10.0;
		bool canBuyAnyUpgrade = false;

		if (unlocked && hasAvailableUpgrades)
		{
			WheelUpgradeType[] upgradeTypes =
			{
				WheelUpgradeType.BiggerPaddles,
				WheelUpgradeType.LessFriction,
				WheelUpgradeType.MoreEfficient
			};

			foreach (WheelUpgradeType type in upgradeTypes)
			{
				if (simulator.CanPurchaseWheelUpgrade(wheelIndex, type))
				{
					canBuyAnyUpgrade = true;
					break;
				}
			}
		}

		if (buyButton != null)
		{
			buyButton.Visible = !unlocked;
			ApplyAffordabilityColor(buyButton, canBuyWheel);
		}

		if (upgradeButton != null)
		{
			upgradeButton.Visible = hasAvailableUpgrades;
			ApplyAffordabilityColor(upgradeButton, canBuyAnyUpgrade);
		}

		Position = ClampToParent(simulator.GetWheelUiPosition(wheelIndex));
	}

	private void ApplyAffordabilityColor(Button button, bool available)
	{
		Color color = available ? UiSettings.FontColorEnabled : UiSettings.FontColorDisabled;
		button.Disabled = false;
		button.AddThemeColorOverride("font_color", color);
		button.AddThemeColorOverride("font_hover_color", color);
		button.AddThemeColorOverride("font_pressed_color", color);
		button.AddThemeColorOverride("font_focus_color", color);
		button.AddThemeColorOverride("font_disabled_color", UiSettings.FontColorDisabled);
	}

	private Vector2 ClampToParent(Vector2 desiredPosition)
	{
		Control parent = GetParent() as Control;
		if (parent == null)
			return desiredPosition;

		Vector2 viewportSize = parent.Size;
		if (viewportSize.X <= 0.0f || viewportSize.Y <= 0.0f)
			viewportSize = GetViewportRect().Size;

		float halfWidth = Mathf.Max(0.0f, Size.X * 0.5f);
		float halfHeight = Mathf.Max(0.0f, Size.Y * 0.5f);
		if (halfWidth <= 0.0f)
			halfWidth = 60.0f;
		if (halfHeight <= 0.0f)
			halfHeight = 18.0f;

		return new Vector2(
			Mathf.Clamp(desiredPosition.X, halfWidth, Mathf.Max(halfWidth, viewportSize.X - halfWidth)),
			Mathf.Clamp(desiredPosition.Y, halfHeight, Mathf.Max(halfHeight, viewportSize.Y - halfHeight)));
	}
}
