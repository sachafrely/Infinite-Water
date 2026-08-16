using Godot;

/// <summary>
/// Central manager for all UI windows.
///
/// Windows live inside:
/// Main/CenterUI/PanelContainer/MarginContainer/Content
///
/// Buttons live separately inside BottomUI.
///
/// Example:
///
/// StatisticsButton -> "StatisticsWindow"
/// SettingsButton   -> "SettingsWindow"
///
/// Only one window can be open at a time.
///
/// The shared WindowBackground is shown while a window is open
/// and hidden when no window is open.
///
/// The manager is also the single source of truth for the
/// current UI window state.
/// </summary>
public partial class UiWindowManager : Node
{
	// ============================================================
	// UI STATE
	// ============================================================

	/// <summary>
	/// Represents the currently active UI state.
	///
	/// Keep this enum small and explicit.
	/// Additional windows can be added later.
	/// </summary>
	public enum UiState
	{
		NoWindow,
		Statistics,
		Settings
	}


	// ============================================================
	// State
	// ============================================================

	private Node activeWindow;

	private UiState currentState = UiState.NoWindow;


	// ============================================================
	// Constants
	// ============================================================

	private const string StatisticsWindowName = "StatisticsWindow";
	private const string SettingsWindowName = "SettingsWindow";


	// ============================================================
	// Godot
	// ============================================================

	public override void _Ready()
	{
		CloseAllWindows();

		GD.Print("========== UI WINDOW MANAGER READY ==========");

		Node content = GetWindowContent();

		if (content != null)
		{
			GD.Print(
				$"UiWindowManager: Content found at '{content.GetPath()}'."
			);
		}
		else
		{
			GD.PushError(
				"UiWindowManager: Could not find window Content container."
			);
		}

		GD.Print(
			$"UiWindowManager: Initial state = {currentState}."
		);
	}


	// ============================================================
	// Toggle
	// ============================================================

	/// <summary>
	/// Toggles a window by its name.
	///
	/// The name is resolved inside:
	/// Main/CenterUI/PanelContainer/MarginContainer/Content
	///
	/// Example:
	/// ToggleWindow("StatisticsWindow");
	/// </summary>
	public void ToggleWindow(string windowName)
	{
		if (string.IsNullOrWhiteSpace(windowName))
		{
			GD.PushWarning(
				"UiWindowManager: ToggleWindow received an empty window name."
			);

			return;
		}

		Node window = GetWindowByName(windowName);

		if (window == null)
			return;

		ToggleWindow(window);
	}


	/// <summary>
	/// Toggles a specific window.
	/// </summary>
	public void ToggleWindow(Node window)
	{
		if (window == null)
		{
			GD.PushWarning(
				"UiWindowManager: ToggleWindow received a null window."
			);

			return;
		}

		// Clicking the button of the currently open window
		// closes it.
		if (activeWindow == window)
		{
			CloseWindow(window);
			return;
		}

		// Otherwise open the requested window.
		OpenWindow(window);
	}


	// ============================================================
	// Open
	// ============================================================

	/// <summary>
	/// Opens a window by its name.
	/// </summary>
	public void OpenWindow(string windowName)
	{
		if (string.IsNullOrWhiteSpace(windowName))
			return;

		Node window = GetWindowByName(windowName);

		if (window == null)
			return;

		OpenWindow(window);
	}


	/// <summary>
	/// Opens a specific window.
	///
	/// Any currently open window is closed first.
	/// </summary>
	public void OpenWindow(Node window)
	{
		if (window == null)
		{
			GD.PushWarning(
				"UiWindowManager: OpenWindow received a null window."
			);

			return;
		}

		// Already open.
		if (activeWindow == window)
			return;

		// Make sure the requested window can be represented
		// by our UI state system.
		UiState newState = GetStateForWindow(window);

		// Close the currently active window.
		if (activeWindow != null)
		{
			SetWindowVisible(activeWindow, false);
		}

		// Show the shared background.
		SetWindowBackgroundVisible(true);

		// Show the requested window.
		SetWindowVisible(window, true);

		// Update state.
		activeWindow = window;
		currentState = newState;

		GD.Print(
			$"UiWindowManager: Opened '{window.Name}'."
		);

		GD.Print(
			$"UiWindowManager: State = {currentState}."
		);
	}


