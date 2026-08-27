using System;
using Godot;

/// <summary>
/// Modal Buy Wheel window styled like the existing WheelUpgradeWindow.
/// The window is centered around the wheel's Buy button and closes when the
/// user presses outside the window.
/// </summary>
public partial class WheelPurchaseConfirmationWindow : Control
{
	private const float WindowWidth = 440.0f;
	private const float WindowHeight = 250.0f;
	private const float RowHeight = 72.0f;
	private const float CloseButtonHeight = 50.0f;
	private const float PurchaseButtonWidth = 100.0f;
	private const float PurchaseCost = 10.0f;

	private int wheelIndex;
	private FluidSimulator simulator;
	private Action<int> confirmed;
	private Action cancelled;
	private PanelContainer panel;
	private Button purchaseButton;

	public void Setup(int index, FluidSimulator fluidSimulator, Action<int> onConfirmed, Action onCancelled)
	{
		wheelIndex = index;
		simulator = fluidSimulator;
		confirmed = onConfirmed;
		cancelled = onCancelled;
	}

	public override void _Ready()
	{
		ZIndex = 2000;
		ZAsRelative = false;
		MouseFilter = MouseFilterEnum.Stop;
		CustomMinimumSize = new Vector2(WindowWidth, WindowHeight);
		Size = CustomMinimumSize;

		panel = new PanelContainer();
		panel.Name = "BuyWheelPanel";
		panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		panel.MouseFilter = MouseFilterEnum.Stop;
		panel.AddThemeStyleboxOverride("panel", UiSettings.CreateBox(UiSettings.WindowColor, UiSettings.BorderColor, (int)UiSettings.BorderSize));
		AddChild(panel);

		MarginContainer margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 18);
		margin.AddThemeConstantOverride("margin_right", 18);
		margin.AddThemeConstantOverride("margin_top", 18);
		margin.AddThemeConstantOverride("margin_bottom", 18);
		panel.AddChild(margin);

		VBoxContainer content = new VBoxContainer();
		content.AddThemeConstantOverride("separation", 10);
		margin.AddChild(content);

		Label title = new Label
		{
			Text = "Buy Wheel",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			CustomMinimumSize = new Vector2(0.0f, 42.0f)
		};
		title.AddThemeFontSizeOverride("font_size", UiSettings.FontSizeMedium);
		content.AddChild(title);

		HBoxContainer row = new HBoxContainer
		{
			CustomMinimumSize = new Vector2(0.0f, RowHeight)
		};
		row.AddThemeConstantOverride("separation", 10);
		content.AddChild(row);

		Label label = new Label
		{
			Text = "Unlock Wheel",
			VerticalAlignment = VerticalAlignment.Center,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		label.AddThemeFontSizeOverride("font_size", UiSettings.FontSizeMedium);
		row.AddChild(label);

		purchaseButton = new Button
		{
			Text = "10$",
			CustomMinimumSize = new Vector2(PurchaseButtonWidth, RowHeight - 6.0f),
			FocusMode = Control.FocusModeEnum.None,
			MouseFilter = Control.MouseFilterEnum.Stop
		};
		purchaseButton.AddThemeFontSizeOverride("font_size", UiSettings.FontSizeMedium);
		purchaseButton.Pressed += Purchase;
		row.AddChild(purchaseButton);

		Control spacer = new Control
		{
			CustomMinimumSize = new Vector2(0.0f, 4.0f),
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		content.AddChild(spacer);

		Button closeButton = new Button
		{
			Text = "Close Window",
			CustomMinimumSize = new Vector2(0.0f, CloseButtonHeight),
			FocusMode = Control.FocusModeEnum.None,
			MouseFilter = MouseFilterEnum.Stop
		};
		closeButton.AddThemeFontSizeOverride("font_size", UiSettings.FontSizeMedium);
		closeButton.Pressed += Cancel;
		content.AddChild(closeButton);

		Refresh();
	}

	public override void _Process(double delta)
	{
		Refresh();
	}

	public override void _Input(InputEvent @event)
	{
		bool pressed = false;
		Vector2 position = Vector2.Zero;

		if (@event is InputEventMouseButton mouseButton && mouseButton.ButtonIndex == MouseButton.Left && mouseButton.Pressed)
		{
			pressed = true;
			position = mouseButton.GlobalPosition;
		}
		else if (@event is InputEventScreenTouch screenTouch && screenTouch.Pressed)
		{
			pressed = true;
			position = screenTouch.Position;
		}

		if (!pressed || panel == null || !IsInstanceValid(panel))
			return;

		if (!panel.GetGlobalRect().HasPoint(position))
		{
			Cancel();
			GetViewport().SetInputAsHandled();
		}
	}

	private void Refresh()
	{
		if (purchaseButton == null)
			return;

		bool available = EnergySystem.Instance != null && EnergySystem.Instance.Dollars >= PurchaseCost;
		Color textColor = available ? UiSettings.FontColorEnabled : UiSettings.FontColorDisabled;
		purchaseButton.Disabled = false;
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

	private void Purchase()
	{
		if (simulator == null || simulator.IsWheelUnlocked(wheelIndex))
		{
			Cancel();
			return;
		}

		bool available = EnergySystem.Instance != null && EnergySystem.Instance.Dollars >= PurchaseCost;
		if (!available)
			return;

		confirmed?.Invoke(wheelIndex);
		QueueFree();
	}

	private void Cancel()
	{
		cancelled?.Invoke();
		QueueFree();
	}
}
