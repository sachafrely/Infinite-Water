using Godot;

/// <summary>
/// Central manager for all UI windows.
///
/// Windows live inside:
/// Main/CenterUI/PanelContainer/MarginContainer/Content
///
/// Buttons live separately inside BottomUI.
///
/// UiToggleButton supplies a NodePath/string identifying the window.
/// This manager handles opening, closing, and ensuring that only one
/// window is open at a time.
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
	}


	// ============================================================
	// Generic window API
	// ============================================================

	/// <summary>
	/// Toggles a window using its NodePath.
	///
	/// Example:
	/// ToggleWindow("../CenterUI/PanelContainer/MarginContainer/Content/StatisticsWindow");
	/// </summary>
	public void ToggleWindow(string windowPath)
	{
		if (string.IsNullOrEmpty(windowPath))
		{
			GD.PushWarning(
				"UiWindowManager: ToggleWindow received an empty path."
			);

			return;
		}

		Node window = GetWindowFromPath(windowPath);

		if (window == null)
			return;

		ToggleWindow(window);
	}


	/// <summary>
	/// Toggles a window using a Node reference.
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

		// If this is already the active window,
		// clicking its button closes it.
		if (activeWindow == window)
		{
			CloseWindow(window);
			return;
		}

		// Otherwise open it.
		OpenWindow(window);
	}


	/// <summary>
	/// Opens a window using a NodePath.
	/// </summary>
	public void OpenWindow(string windowPath)
	{
		if (string.IsNullOrEmpty(windowPath))
			return;

		Node window = GetWindowFromPath(windowPath);

		if (window == null)
			return;

		OpenWindow(window);
	}


	/// <summary>
	/// Opens a window.
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

		// Close the previous window.
		if (activeWindow != null)
		{
			CloseWindow(activeWindow);
		}

		SetWindowVisible(window, true);

		activeWindow = window;

		GD.Print(
			$"UiWindowManager: Opened '{window.Name}'."
		);
	}


	/// <summary>
	/// Closes a window using a NodePath.
	/// </summary>
	public void CloseWindow(string windowPath)
	{
		if (string.IsNullOrEmpty(windowPath))
			return;

		Node window = GetWindowFromPath(windowPath);

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
		}

		GD.Print(
			$"UiWindowManager: Closed '{window.Name}'."
		);
	}


	// ============================================================
	// Active window
	// ============================================================

	/// <summary>
	/// Closes whichever window is currently open.
	/// </summary>
	public void CloseActiveWindow()
	{
		if (activeWindow == null)
			return;

		CloseWindow(activeWindow);
	}


	/// <summary>
	/// Closes every window currently registered in the scene.
	///
	/// This intentionally searches the Content container instead
	/// of maintaining a hardcoded list, so future windows can be
	/// added without changing this method.
	/// </summary>
	public void CloseAllWindows()
	{
		Node content = GetWindowContent();

		if (content == null)
			return;

		foreach (Node child in content.GetChildren())
		{
			if (child is CanvasItem canvasItem)
			{
				canvasItem.Visible = false;
			}
		}

		activeWindow = null;
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


	public bool IsWindowOpen(string windowPath)
	{
		Node window = GetWindowFromPath(windowPath);

		return window != null && activeWindow == window;
	}


	// ============================================================
	// Window lookup
	// ============================================================

	private Node GetWindowFromPath(string windowPath)
	{
		Node window = GetNodeOrNull(windowPath);

		if (window == null)
		{
			GD.PushError(
				$"UiWindowManager: Could not find window at path '{windowPath}'."
			);
		}

		return window;
	}


	private Node GetWindowContent()
	{
		return GetNodeOrNull(
			"../CenterUI/PanelContainer/MarginContainer/Content"
		);
	}


	// ============================================================
	// Visibility
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
