using System.Collections.Generic;
using Godot;

/// <summary>
/// Coordinates per-wheel ownership, economy transactions, and wheel purchase UI.
/// Purchase controls are hosted directly in the CanvasLayer supplied by the
/// wheel bootstrap, so they are never clipped by GameView/SubViewport nodes.
/// </summary>
internal sealed class WheelPurchaseSystem
{
    public const int StartingWheelId = 3;

    private readonly WaterWheelManager wheelManager;
    private readonly EnergySystem energySystem;
    private readonly CanvasLayer uiLayer;
    private readonly bool[] wheelPurchased = new bool[WaterWheelManager.MaxWheelCount];
    private readonly Dictionary<int, WheelPurchaseWorldUi> purchaseUiByWheelId = new();

    private Control purchaseRoot;
    private WheelPurchaseConfirmationWindow confirmationWindow;

    public WheelPurchaseSystem(
        WaterWheelManager wheelManager,
        EnergySystem energySystem,
        Node uiOwner)
    {
        this.wheelManager = wheelManager;
        this.energySystem = energySystem;
        uiLayer = uiOwner as CanvasLayer;

        wheelPurchased[StartingWheelId - 1] = true;
    }

    public bool IsWheelPurchased(int wheelId)
    {
        return IsValidWheelId(wheelId) && wheelPurchased[wheelId - 1];
    }

    public void Initialize()
    {
        if (wheelManager.WheelLocationCount <= 0)
        {
            GD.PushWarning("WheelPurchaseSystem: No wheel locations were discovered.");
            return;
        }

        EnsurePurchaseRoot();
        wheelManager.TryActivateWheel(StartingWheelId);
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
            " for $" + EnergySystem.WheelPurchaseCost.ToString("F0"));

