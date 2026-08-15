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
/// </summary>
public partial class UiWindowManager : Node
{
	// ============================================================
	// State
	// ============================================================

	private Node activeWindow;


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
			return;

		// Already open.
		if (activeWindow == window)
			return;

		// Close the currently active window.
		if (activeWindow != null)
		{
			SetWindowVisible(activeWindow, false);
		}

		// Show the shared background.
		SetWindowBackgroundVisible(true);

		// Show the requested window.
		SetWindowVisible(window, true);

		activeWindow = window;

		GD.Print(
			$"UiWindowManager: Opened '{window.Name}'."
		);
	}


	// ============================================================
	// Close
	// ============================================================

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

			// No window is open anymore.
			SetWindowBackgroundVisible(false);
		}

		GD.Print(
			$"UiWindowManager: Closed '{window.Name}'."
		);
	}


	// ============================================================
	// Active window
	// ============================================================

	public void CloseActiveWindow()
	{
		if (activeWindow == null)
			return;

		CloseWindow(activeWindow);
	}


	// ============================================================
	// Close all
	// ============================================================

	/// <summary>
	/// Closes all windows inside Content and hides the
	/// shared background.
	/// </summary>
	public void CloseAllWindows()
	{
		Node content = GetWindowContent();

		if (content == null)
		{
			SetWindowBackgroundVisible(false);
			activeWindow = null;
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

		// No window is open at startup.
		SetWindowBackgroundVisible(false);
	}


	// ============================================================
	// State queries
	// ============================================================

	public bool HasOpenWindow()
	{
		return activeWindow != null;
	}


	public Node GetActiveWindow()
	{
		return activeWindow;
	}


	public bool IsWindowOpen(Node window)
	{
		return window != null && activeWindow == window;
	}


	public bool IsWindowOpen(string windowName)
	{
		Node window = GetWindowByName(windowName);

		return window != null && activeWindow == window;
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
