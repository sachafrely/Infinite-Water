using System;
using Godot;

/// <summary>
/// Creates the five small wheel-purchase windows for the five locked wheel slots.
/// Each window is centered directly on the tile position where its wheel will
/// appear. The window always uses the same two-row text:
/// "Buy Wheel" followed by the current purchase price.
/// </summary>
public partial class WheelPurchaseUi : Control
{
    private const int MaxWheelCount = 6;
    private const float WindowWidth = 116.0f;
    private const float WindowHeight = 58.0f;
    private const float BorderWidth = 2.0f;

    private readonly PanelContainer[] windows = new PanelContainer[MaxWheelCount];
    private readonly Button[] buttons = new Button[MaxWheelCount];

    private FluidSimulator simulator;

    public override void _Ready()
    {
        ZIndex = 900;
        MouseFilter = MouseFilterEnum.Pass;

        simulator = GetParent() as FluidSimulator;
        if (simulator == null)
        {
            simulator = GetTree().CurrentScene?.FindChild(
                "FluidSimulation",
                true,
                false
            ) as FluidSimulator;
        }

        BuildWindows();
        CallDeferred(nameof(Refresh));
    }

    public override void _Process(double delta)
    {
        Refresh();
    }

    private void BuildWindows()
    {
        if (simulator == null)
            return;

        for (int wheelIndex = 0; wheelIndex < MaxWheelCount; wheelIndex++)
        {
            if (simulator.IsWheelUnlocked(wheelIndex))
                continue;

            PanelContainer panel = new PanelContainer();
            panel.Name = "BuyWheelWindow_" + (wheelIndex + 1);
            panel.CustomMinimumSize = new Vector2(WindowWidth, WindowHeight);
            panel.Size = new Vector2(WindowWidth, WindowHeight);
            panel.MouseFilter = MouseFilterEnum.Stop;
            panel.ZIndex = 901;

            StyleBoxFlat background = new StyleBoxFlat();
            background.BgColor = new Color(0.035f, 0.055f, 0.055f, 0.96f);
            background.BorderColor = new Color(0.75f, 0.75f, 0.75f, 1.0f);
            background.SetBorderWidthAll((int)BorderWidth);
            background.CornerRadiusTopLeft = 3;
            background.CornerRadiusTopRight = 3;
            background.CornerRadiusBottomLeft = 3;
            background.CornerRadiusBottomRight = 3;
            panel.AddThemeStyleboxOverride("panel", background);

            VBoxContainer content = new VBoxContainer();
            content.Alignment = BoxContainer.AlignmentMode.Center;
            content.AddThemeConstantOverride("separation", 1);
            panel.AddChild(content);

            Label title = new Label();
            title.Text = "Buy Wheel";
            title.HorizontalAlignment = HorizontalAlignment.Center;
            title.AddThemeFontSizeOverride("font_size", 11);
            content.AddChild(title);

            Button button = new Button();
            button.Name = "BuyButton";
            button.Text = EnergySystem.WheelPurchaseCost.ToString("F0") + "$";
            button.CustomMinimumSize = new Vector2(100.0f, 27.0f);
            button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            button.FocusMode = Control.FocusModeEnum.None;
            int capturedIndex = wheelIndex;
            button.Pressed += () => OnBuyPressed(capturedIndex);
            content.AddChild(button);
            buttons[wheelIndex] = button;

            AddChild(panel);
            windows[wheelIndex] = panel;
        }
    }

    private void OnBuyPressed(int wheelIndex)
    {
        if (simulator == null)
            return;

        if (simulator.GetNextLockedWheelIndex() != wheelIndex)
            return;

        if (simulator.TryPurchaseNextWheel())
            Refresh();
    }

    private void Refresh()
    {
        if (simulator == null || !IsInsideTree())
            return;

        for (int wheelIndex = 0; wheelIndex < MaxWheelCount; wheelIndex++)
        {
            PanelContainer panel = windows[wheelIndex];
            if (panel == null)
                continue;

            if (simulator.IsWheelUnlocked(wheelIndex))
            {
                panel.Visible = false;
                continue;
            }

            panel.Visible = true;

            Vector2 wheelPosition = simulator.GetWheelPosition(wheelIndex);

            // Center the purchase window exactly on the wheel tile.
            panel.Position = new Vector2(
                wheelPosition.X - WindowWidth * 0.5f,
                wheelPosition.Y - WindowHeight * 0.5f
            );

            Button button = buttons[wheelIndex];
            bool isNext = simulator.GetNextLockedWheelIndex() == wheelIndex;

            if (button != null)
            {
                button.Text = EnergySystem.WheelPurchaseCost.ToString("F0") + "$";
                button.Disabled = !isNext ||
                    EnergySystem.Instance == null ||
                    EnergySystem.Instance.Dollars < EnergySystem.WheelPurchaseCost;
            }
        }
    }
}
