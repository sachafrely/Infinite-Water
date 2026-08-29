// What Node is this Script attached to?
// Is it needed? Should it be migrated?
// I think it is the old script, which we should recycle.

using System;
using Godot;

public partial class WheelUpgradeWindow : Control
{
	private const float WindowWidth = 440.0f;
	private const float WindowHeight = 340.0f;
	private const float RowHeight = 72.0f;
	private const float CloseButtonHeight = 50.0f;
	private const float UpgradeButtonWidth = 120.0f;

	private FluidSimulator simulator;
	private Action closeAction;
	private readonly Button[] purchaseButtons = new Button[3];
	private readonly Label[] levelLabels = new Label[3];
	private PanelContainer panel;

	public int WheelIndex { get; private set; } = -1;

	public void Setup(int wheelIndex, FluidSimulator ownerSimulator, Action onClosed)
	{
		WheelIndex = wheelIndex;
		simulator = ownerSimulator;
		closeAction = onClosed;
		BuildWindow();
		Refresh();
	}

	private void BuildWindow()
	{
		CustomMinimumSize = new Vector2(WindowWidth, WindowHeight);
		Size = CustomMinimumSize;
		MouseFilter = MouseFilterEnum.Stop;
		ZIndex = 2000;
		ZAsRelative = false;

		panel = new PanelContainer();
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
			Text = "Wheel Upgrades",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			CustomMinimumSize = new Vector2(0.0f, 42.0f)
		};
		title.AddThemeFontSizeOverride("font_size", UiSettings.FontSizeMedium);
		content.AddChild(title);

		CreateRow(content, 0, WheelUpgradeType.BiggerPaddles, "Bigger Paddles");
		CreateRow(content, 1, WheelUpgradeType.LessFriction, "Less Friction");
		CreateRow(content, 2, WheelUpgradeType.MoreEfficient, "More Efficient");

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
		closeButton.Pressed += Close;
		content.AddChild(closeButton);
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
			Close();
			GetViewport().SetInputAsHandled();
		}
	}

	private void CreateRow(VBoxContainer parent, int arrayIndex, WheelUpgradeType type, string title)
	{
		HBoxContainer row = new HBoxContainer
		{
			CustomMinimumSize = new Vector2(0.0f, RowHeight)
		};
		row.AddThemeConstantOverride("separation", 10);
		parent.AddChild(row);

		Label label = new Label
		{
			Text = title,
			VerticalAlignment = VerticalAlignment.Center,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		label.AddThemeFontSizeOverride("font_size", UiSettings.FontSizeMedium);
		row.AddChild(label);

		Label level = new Label
		{
			VerticalAlignment = VerticalAlignment.Center,
			CustomMinimumSize = new Vector2(68.0f, 0.0f),
			HorizontalAlignment = HorizontalAlignment.Center
		};
		level.AddThemeFontSizeOverride("font_size", UiSettings.FontSizeMedium);
		row.AddChild(level);
		levelLabels[arrayIndex] = level;

		Button button = new Button
		{
			CustomMinimumSize = new Vector2(UpgradeButtonWidth, RowHeight - 6.0f),
			FocusMode = Control.FocusModeEnum.None,
			MouseFilter = MouseFilterEnum.Stop
		};
		button.AddThemeFontSizeOverride("font_size", UiSettings.FontSizeMedium);
		ApplyButtonStyle(button);
		button.Pressed += () => Purchase(type);
		row.AddChild(button);
		purchaseButtons[arrayIndex] = button;
	}

	private void ApplyButtonStyle(Button button)
	{
		button.AddThemeStyleboxOverride("normal", UiSettings.CreateBox(UiSettings.ButtonUnpressedColor, UiSettings.BorderColor, UiSettings.ButtonBorderSize));
		button.AddThemeStyleboxOverride("hover", UiSettings.CreateBox(UiSettings.ButtonUnpressedColor, UiSettings.BorderColor, UiSettings.ButtonBorderSize));
		button.AddThemeStyleboxOverride("pressed", UiSettings.CreateBox(UiSettings.ButtonPressedColor, UiSettings.BorderColor, UiSettings.ButtonBorderSize));
		button.AddThemeStyleboxOverride("focus", UiSettings.CreateBox(UiSettings.ButtonUnpressedColor, UiSettings.BorderColor, UiSettings.ButtonBorderSize));
		button.AddThemeStyleboxOverride("disabled", UiSettings.CreateBox(UiSettings.ButtonUnpressedColor, UiSettings.BorderColor, UiSettings.ButtonBorderSize));
	}

	private void Purchase(WheelUpgradeType type)
	{
		if (simulator == null || WheelIndex < 0)
			return;

		if (simulator.PurchaseWheelUpgrade(WheelIndex, type))
		{
			Refresh();
			if (!simulator.HasAvailableWheelUpgrades(WheelIndex))
				Close();
		}
	}

	private void Refresh()
	{
		if (simulator == null || WheelIndex < 0)
			return;

		WheelUpgradeType[] types =
		{
			WheelUpgradeType.BiggerPaddles,
			WheelUpgradeType.LessFriction,
			WheelUpgradeType.MoreEfficient
		};

		for (int i = 0; i < types.Length; i++)
		{
			int level = simulator.GetWheelUpgradeLevel(WheelIndex, types[i]);
			int price = simulator.GetWheelUpgradePrice(WheelIndex, types[i]);
			bool maxed = level >= WheelUpgradeState.MaxLevel;
			bool canBuy = simulator.CanPurchaseWheelUpgrade(WheelIndex, types[i]);
			bool available = !maxed && canBuy;
			Color textColor = available ? UiSettings.FontColorEnabled : UiSettings.FontColorDisabled;

			if (levelLabels[i] != null)
				levelLabels[i].Text = "Lv " + level;

			if (purchaseButtons[i] == null)
				continue;

			purchaseButtons[i].Text = maxed ? "MAX" : price + "$";
			purchaseButtons[i].Disabled = false;
			purchaseButtons[i].AddThemeColorOverride("font_color", textColor);
			purchaseButtons[i].AddThemeColorOverride("font_hover_color", textColor);
			purchaseButtons[i].AddThemeColorOverride("font_pressed_color", textColor);
			purchaseButtons[i].AddThemeColorOverride("font_focus_color", textColor);
			purchaseButtons[i].AddThemeColorOverride("font_disabled_color", UiSettings.FontColorDisabled);
		}
	}

	private void Close()
	{
		closeAction?.Invoke();
		QueueFree();
	}
}