        return true;
    }

    public void ShowPurchaseConfirmation(int wheelId)
    {
        if (!IsValidWheelId(wheelId) || IsWheelPurchased(wheelId))
            return;

        EnsurePurchaseRoot();
        Vector2 position = wheelManager.GetWheelSimulationPosition(wheelId);

        if (confirmationWindow == null || !GodotObject.IsInstanceValid(confirmationWindow))
        {
            confirmationWindow = new WheelPurchaseConfirmationWindow(this);
            confirmationWindow.Name = "BuyWheelConfirmation";
            purchaseRoot.AddChild(confirmationWindow);
        }

        confirmationWindow.ShowForWheel(wheelId, position);
    }

    public void CloseConfirmation()
    {
        if (confirmationWindow != null && GodotObject.IsInstanceValid(confirmationWindow))
            confirmationWindow.Hide();
    }

    private void EnsurePurchaseRoot()
    {
        if (purchaseRoot != null && GodotObject.IsInstanceValid(purchaseRoot))
            return;

        if (uiLayer == null)
        {
            GD.PushError("WheelPurchaseSystem: WheelPurchaseUiLayer is not a CanvasLayer.");
            return;
        }

        purchaseRoot = new Control
        {
            Name = "WheelPurchaseUi",
            Position = Vector2.Zero,
            Size = uiLayer.GetViewport().GetVisibleRect().Size,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 0
        };
        uiLayer.AddChild(purchaseRoot);

        GD.Print(
            $"WheelPurchaseSystem: Purchase root created at {purchaseRoot.GetPath()}, " +
            $"screen size={purchaseRoot.Size}, CanvasLayer={uiLayer.Layer}.");
    }

    private void CreatePurchaseDisplays()
    {
        EnsurePurchaseRoot();
        if (purchaseRoot == null)
            return;

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

            WheelPurchaseWorldUi ui = new WheelPurchaseWorldUi(this, wheelId)
            {
                Name = "BuyWheel_" + wheelId,
                Position = wheelManager.GetWheelSimulationPosition(wheelId) + new Vector2(-60.0f, -50.0f)
            };

            purchaseRoot.AddChild(ui);
            purchaseUiByWheelId[wheelId] = ui;

            GD.Print(
                "Buy Wheel UI created for Wheel " + wheelId +
                " at screen position " + ui.Position +
                " parent=" + purchaseRoot.GetPath());
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

internal sealed partial class WheelPurchaseWorldUi : Control
{
    private readonly WheelPurchaseSystem purchaseSystem;
    private readonly int wheelId;
    private readonly Button button;

    public WheelPurchaseWorldUi(WheelPurchaseSystem purchaseSystem, int wheelId)
    {
        this.purchaseSystem = purchaseSystem;
        this.wheelId = wheelId;

        MouseFilter = Control.MouseFilterEnum.Ignore;
        ZIndex = 10;
        Size = new Vector2(120.0f, 44.0f);
        CustomMinimumSize = Size;
        Visible = true;

        button = new Button
        {
            Name = "BuyButton",
            Text = "Buy Wheel",
            Position = Vector2.Zero,
            Size = Size,
            CustomMinimumSize = Size,
            MouseFilter = Control.MouseFilterEnum.Stop,
            ZIndex = 11
        };
        button.Pressed += OnPressed;
        AddChild(button);

        ApplyButtonStyle();
    }

    private void ApplyButtonStyle()
    {
        button.AddThemeFontSizeOverride("font_size", UiSettings.FontSizeSmall);
        button.AddThemeColorOverride("font_color", Colors.White);
        button.AddThemeColorOverride("font_hover_color", Colors.White);
        button.AddThemeColorOverride("font_pressed_color", Colors.White);
        button.AddThemeColorOverride("font_focus_color", Colors.White);

        button.AddThemeStyleboxOverride("normal", CreateStyle(UiSettings.ButtonColor));
        button.AddThemeStyleboxOverride("hover", CreateStyle(UiSettings.WindowColor));
        button.AddThemeStyleboxOverride("pressed", CreateStyle(UiSettings.WindowColor));
        button.AddThemeStyleboxOverride("focus", CreateStyle(UiSettings.ButtonColor));
    }

    private static StyleBoxFlat CreateStyle(Color backgroundColor)
    {
        StyleBoxFlat style = new StyleBoxFlat
        {
            BgColor = backgroundColor,
            BorderColor = UiSettings.BorderColor
        };
        style.SetBorderWidthAll((int)UiSettings.BorderSize);
        return style;
    }

    private void OnPressed()
    {
        purchaseSystem.ShowPurchaseConfirmation(wheelId);
    }
}

internal sealed partial class WheelPurchaseConfirmationWindow : PanelContainer
{
    private readonly WheelPurchaseSystem purchaseSystem;
    private readonly Label messageLabel;
    private int wheelId;

    public WheelPurchaseConfirmationWindow(WheelPurchaseSystem purchaseSystem)
    {
        this.purchaseSystem = purchaseSystem;

        ZIndex = 100;
        CustomMinimumSize = new Vector2(280.0f, 120.0f);
        Size = new Vector2(280.0f, 120.0f);
        MouseFilter = Control.MouseFilterEnum.Stop;

        StyleBoxFlat style = new StyleBoxFlat
        {
            BgColor = UiSettings.WindowColor,
            BorderColor = UiSettings.BorderColor
        };
        style.SetBorderWidthAll((int)UiSettings.BorderSize);
        AddThemeStyleboxOverride("panel", style);

        MarginContainer margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        AddChild(margin);

        VBoxContainer content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 8);
        margin.AddChild(content);

        messageLabel = new Label
        {
            Text = "Do you want to buy this wheel for 100$?",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        messageLabel.AddThemeFontSizeOverride("font_size", UiSettings.FontSizeSmall);
        messageLabel.AddThemeColorOverride("font_color", Colors.White);
        content.AddChild(messageLabel);

        HBoxContainer buttons = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center
        };
        buttons.AddThemeConstantOverride("separation", 12);
        content.AddChild(buttons);

        Button yesButton = CreateButton("Yes");
        yesButton.Pressed += OnYesPressed;
        buttons.AddChild(yesButton);

        Button noButton = CreateButton("No");
        noButton.Pressed += OnNoPressed;
        buttons.AddChild(noButton);

        Hide();
    }

    private static Button CreateButton(string text)
    {
        Button button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(88.0f, 42.0f)
        };
        button.AddThemeFontSizeOverride("font_size", Colors.White == Colors.White ? UiSettings.FontSizeSmall : UiSettings.FontSizeSmall);
        button.AddThemeColorOverride("font_color", Colors.White);
        button.AddThemeColorOverride("font_hover_color", Colors.White);
        button.AddThemeColorOverride("font_pressed_color", Colors.White);
        return button;
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