	// ============================================================
	// Close
	// ============================================================

	/// <summary>
	/// Closes a window by its name.
	/// </summary>
	public void CloseWindow(string windowName)
	{
		if (string.IsNullOrWhiteSpace(windowName))
			return;

		Node window = GetWindowByName(windowName);

		if (window == null)
			return;

		CloseWindow(window);
	}


	/// <summary>
	/// Closes a specific window.
	/// </summary>
	public void CloseWindow(Node window)
	{
		if (window == null)
			return;

		SetWindowVisible(window, false);

		if (activeWindow == window)
		{
			activeWindow = null;
			currentState = UiState.NoWindow;

			// No window is open anymore.
			SetWindowBackgroundVisible(false);

			GD.Print(
				$"UiWindowManager: Closed '{window.Name}'."
			);

			GD.Print(
				$"UiWindowManager: State = {currentState}."
			);

			return;
		}

		GD.Print(
			$"UiWindowManager: Hid non-active window '{window.Name}'."
		);
	}


	// ============================================================
	// Active window
	// ============================================================

	/// <summary>
	/// Closes whichever window is currently active.
	/// </summary>
	public void CloseActiveWindow()
	{
		if (activeWindow == null)
			return;

		CloseWindow(activeWindow);
	}


	/// <summary>
	/// Returns the currently active window.
	///
	/// Returns null when no window is open.
	/// </summary>
	public Node GetActiveWindow()
	{
		return activeWindow;
	}


	/// <summary>
	/// Returns the current UI state.
	/// </summary>
	public UiState GetCurrentState()
	{
		return currentState;
	}


	/// <summary>
	/// Returns true if no UI window is currently open.
	/// </summary>
	public bool IsNoWindowOpen()
	{
		return currentState == UiState.NoWindow;
	}


	/// <summary>
	/// Returns true if the Statistics window is currently open.
	/// </summary>
	public bool IsStatisticsOpen()
	{
		return currentState == UiState.Statistics;
	}


	/// <summary>
	/// Returns true if the Settings window is currently open.
	/// </summary>
	public bool IsSettingsOpen()
	{
		return currentState == UiState.Settings;
	}


	// ============================================================
	// State queries
	// ============================================================

	/// <summary>
	/// Returns true when any UI window is open.
	/// </summary>
	public bool HasOpenWindow()
	{
		return activeWindow != null;
	}


	/// <summary>
	/// Returns true when the supplied window is active.
	/// </summary>
	public bool IsWindowOpen(Node window)
	{
		return window != null && activeWindow == window;
	}


	/// <summary>
	/// Returns true when the named window is active.
	/// </summary>
	public bool IsWindowOpen(string windowName)
	{
		if (string.IsNullOrWhiteSpace(windowName))
			return false;

		Node window = GetWindowByName(windowName);

		return window != null && activeWindow == window;
	}


	// ============================================================
	// Convenience methods
	// ============================================================

	/// <summary>
	/// Toggles the Statistics window.
	///
	/// This gives future UI code a clean API without having
	/// to know the internal node name.
	/// </summary>
	public void ToggleStatistics()
	{
		ToggleWindow(StatisticsWindowName);
	}


	/// <summary>
	/// Opens the Statistics window.
	/// </summary>
	public void OpenStatistics()
	{
		OpenWindow(StatisticsWindowName);
	}


	/// <summary>
	/// Closes the Statistics window.
	/// </summary>
	public void CloseStatistics()
	{
		CloseWindow(StatisticsWindowName);
	}


	/// <summary>
	/// Toggles the Settings window.
	/// </summary>
	public void ToggleSettings()
	{
		ToggleWindow(SettingsWindowName);
	}


	/// <summary>
	/// Opens the Settings window.
	/// </summary>
	public void OpenSettings()
	{
		OpenWindow(SettingsWindowName);
	}


