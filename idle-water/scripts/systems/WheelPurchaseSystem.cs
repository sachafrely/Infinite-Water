using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Coordinates per-wheel ownership, economy transactions, and world purchase UI.
/// Wheel ownership is stored per stable wheel ID rather than as an unlock count.
/// </summary>
internal sealed class WheelPurchaseSystem
{
	public const int StartingWheelId = 4;

	private readonly WaterWheelManager wheelManager;
	private readonly EnergySystem energySystem;
	private readonly Node2D owner;
	private readonly bool[] wheelPurchased =
		new bool[WaterWheelManager.MaxWheelCount];
	private readonly Dictionary<int, WheelPurchaseWorldUi> purchaseUiByWheelId =
		new Dictionary<int, WheelPurchaseWorldUi>();

	private WheelPurchaseConfirmationWindow confirmationWindow;

	public WheelPurchaseSystem(
		WaterWheelManager wheelManager,
		EnergySystem energySystem,
		Node2D owner)
	{
		this.wheelManager = wheelManager;
		this.energySystem = energySystem;
		this.owner = owner;

		// New games start with exactly Wheel 4 active.
		wheelPurchased[StartingWheelId - 1] = true;
	}

	public bool IsWheelPurchased(int wheelId)
	{
		if (!IsValidWheelId(wheelId))
			return false;

		return wheelPurchased[wheelId - 1];
	}

	public void Initialize()
	{
		if (wheelManager.WheelLocationCount <= 0)
			return;

		// Explicitly activate the starting wheel first so it remains the solver's
		// primary wheel. Runtime list order is never used as persistent wheel ID.
		if (IsWheelPurchased(StartingWheelId))
			wheelManager.TryActivateWheel(StartingWheelId);

		for (int wheelId = 1; wheelId <= WaterWheelManager.MaxWheelCount; wheelId++)
		{
			if (wheelId == StartingWheelId || !IsWheelPurchased(wheelId))
				continue;

			wheelManager.TryActivateWheel(wheelId);
		}

		CreatePurchaseDisplays();
	}

	public bool TryPurchaseWheel(int wheelId)
	{
		if (!IsValidWheelId(wheelId))
			return false;

		if (IsWheelPurchased(wheelId))
			return false;

		if (!wheelManager.HasWheelLocation(wheelId))
			return false;

		// Check funds before creating runtime state. The economy transaction is
		// then performed immediately after successful wheel activation.
		if (energySystem.Dollars < EnergySystem.WheelPurchaseCost)
			return false;

		if (!wheelManager.TryActivateWheel(wheelId))
			return false;

		if (!energySystem.TrySpendDollars(EnergySystem.WheelPurchaseCost))
		{
			GD.PushError(
				"WheelPurchaseSystem: Dollar transaction failed after wheel activation."
			);
			return false;
		}

		wheelPurchased[wheelId - 1] = true;
		RemovePurchaseDisplay(wheelId);
		CloseConfirmation();

		GD.Print(
			"Wheel purchased: Wheel " +
			wheelId +
			" for $" +
			EnergySystem.WheelPurchaseCost.ToString("F0")
		);

		return true;
	}

	public void ShowPurchaseConfirmation(int wheelId)
	{
		if (!IsValidWheelId(wheelId) || IsWheelPurchased(wheelId))
			return;

		Vector2 position =
			wheelManager.GetWheelSimulationPosition(wheelId);

		if (confirmationWindow == null || !GodotObject.IsInstanceValid(confirmationWindow))
		{
			confirmationWindow =
				new WheelPurchaseConfirmationWindow(this);
			confirmationWindow.Name = "BuyWheelConfirmation";
			owner.AddChild(confirmationWindow);
		}

		confirmationWindow.ShowForWheel(
			wheelId,
			position
		);
	}

	public void CloseConfirmation()
	{
		if (confirmationWindow == null || !GodotObject.IsInstanceValid(confirmationWindow))
			return;

		confirmationWindow.Hide();
	}

	private void CreatePurchaseDisplays()
	{
		foreach (int wheelId in purchaseUiByWheelId.Keys)
		{
			purchaseUiByWheelId[wheelId].QueueFree();
		}

		purchaseUiByWheelId.Clear();

		for (int wheelId = 1; wheelId <= WaterWheelManager.MaxWheelCount; wheelId++)
		{
			if (IsWheelPurchased(wheelId) || !wheelManager.HasWheelLocation(wheelId))
				continue;

			WheelPurchaseWorldUi ui =
				new WheelPurchaseWorldUi(
					this,
					wheelId
				);

			ui.Name = "BuyWheel_" + wheelId;
			ui.Position =
				wheelManager.GetWheelSimulationPosition(wheelId) +
				new Vector2(-52.0f, -78.0f);

			owner.AddChild(ui);
			purchaseUiByWheelId[wheelId] = ui;
		}
	}

