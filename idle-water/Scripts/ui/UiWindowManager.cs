using Godot;

/// <summary>
/// Central manager for UI windows inside Main/Ui/CenterUi.
/// Only one CenterUi window can be open at a time.
/// </summary>
public partial class UiWindowManager : Node
{
	public enum UiState
	{
		NoWindow,
		Statistics,
		Settings
	}

	private Node activeWindow;
	private UiState currentState = UiState.NoWindow;

	private const string StatisticsWindowName = "StatisticsWindow";
	private const string SettingsWindowName = "SettingsWindow";

	public override void _Ready()
	{
		CloseAllWindows();
	}

	public void ToggleWindow(string windowName)
	{
		Node window = GetWindowByName(windowName);
		if (window == null)
			return;

		ToggleWindow(window);
	}

	public void ToggleWindow(Node window)
	{
		if (window == null)
			return;

		if (activeWindow == window)
		{
			CloseWindow(window);
			return;
		}

		OpenWindow(window);
	}

	public void OpenWindow(string windowName)
	{
		Node window = GetWindowByName(windowName);
		if (window == null)
			return;

		OpenWindow(window);
	}

	public void OpenWindow(Node window)
	{
		if (window == null || activeWindow == window)
			return;

		if (activeWindow != null)
			SetWindowVisible(activeWindow, false);

		SetWindowVisible(window, true);
		activeWindow = window;
		currentState = GetStateForWindow(window);
	}

	public void CloseWindow(string windowName)
	{
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
		Node centerUi = GetCenterUi();

		if (centerUi != null)
		{
			foreach (Node child in centerUi.GetChildren())
			{
				if (child is CanvasItem canvasItem)
					canvasItem.Visible = false;
			}
		}

		activeWindow = null;
		currentState = UiState.NoWindow;
	}

	private UiState GetStateForWindow(Node window)
	{
		if (window == null)
			return UiState.NoWindow;

		switch (window.Name.ToString())
		{
			case StatisticsWindowName:
				return UiState.Statistics;
			case SettingsWindowName:
				return UiState.Settings;
			default:
				return UiState.NoWindow;
		}
	}

	private Node GetWindowByName(string windowName)
	{
		Node centerUi = GetCenterUi();
		if (centerUi == null)
		{
			GD.PushError("UiWindowManager: Could not find CenterUi.");
			return null;
		}

		Node window = centerUi.GetNodeOrNull(windowName);
		if (window == null)
		{
			GD.PushError($"UiWindowManager: Could not find '{windowName}' inside '{centerUi.GetPath()}'.");
			return null;
		}

		return window;
	}

	private Node GetCenterUi()
	{
		return GetNodeOrNull("../CenterUi");
	}

	private void SetWindowVisible(Node window, bool visible)
	{
		if (window is CanvasItem canvasItem)
			canvasItem.Visible = visible;
	}
}
