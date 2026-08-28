using Godot;

/// <summary>
/// Controls the authored UpgradeWindow.tscn scene.
/// One instance is reused for whichever unlocked wheel is selected.
/// </summary>
public partial class UpgradeWindow : Control
{
	private readonly WheelUpgradeType[] types =
	{
		WheelUpgradeType.BiggerPaddles,
		WheelUpgradeType.LessFriction,
		WheelUpgradeType.MoreEfficient
	};

	private FluidSimulator simulator;
	private int wheelIndex = -1;
	private Label title;
	private readonly Label[] levels = new Label[3];
	private readonly Button[] purchaseButtons = new Button[3];
	private Button closeButton;

	public int WheelIndex => wheelIndex;

	public override void _Ready()
	{
		title = GetNodeOrNull<Label>("Panel/Margin/Content/Title");
		levels[0] = GetNodeOrNull<Label>("Panel/Margin/Content/BiggerPaddlesRow/Level");
		levels[1] = GetNodeOrNull<Label>("Panel/Margin/Content/LessFrictionRow/Level");
		levels[2] = GetNodeOrNull<Label>("Panel/Margin/Content/MoreEfficientRow/Level");
		purchaseButtons[0] = GetNodeOrNull<Button>("Panel/Margin/Content/BiggerPaddlesRow/PurchaseButton");
		purchaseButtons[1] = GetNodeOrNull<Button>("Panel/Margin/Content/LessFrictionRow/PurchaseButton");
		purchaseButtons[2] = GetNodeOrNull<Button>("Panel/Margin/Content/MoreEfficientRow/PurchaseButton");
		closeButton = GetNodeOrNull<Button>("Panel/Margin/Content/CloseButton");

		for (int i = 0; i < purchaseButtons.Length; i++)
		{
			int capturedIndex = i;
			purchaseButtons[i]?.Pressed += () => Purchase(types[capturedIndex]);
		}

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

	public void Close() => Hide();
	public bool IsOpen() => Visible;

	public override void _Process(double delta)
	{
		if (Visible)
			Refresh();
	}

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

	private void Purchase(WheelUpgradeType type)
	{
		if (simulator == null || wheelIndex < 0)
			return;

		if (simulator.PurchaseWheelUpgrade(wheelIndex, type))
		{
			Refresh();
			if (!simulator.HasAvailableWheelUpgrades(wheelIndex))
				Close();
		}
	}

	private void MoveToWheel()
	{
		Vector2 anchor = simulator.GetWheelUiPosition(wheelIndex);
		Vector2 viewportSize = GetViewportRect().Size;
		Vector2 position = anchor - Size * 0.5f;
		position.X = Mathf.Clamp(position.X, 4.0f, Mathf.Max(4.0f, viewportSize.X - Size.X - 4.0f));
		position.Y = Mathf.Clamp(position.Y, 4.0f, Mathf.Max(4.0f, viewportSize.Y - Size.Y - 4.0f));
		Position = position;
	}

	private void Refresh()
	{
		if (title == null)
			return;

		title.AddThemeFontSizeOverride("font_size", UiSettings.FontSizeMedium);
		title.Text = wheelIndex >= 0 ? $"Wheel {wheelIndex + 1} Upgrades" : "Wheel Upgrades";
		closeButton?.AddThemeFontSizeOverride("font_size", UiSettings.FontSizeMedium);

		if (simulator == null || wheelIndex < 0)
			return;

		for (int i = 0; i < types.Length; i++)
		{
			int level = simulator.GetWheelUpgradeLevel(wheelIndex, types[i]);
			int price = simulator.GetWheelUpgradePrice(wheelIndex, types[i]);
			bool maxed = level >= WheelUpgradeState.MaxLevel;
			bool available = !maxed && simulator.CanPurchaseWheelUpgrade(wheelIndex, types[i]);
			Color textColor = available ? UiSettings.FontColorEnabled : UiSettings.FontColorDisabled;

			levels[i]?.AddThemeFontSizeOverride("font_size", UiSettings.FontSizeMedium);
			if (levels[i] != null)
				levels[i].Text = $"Lv {level}";

			Button button = purchaseButtons[i];
			if (button == null)
				continue;

			button.AddThemeFontSizeOverride("font_size", UiSettings.FontSizeMedium);
			button.Text = maxed ? "MAX" : $"{price}$";
			button.Disabled = false;
			button.AddThemeColorOverride("font_color", textColor);
			button.AddThemeColorOverride("font_hover_color", textColor);
			button.AddThemeColorOverride("font_pressed_color", textColor);
			button.AddThemeColorOverride("font_focus_color", textColor);
			button.AddThemeColorOverride("font_disabled_color", UiSettings.FontColorDisabled);
			button.AddThemeStyleboxOverride("normal", UiSettings.CreateBox(UiSettings.ButtonUnpressedColor, UiSettings.BorderColor, UiSettings.ButtonBorderSize));
			button.AddThemeStyleboxOverride("hover", UiSettings.CreateBox(UiSettings.ButtonUnpressedColor, UiSettings.BorderColor, UiSettings.ButtonBorderSize));
			button.AddThemeStyleboxOverride("pressed", UiSettings.CreateBox(UiSettings.ButtonPressedColor, UiSettings.BorderColor, UiSettings.ButtonBorderSize));
			button.AddThemeStyleboxOverride("focus", UiSettings.CreateBox(UiSettings.ButtonUnpressedColor, UiSettings.BorderColor, UiSettings.ButtonBorderSize));
			button.AddThemeStyleboxOverride("disabled", UiSettings.CreateBox(UiSettings.ButtonUnpressedColor, UiSettings.BorderColor, UiSettings.ButtonBorderSize));
		}
	}
}
