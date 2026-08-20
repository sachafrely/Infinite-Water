using Godot;

/// <summary>
/// Central manager for all UI windows.
/// Windows remain in the existing Content container, but the manager explicitly
/// controls their draw order so WindowBackground can never cover them.
/// </summary>
public partial class UiWindowManager : Node
{
    public enum UiState { NoWindow, Statistics, Settings }

    private Node activeWindow;
    private UiState currentState = UiState.NoWindow;

    private const string StatisticsWindowName = "StatisticsWindow";
    private const string SettingsWindowName = "SettingsWindow";
    private const int WindowBackgroundZIndex = 100;
    private const int WindowZIndex = 200;

    public override void _Ready()
    {
        CloseAllWindows();
        GD.Print("========== UI WINDOW MANAGER READY ==========");

        Node content = GetWindowContent();
        if (content == null)
        {
            GD.PushError("UiWindowManager: Could not find window Content container.");
            return;
        }

        if (content is CanvasItem contentCanvas)
            contentCanvas.Visible = true;

        GD.Print($"UiWindowManager: Content found at '{content.GetPath()}'.");
        GD.Print($"UiWindowManager: Content size = {GetControlSize(content)}.");
        GD.Print($"UiWindowManager: Initial state = {currentState}.");
        CallDeferred(nameof(ValidateWindowPresentation));
    }

    private void ValidateWindowPresentation()
    {
        Node content = GetWindowContent();
        if (content == null)
            return;

        foreach (Node child in content.GetChildren())
        {
            if (child is not CanvasItem canvasItem)
                continue;

            canvasItem.ZIndex = WindowZIndex;
            canvasItem.ZAsRelative = false;

            if (child is Control control && (control.Size.X <= 0.0f || control.Size.Y <= 0.0f))
            {
                control.Position = Vector2.Zero;
                control.Size = GetControlSize(content);
            }
        }

        SetWindowBackgroundZIndex();
    }

    public void ToggleWindow(string windowName)
    {
        if (string.IsNullOrWhiteSpace(windowName))
            return;

        Node window = GetWindowByName(windowName);
        if (window != null)
            ToggleWindow(window);
    }

    public void ToggleWindow(Node window)
    {
        if (window == null)
            return;

        if (activeWindow == window)
            CloseWindow(window);
        else
            OpenWindow(window);
    }

    public void OpenWindow(string windowName)
    {
        if (string.IsNullOrWhiteSpace(windowName))
            return;

        Node window = GetWindowByName(windowName);
        if (window != null)
            OpenWindow(window);
    }

    public void OpenWindow(Node window)
    {
        if (window == null || activeWindow == window)
            return;

        Node content = GetWindowContent();
        if (content == null)
            return;

        if (content is CanvasItem contentCanvas)
            contentCanvas.Visible = true;

        if (activeWindow != null)
            SetWindowVisible(activeWindow, false);

        SetWindowBackgroundVisible(true);

        if (window is Control windowControl &&
            (windowControl.Size.X <= 0.0f || windowControl.Size.Y <= 0.0f))
        {
            windowControl.Position = Vector2.Zero;
            windowControl.Size = GetControlSize(content);
        }

        SetWindowVisible(window, true);

        activeWindow = window;
        currentState = GetStateForWindow(window);

        GD.Print($"UiWindowManager: Opened '{window.Name}'.");
        GD.Print($"UiWindowManager: State = {currentState}.");
        GD.Print(
            $"UiWindowManager: Window visible={IsWindowVisible(window)} " +
            $"size={GetControlSize(window)} global_rect={GetGlobalRect(window)} " +
            $"z={GetZIndex(window)}.");
    }

    public void CloseWindow(string windowName)
    {
        if (string.IsNullOrWhiteSpace(windowName))
            return;

        Node window = GetWindowByName(windowName);
        if (window != null)
            CloseWindow(window);
    }

    public void CloseWindow(Node window)
    {
        if (window == null)
            return;

        SetWindowVisible(window, false);

        if (activeWindow == window)
        {
            activeWindow = null;
            currentState = UiState.NoWindow;
            SetWindowBackgroundVisible(false);
            GD.Print($"UiWindowManager: Closed '{window.Name}'.");
            GD.Print($"UiWindowManager: State = {currentState}.");
        }
    }

    public void CloseActiveWindow()
    {
        if (activeWindow != null)
            CloseWindow(activeWindow);
    }

