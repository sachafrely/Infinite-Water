using Godot;

/// <summary>
/// Controls the authored BuyWindow.tscn scene.
/// One instance is reused for whichever locked wheel is selected.
/// </summary>
public partial class BuyWindow : Control
{
	private const double PurchaseCost = 10.0;

	private Label title;
	private Label description;
	private Button purchaseButton;
	private Button closeButton;
	private FluidSimulator simulator;
	private int wheelIndex = -1;

	public int WheelIndex => wheelIndex;

	public override void _Ready()
	{
		title = GetNodeOrNull<Label>("Panel/Margin/Content/Title");
		description = GetNodeOrNull<Label>("Panel/Margin/Content/UnlockRow/Description");
		purchaseButton = GetNodeOrNull<Button>("Panel/Margin/Content/UnlockRow/PurchaseButton");
		closeButton = GetNodeOrNull<Button>("Panel/Margin/Content/CloseButton");

		purchaseButton?.Pressed += Purchase;
		closeButton?.Pressed += Close;

		MouseFilter = MouseFilterEnum.Stop;
		Hide();
		Refresh();
	}

	public void Setup(int logicalWheelNumber, FluidSimulator ownerSimulator)
	{
		wheelIndex = logicalWheelNumber - 1;
		simulator = ownerSimulator;
		Refresh();
	}

	public void Open()
	{
		if (wheelIndex < 0 || simulator == null)
			return;

		Show();
		Refresh();
		MoveToWheel();
		QueueRedraw();
	}

	public void Close()
	{
		Hide();
	}

	public bool IsOpen() => Visible;

	public override void _Input(InputEvent @event)
	{
		if (!Visible)
			return;

		Vector2 position = Vector2.Zero;
		bool pressed = false;

		if (@event is InputEventMouseButton mouse && mouse.ButtonIndex == MouseButton.Left && mouse.Pressed)
		{
			position = mouse.GlobalPosition;
			pressed = true;
		}
		else if (@event is InputEventScreenTouch touch && touch.Pressed)
		{
			position = touch.Position;
			pressed = true;
		}

		if (pressed && !GetGlobalRect().HasPoint(position))
		{
			Close();
			GetViewport().SetInputAsHandled();
		}
	}

	private void Purchase()
	{
		if (simulator == null || wheelIndex < 0 || simulator.IsWheelUnlocked(wheelIndex))
		{
			Close();
			return;
		}

		if (EnergySystem.Instance == null || EnergySystem.Instance.Dollars < PurchaseCost)
			return;

		simulator.TryPurchaseWheel(wheelIndex);
		Close();
	}

	private void MoveToWheel()
	{
		Vector2 anchor = simulator.GetWheelUiPosition(wheelIndex);
		Vector2 viewportSize = GetViewportRect().Size;
		Vector2 size = Size;
		Vector2 position = anchor - size * 0.5f;
		position.X = Mathf.Clamp(position.X, 4.0f, Mathf.Max(4.0f, viewportSize.X - size.X - 4.0f));
		position.Y = Mathf.Clamp(position.Y, 4.0f, Mathf.Max(4.0f, viewportSize.Y - size.Y - 4.0f));
		Position = position;
	}

	private void Refresh()
	{
		if (purchaseButton == null)
			return;

		bool available = EnergySystem.Instance != null && EnergySystem.Instance.Dollars >= PurchaseCost;
		Color textColor = available ? UiSettings.FontColorEnabled : UiSettings.FontColorDisabled;

		if (title != null)
			title.Text = wheelIndex >= 0 ? $"Buy Wheel {wheelIndex + 1}" : "Buy Wheel";
		if (description != null)
			description.Text = wheelIndex >= 0 ? $"Unlock Wheel {wheelIndex + 1}" : "Unlock Wheel";

		title?.AddThemeFontSizeOverride("font_size", UiSettings.FontSizeMedium);
		description?.AddThemeFontSizeOverride("font_size", UiSettings.FontSizeMedium);
		purchaseButton.AddThemeFontSizeOverride("font_size", UiSettings.FontSizeMedium);
		closeButton?.AddThemeFontSizeOverride("font_size", UiSettings.FontSizeMedium);

		purchaseButton.Disabled = false;
		purchaseButton.Text = $"{PurchaseCost:0}$";
		purchaseButton.AddThemeColorOverride("font_color", textColor);
		purchaseButton.AddThemeColorOverride("font_hover_color", textColor);
		purchaseButton.AddThemeColorOverride("font_pressed_color", textColor);
		purchaseButton.AddThemeColorOverride("font_focus_color", textColor);
		purchaseButton.AddThemeColorOverride("font_disabled_color", UiSettings.FontColorDisabled);
		purchaseButton.AddThemeStyleboxOverride("normal", UiSettings.CreateBox(UiSettings.ButtonUnpressedColor, UiSettings.BorderColor, UiSettings.ButtonBorderSize));
		purchaseButton.AddThemeStyleboxOverride("hover", UiSettings.CreateBox(UiSettings.ButtonUnpressedColor, UiSettings.BorderColor, UiSettings.ButtonBorderSize));
		purchaseButton.AddThemeStyleboxOverride("pressed", UiSettings.CreateBox(UiSettings.ButtonPressedColor, UiSettings.BorderColor, UiSettings.ButtonBorderSize));
		purchaseButton.AddThemeStyleboxOverride("focus", UiSettings.CreateBox(UiSettings.ButtonUnpressedColor, UiSettings.BorderColor, UiSettings.ButtonBorderSize));
		purchaseButton.AddThemeStyleboxOverride("disabled", UiSettings.CreateBox(UiSettings.ButtonUnpressedColor, UiSettings.BorderColor, UiSettings.ButtonBorderSize));
	}
}