	private void RemovePurchaseDisplay(int wheelId)
	{
		if (!purchaseUiByWheelId.TryGetValue(wheelId, out WheelPurchaseWorldUi ui))
			return;

		if (GodotObject.IsInstanceValid(ui))
			ui.QueueFree();

		purchaseUiByWheelId.Remove(wheelId);
	}

	private static bool IsValidWheelId(int wheelId)
	{
		return wheelId >= 1 && wheelId <= WaterWheelManager.MaxWheelCount;
	}
}

/// <summary>
/// Small world-space clickable control displayed above an unpurchased wheel.
/// </summary>
internal sealed partial class WheelPurchaseWorldUi : Control
{
	private readonly WheelPurchaseSystem purchaseSystem;
	private readonly int wheelId;

	public WheelPurchaseWorldUi(
		WheelPurchaseSystem purchaseSystem,
		int wheelId)
	{
		this.purchaseSystem = purchaseSystem;
		this.wheelId = wheelId;

		MouseFilter = MouseFilterEnum.Pass;
		ZIndex = 100;
		CustomMinimumSize = new Vector2(104.0f, 36.0f);

		Button button = new Button();
		button.Text = "Buy Wheel";
		button.CustomMinimumSize = new Vector2(104.0f, 36.0f);
		button.MouseFilter = MouseFilterEnum.Stop;
		button.Pressed += OnPressed;
		AddChild(button);
	}

	private void OnPressed()
	{
		purchaseSystem.ShowPurchaseConfirmation(wheelId);
	}
}

/// <summary>
/// Contextual confirmation window for a single wheel purchase.
/// </summary>
internal sealed partial class WheelPurchaseConfirmationWindow : PanelContainer
{
	private readonly WheelPurchaseSystem purchaseSystem;
	private readonly Label messageLabel;
	private int wheelId;

	public WheelPurchaseConfirmationWindow(
		WheelPurchaseSystem purchaseSystem)
	{
		this.purchaseSystem = purchaseSystem;

		ZIndex = 200;
		CustomMinimumSize = new Vector2(280.0f, 120.0f);
		MouseFilter = MouseFilterEnum.Stop;

		MarginContainer margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 12);
		margin.AddThemeConstantOverride("margin_right", 12);
		margin.AddThemeConstantOverride("margin_top", 10);
		margin.AddThemeConstantOverride("margin_bottom", 10);
		AddChild(margin);

		VBoxContainer content = new VBoxContainer();
		content.AddThemeConstantOverride("separation", 8);
		margin.AddChild(content);

		messageLabel = new Label();
		messageLabel.Text = "Do you want to buy this wheel for 100$?";
		messageLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		content.AddChild(messageLabel);

		HBoxContainer buttons = new HBoxContainer();
		buttons.Alignment = BoxContainer.AlignmentMode.Center;
		buttons.AddThemeConstantOverride("separation", 12);
		content.AddChild(buttons);

		Button yesButton = new Button();
		yesButton.Text = "Yes";
		yesButton.CustomMinimumSize = new Vector2(88.0f, 42.0f);
		yesButton.Pressed += OnYesPressed;
		buttons.AddChild(yesButton);

		Button noButton = new Button();
		noButton.Text = "No";
		noButton.CustomMinimumSize = new Vector2(88.0f, 42.0f);
		noButton.Pressed += OnNoPressed;
		buttons.AddChild(noButton);

		Hide();
	}

	public void ShowForWheel(
		int wheelId,
		Vector2 simulationPosition)
	{
		this.wheelId = wheelId;
		messageLabel.Text = "Do you want to buy this wheel for 100$?";
		Position = simulationPosition + new Vector2(-140.0f, -145.0f);
		Show();
	}

	private void OnYesPressed()
	{
		if (purchaseSystem.TryPurchaseWheel(wheelId))
			return;

		if (EnergySystem.Instance == null ||
			EnergySystem.Instance.Dollars < EnergySystem.WheelPurchaseCost)
		{
			messageLabel.Text = "Not enough money. You need 100$.";
		}
		else
		{
			messageLabel.Text = "This wheel could not be activated.";
		}
	}

	private void OnNoPressed()
	{
		purchaseSystem.CloseConfirmation();
	}
}