    public Node GetActiveWindow() => activeWindow;
    public UiState GetCurrentState() => currentState;
    public bool IsNoWindowOpen() => currentState == UiState.NoWindow;
    public bool IsStatisticsOpen() => currentState == UiState.Statistics;
    public bool IsSettingsOpen() => currentState == UiState.Settings;
    public bool HasOpenWindow() => activeWindow != null;
    public bool IsWindowOpen(Node window) => window != null && activeWindow == window;

    public bool IsWindowOpen(string windowName)
    {
        if (string.IsNullOrWhiteSpace(windowName))
            return false;

        Node window = GetWindowByName(windowName);
        return window != null && activeWindow == window;
    }

    public void ToggleStatistics() => ToggleWindow(StatisticsWindowName);
    public void OpenStatistics() => OpenWindow(StatisticsWindowName);
    public void CloseStatistics() => CloseWindow(StatisticsWindowName);
    public void ToggleSettings() => ToggleWindow(SettingsWindowName);
    public void OpenSettings() => OpenWindow(SettingsWindowName);
    public void CloseSettings() => CloseWindow(SettingsWindowName);

    public void CloseAllWindows()
    {
        Node content = GetWindowContent();

        if (content == null)
        {
            SetWindowBackgroundVisible(false);
            activeWindow = null;
            currentState = UiState.NoWindow;
            return;
        }

        if (content is CanvasItem contentCanvas)
            contentCanvas.Visible = true;

        foreach (Node child in content.GetChildren())
        {
            if (child is CanvasItem canvasItem)
            {
                canvasItem.Visible = false;
                canvasItem.ZIndex = WindowZIndex;
                canvasItem.ZAsRelative = false;
                GD.Print($"UiWindowManager: Closed startup window '{child.Name}'.");
            }
        }

        activeWindow = null;
        currentState = UiState.NoWindow;
        SetWindowBackgroundVisible(false);
        SetWindowBackgroundZIndex();
    }

    private UiState GetStateForWindow(Node window)
    {
        if (window == null)
            return UiState.NoWindow;

        return window.Name.ToString() switch
        {
            StatisticsWindowName => UiState.Statistics,
            SettingsWindowName => UiState.Settings,
            _ => UiState.NoWindow
        };
    }

    private Node GetWindowByName(string windowName)
    {
        Node content = GetWindowContent();

        if (content == null)
        {
            GD.PushError("UiWindowManager: Could not find Content container.");
            return null;
        }

        Node window = content.GetNodeOrNull(windowName);
        if (window == null)
        {
            GD.PushError($"UiWindowManager: Could not find window '{windowName}' inside '{content.GetPath()}'.");
            return null;
        }

        return window;
    }

    private Node GetWindowContent() => GetNodeOrNull("../CenterUI/PanelContainer/MarginContainer/Content");

    private Node GetWindowBackground() => GetNodeOrNull("../CenterUI/PanelContainer/WindowBackground");

    private void SetWindowBackgroundZIndex()
    {
        Node background = GetWindowBackground();
        if (background is CanvasItem canvasItem)
        {
            canvasItem.ZIndex = WindowBackgroundZIndex;
            canvasItem.ZAsRelative = false;
        }
    }

    private void SetWindowBackgroundVisible(bool visible)
    {
        Node background = GetWindowBackground();
        if (background is CanvasItem canvasItem)
        {
            canvasItem.ZIndex = WindowBackgroundZIndex;
            canvasItem.ZAsRelative = false;
            canvasItem.Visible = visible;
        }
        else if (background == null)
        {
            GD.PushWarning("UiWindowManager: Could not find CenterUI/PanelContainer/WindowBackground.");
        }
    }

    private void SetWindowVisible(Node window, bool visible)
    {
        if (window is CanvasItem canvasItem)
        {
            canvasItem.ZIndex = WindowZIndex;
            canvasItem.ZAsRelative = false;
            canvasItem.Visible = visible;
        }
    }

    private static Vector2 GetControlSize(Node node) => node is Control control ? control.Size : Vector2.Zero;
    private static Rect2 GetGlobalRect(Node node) => node is Control control ? control.GetGlobalRect() : new Rect2();
    private static int GetZIndex(Node node) => node is CanvasItem canvasItem ? canvasItem.ZIndex : 0;
    private static bool IsWindowVisible(Node node) => node is CanvasItem canvasItem && canvasItem.Visible;
}