	/// <summary>
	/// Closes the Settings window.
	/// </summary>
	public void CloseSettings()
	{
		CloseWindow(SettingsWindowName);
	}


	// ============================================================
	// Close all
	// ============================================================

	/// <summary>
	/// Closes all windows inside Content and hides the
	/// shared background.
	///
	/// This is also used during startup to guarantee a
	/// clean initial UI state.
	/// </summary>
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

		foreach (Node child in content.GetChildren())
		{
			if (child is CanvasItem canvasItem)
			{
				canvasItem.Visible = false;

				GD.Print(
					$"UiWindowManager: Closed startup window '{child.Name}'."
				);
			}
		}

		activeWindow = null;
		currentState = UiState.NoWindow;

		// No window is open at startup.
		SetWindowBackgroundVisible(false);
	}


	// ============================================================
	// Window state mapping
	// ============================================================

	/// <summary>
	/// Converts a window node into the corresponding UI state.
	///
	/// This keeps the rest of the application independent from
	/// the actual node names.
	/// </summary>
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
				GD.PushWarning(
					$"UiWindowManager: Window '{window.Name}' has no " +
					"explicit UiState mapping. Using NoWindow."
				);

				return UiState.NoWindow;
		}
	}


	// ============================================================
	// Window lookup
	// ============================================================

	/// <summary>
	/// Finds a window by name inside the Content container.
	///
	/// This is important:
	///
	/// UiWindowManager is located at:
	/// Main/UiWindowManager
	///
	/// while StatisticsWindow is located at:
	/// Main/CenterUI/PanelContainer/MarginContainer/Content/StatisticsWindow
	///
	/// Therefore "StatisticsWindow" cannot be resolved relative
	/// to UiWindowManager directly.
	/// </summary>
	private Node GetWindowByName(string windowName)
	{
		Node content = GetWindowContent();

		if (content == null)
		{
			GD.PushError(
				"UiWindowManager: Could not find Content container."
			);

			return null;
		}

		Node window = content.GetNodeOrNull(windowName);

		if (window == null)
		{
			GD.PushError(
				$"UiWindowManager: Could not find window " +
				$"'{windowName}' inside '{content.GetPath()}'."
			);

			return null;
		}

		return window;
	}


	/// <summary>
	/// Gets:
	///
	/// Main/CenterUI/PanelContainer/MarginContainer/Content
	/// </summary>
	private Node GetWindowContent()
	{
		return GetNodeOrNull(
			"../CenterUI/PanelContainer/MarginContainer/Content"
		);
	}


	// ============================================================
	// Shared window background
	// ============================================================

	/// <summary>
	/// Gets:
	///
	/// Main/CenterUI/PanelContainer/WindowBackground
	/// </summary>
	private Node GetWindowBackground()
	{
		return GetNodeOrNull(
			"../CenterUI/PanelContainer/WindowBackground"
		);
	}


	/// <summary>
	/// Shows or hides the shared window background.
	/// </summary>
	private void SetWindowBackgroundVisible(bool visible)
	{
		Node background = GetWindowBackground();

		if (background == null)
		{
			GD.PushWarning(
				"UiWindowManager: Could not find " +
				"CenterUI/PanelContainer/WindowBackground."
			);

			return;
		}

		if (background is CanvasItem canvasItem)
		{
			canvasItem.Visible = visible;
		}
		else
		{
			GD.PushWarning(
				"UiWindowManager: WindowBackground is not a CanvasItem."
			);
		}
	}


	// ============================================================
	// Window visibility
	// ============================================================

	/// <summary>
	/// Shows or hides a window.
	/// </summary>
	private void SetWindowVisible(Node window, bool visible)
	{
		if (window == null)
			return;

		if (window is CanvasItem canvasItem)
		{
			canvasItem.Visible = visible;
			return;
		}

		GD.PushWarning(
			$"UiWindowManager: '{window.Name}' is not a CanvasItem, " +
			"so its visibility cannot be controlled."
		);
	}
}
