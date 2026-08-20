using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Coordinates per-wheel ownership, economy transactions, and world purchase UI.
/// Wheel ownership is stored per stable wheel ID rather than as an unlock count.
/// </summary>
internal sealed class WheelPurchaseSystem
{
	// Wheel IDs are assigned top-to-bottom, then left-to-right.
	// The desired starting wheel is the third wheel from the top.
	public const int StartingWheelId = 3;

	private readonly WaterWheelManager wheelManager;
	private readonly EnergySystem energySystem;
	private readonly Node uiOwner;
	private readonly bool[] wheelPurchased =
		new bool[WaterWheelManager.MaxWheelCount];
	private readonly Dictionary<int, WheelPurchaseWorldUi> purchaseUiByWheelId =
		new Dictionary<int, WheelPurchaseWorldUi>();

	private WheelPurchaseConfirmationWindow confirmationWindow;

	public WheelPurchaseSystem(
		WaterWheelManager wheelManager,
		EnergySystem energySystem,
		Node uiOwner)
	{
		this.wheelManager = wheelManager;
		this.energySystem = energySystem;
		this.uiOwner = uiOwner;

		// New games start with exactly Wheel 3 active.
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
		{
			GD.PushWarning("WheelPurchaseSystem: No wheel locations were discovered.");
			return;
		}

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
		if (!IsValidWheelId(wheelId) || IsWheelPurchased(wheelId))
			return false;

		if (!wheelManager.HasWheelLocation(wheelId))
			return false;

		if (energySystem.Dollars < EnergySystem.WheelPurchaseCost)
			return false;

		if (!wheelManager.TryActivateWheel(wheelId))
			return false;

		if (!energySystem.TrySpendDollars(EnergySystem.WheelPurchaseCost))
		{
			GD.PushError("WheelPurchaseSystem: Dollar transaction failed after wheel activation.");
			return false;
		}

		wheelPurchased[wheelId - 1] = true;
		RemovePurchaseDisplay(wheelId);
		CloseConfirmation();

		GD.Print(
			"Wheel purchased: Wheel " + wheelId +
			" for $" + EnergySystem.WheelPurchaseCost.ToString("F0")
		);

		return true;
	}

	public void ShowPurchaseConfirmation(int wheelId)
	{
		if (!IsValidWheelId(wheelId) || IsWheelPurchased(wheelId))
			return;

		Vector2 position = wheelManager.GetWheelSimulationPosition(wheelId);

		if (confirmationWindow == null || !GodotObject.IsInstanceValid(confirmationWindow))
		{
			confirmationWindow = new WheelPurchaseConfirmationWindow(this);
			confirmationWindow.Name = "BuyWheelConfirmation";
			uiOwner.AddChild(confirmationWindow);
		}

		confirmationWindow.ShowForWheel(wheelId, position);
	}

	public void CloseConfirmation()
	{
		if (confirmationWindow == null || !GodotObject.IsInstanceValid(confirmationWindow))
			return;

		confirmationWindow.Hide();
	}

	private void CreatePurchaseDisplays()
	{
		foreach (WheelPurchaseWorldUi ui in purchaseUiByWheelId.Values)
		{
			if (GodotObject.IsInstanceValid(ui))
				ui.QueueFree();
		}

		purchaseUiByWheelId.Clear();

		for (int wheelId = 1; wheelId <= WaterWheelManager.MaxWheelCount; wheelId++)
		{
			if (IsWheelPurchased(wheelId) || !wheelManager.HasWheelLocation(wheelId))
				continue;

			WheelPurchaseWorldUi ui = new WheelPurchaseWorldUi(this, wheelId);
			ui.Name = "BuyWheel_" + wheelId;
			ui.Position = wheelManager.GetWheelSimulationPosition(wheelId) + new Vector2(-60.0f, -50.0f);
			ui.ZIndex = 500;
			ui.Show();

			uiOwner.AddChild(ui);
			purchaseUiByWheelId[wheelId] = ui;

			GD.Print(
				"Buy Wheel UI created for Wheel " + wheelId +
				" at GameView position " + ui.Position
			);
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
/// Small clickable control displayed in the GameView overlay above an unpurchased wheel.
/// GameView uses the same 720x1160 coordinate space as the simulation viewport, so
/// simulation coordinates can be used directly without a second camera transform.
/// </summary>
internal sealed partial class WheelPurchaseWorldUi : Control
{
	private readonly WheelPurchaseSystem purchaseSystem;
	private readonly int wheelId;

	public WheelPurchaseWorldUi(WheelPurchaseSystem purchaseSystem, int wheelId)
	{
		this.purchaseSystem = purchaseSystem;
		this.wheelId = wheelId;

		MouseFilter = MouseFilterEnum.Ignore;
		ZIndex = 500;
		Size = new Vector2(120.0f, 40.0f);
		CustomMinimumSize = new Vector2(120.0f, 40.0f);
		Visible = true;

		Button button = new Button();
		button.Name = "BuyButton";
		button.Text = "Buy Wheel";
		button.Position = Vector2.Zero;
		button.Size = new Vector2(120.0f, 40.0f);
		button.CustomMinimumSize = new Vector2(120.0f, 40.0f);
		button.MouseFilter = MouseFilterEnum.Stop;
		button.ZIndex = 501;
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

	public WheelPurchaseConfirmationWindow(WheelPurchaseSystem purchaseSystem)
	{
		this.purchaseSystem = purchaseSystem;

		ZIndex = 550;
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

	public void ShowForWheel(int wheelId, Vector2 simulationPosition)
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

		if (EnergySystem.Instance == null || EnergySystem.Instance.Dollars < EnergySystem.WheelPurchaseCost)
			messageLabel.Text = "Not enough money. You need 100$.";
		else
			messageLabel.Text = "This wheel could not be activated.";
	}

	private void OnNoPressed()
	{
		purchaseSystem.CloseConfirmation();
	}
}
