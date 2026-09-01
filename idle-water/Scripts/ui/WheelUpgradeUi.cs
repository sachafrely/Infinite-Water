// THIS FILE SHALL BE GONE SOON //

using Godot;

public partial class WheelUpgradeUi : Control
{
	private const int MaxWheelCount = 6;
	private const float WindowWidth = 100.0f;
	private const float WindowHeight = 54.0f;
	private readonly PanelContainer[] windows = new PanelContainer[MaxWheelCount];
	private readonly Button[] buttons = new Button[MaxWheelCount];
	private FluidSimulator simulator;
	private WheelUpgradeWindow upgradeWindow;
	private bool inputBlockedByPurchaseWindow;

	public override void _Ready()
	{
		// Legacy runtime-generated upgrade buttons are disabled; UpgradeWindow.tscn is the active UI.
		Visible = false;
		SetProcess(false);
		SetProcessInput(false);
		SetProcessUnhandledInput(false);
		return;

		ZIndex = 900; ZAsRelative = false; MouseFilter = MouseFilterEnum.Pass;
		simulator = GetTree().CurrentScene?.FindChild("FluidSimulation", true, false) as FluidSimulator;
		if (simulator == null) simulator = GetTree().Root.FindChild("FluidSimulation", true, false) as FluidSimulator;
		BuildWindows(); CallDeferred(nameof(Refresh));
	}
	public override void _Process(double delta) => Refresh();
	private void BuildWindows()
	{
		if (simulator == null) return;
		for (int wheelIndex = 0; wheelIndex < MaxWheelCount; wheelIndex++)
		{
			PanelContainer panel = new PanelContainer { Name = "UpgradeWheelWindow_" + (wheelIndex + 1), CustomMinimumSize = new Vector2(WindowWidth, WindowHeight), Size = new Vector2(WindowWidth, WindowHeight), MouseFilter = MouseFilterEnum.Stop, ZIndex = 901, ZAsRelative = false };
			panel.AddThemeStyleboxOverride("panel", UiSettings.CreateBox(UiSettings.WindowColor, UiSettings.WindowColor, 0));
			VBoxContainer content = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; content.AddThemeConstantOverride("separation", 0); panel.AddChild(content);
			Button button = new Button { Name = "UpgradeButton", Text = "Upgrade", CustomMinimumSize = new Vector2(WindowWidth, WindowHeight), Size = new Vector2(WindowWidth, WindowHeight), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill, FocusMode = Control.FocusModeEnum.None, MouseFilter = Control.MouseFilterEnum.Stop };
			button.AddThemeFontSizeOverride("font_size", UiSettings.FontSizeMedium); ApplyButtonStyle(button, false);
			int capturedIndex = wheelIndex; button.Pressed += () => OnUpgradePressed(capturedIndex); content.AddChild(button); AddChild(panel); windows[wheelIndex] = panel; buttons[wheelIndex] = button;
		}
	}
	private void ApplyButtonStyle(Button button, bool available)
	{
		button.Disabled = false; Color textColor = available ? UiSettings.FontColorEnabled : UiSettings.FontColorDisabled;
		button.AddThemeColorOverride("font_color", textColor); button.AddThemeColorOverride("font_hover_color", textColor); button.AddThemeColorOverride("font_pressed_color", textColor); button.AddThemeColorOverride("font_focus_color", textColor); button.AddThemeColorOverride("font_disabled_color", UiSettings.FontColorDisabled);
		button.AddThemeStyleboxOverride("normal", UiSettings.CreateBox(UiSettings.ButtonUnpressedColor)); button.AddThemeStyleboxOverride("hover", UiSettings.CreateBox(UiSettings.ButtonUnpressedColor)); button.AddThemeStyleboxOverride("pressed", UiSettings.CreateBox(UiSettings.ButtonPressedColor)); button.AddThemeStyleboxOverride("focus", UiSettings.CreateBox(UiSettings.ButtonUnpressedColor)); button.AddThemeStyleboxOverride("disabled", UiSettings.CreateBox(UiSettings.ButtonUnpressedColor));
	}
	private void OnUpgradePressed(int wheelIndex)
	{
		if (inputBlockedByPurchaseWindow || simulator == null || !simulator.IsWheelUnlocked(wheelIndex) || !simulator.HasAvailableWheelUpgrades(wheelIndex)) return;
		CloseSettingsAndStatisticsWindow(); (GetTree().Root.FindChild("WheelPurchaseUi", true, false) as WheelPurchaseUi)?.ClosePurchaseWindow(); OpenUpgradeWindow(wheelIndex);
	}
	private void CloseSettingsAndStatisticsWindow() { (GetTree().Root.FindChild("UiWindowManager", true, false) as UiWindowManager)?.CloseActiveWindow(); }
	private void OpenUpgradeWindow(int wheelIndex)
	{
		CloseUpgradeWindow(); upgradeWindow = new WheelUpgradeWindow { Name = "WheelUpgradeWindow", ZIndex = 2000, ZAsRelative = false }; AddChild(upgradeWindow); upgradeWindow.Setup(wheelIndex, simulator, () => upgradeWindow = null);
		Vector2 anchor = simulator.GetWheelUiPosition(wheelIndex), size = upgradeWindow.Size, viewportSize = GetViewportRect().Size; Vector2 position = anchor - size * 0.5f;
		position.X = Mathf.Clamp(position.X, 4.0f, Mathf.Max(4.0f, viewportSize.X - size.X - 4.0f)); position.Y = Mathf.Clamp(position.Y, 4.0f, Mathf.Max(4.0f, viewportSize.Y - size.Y - 4.0f)); upgradeWindow.Position = position;
	}
	public void CloseUpgradeWindow() { if (upgradeWindow != null && IsInstanceValid(upgradeWindow)) upgradeWindow.QueueFree(); upgradeWindow = null; }
	public void SetPurchaseModalInputBlocked(bool blocked)
	{
		inputBlockedByPurchaseWindow = blocked;
		for (int i = 0; i < buttons.Length; i++) { if (buttons[i] != null) buttons[i].MouseFilter = blocked ? Control.MouseFilterEnum.Ignore : Control.MouseFilterEnum.Stop; if (windows[i] != null) windows[i].MouseFilter = blocked ? Control.MouseFilterEnum.Ignore : Control.MouseFilterEnum.Stop; }
	}
	private bool CanPurchaseAnyUpgrade(int wheelIndex)
	{
		if (simulator == null) return false;
		WheelUpgradeType[] types = { WheelUpgradeType.BiggerPaddles, WheelUpgradeType.LessFriction, WheelUpgradeType.MoreEfficient };
		for (int i = 0; i < types.Length; i++) if (simulator.CanPurchaseWheelUpgrade(wheelIndex, types[i])) return true;
		return false;
	}
	private void Refresh()
	{
		if (simulator == null || !IsInsideTree()) return;
		for (int wheelIndex = 0; wheelIndex < MaxWheelCount; wheelIndex++)
		{
			PanelContainer panel = windows[wheelIndex]; if (panel == null) continue;
			bool hasUpgrade = simulator.IsWheelUnlocked(wheelIndex) && simulator.HasAvailableWheelUpgrades(wheelIndex); panel.Visible = hasUpgrade; if (!hasUpgrade) continue;
			Vector2 wheelPosition = simulator.GetWheelUiPosition(wheelIndex); panel.Position = new Vector2(wheelPosition.X - WindowWidth * 0.5f - 4.0f, wheelPosition.Y - WindowHeight * 0.5f);
			if (buttons[wheelIndex] != null) { ApplyButtonStyle(buttons[wheelIndex], CanPurchaseAnyUpgrade(wheelIndex)); buttons[wheelIndex].MouseFilter = inputBlockedByPurchaseWindow ? Control.MouseFilterEnum.Ignore : Control.MouseFilterEnum.Stop; }
			panel.MouseFilter = inputBlockedByPurchaseWindow ? Control.MouseFilterEnum.Ignore : Control.MouseFilterEnum.Stop;
		}
		if (upgradeWindow != null && IsInstanceValid(upgradeWindow) && (!simulator.IsWheelUnlocked(upgradeWindow.WheelIndex) || !simulator.HasAvailableWheelUpgrades(upgradeWindow.WheelIndex))) CloseUpgradeWindow();
	}
}
