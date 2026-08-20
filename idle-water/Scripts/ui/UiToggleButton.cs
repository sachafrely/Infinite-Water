using Godot;

/// <summary>
/// Touch/mouse friendly button used to toggle one UI window through UiWindowManager.
///
/// Input is handled at the viewport level rather than relying only on _GuiInput.
/// This keeps the button clickable even when another overlay Control consumes GUI input.
/// </summary>
public partial class UiToggleButton : Control
{
	[Export]
	public string ButtonText { get; set; } = "Statistics";

	[Export]
	public string WindowName { get; set; } = "StatisticsWindow";

	private UiWindowManager windowManager;
	private Rect2 buttonRect;
	private bool wasWindowOpen = false;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Stop;

		windowManager = GetNodeOrNull<UiWindowManager>(
			"/root/Main/UiWindowManager"
		);

		if (windowManager == null)
		{
			GD.PushError(
				$"[UiToggleButton] Could not find UiWindowManager for {GetPath()}"
			);
		}

		GD.Print(
			$"[UiToggleButton] Ready: {GetPath()} -> '{ButtonText}' -> '{WindowName}'"
		);

		wasWindowOpen = IsAssociatedWindowOpen();
		QueueRedraw();
	}

	public override void _Process(double delta)
	{
		bool windowOpen = IsAssociatedWindowOpen();

		if (windowOpen != wasWindowOpen)
		{
			wasWindowOpen = windowOpen;
			QueueRedraw();
		}
	}

	/// <summary>
	/// Handles mouse and Android touch directly from the viewport.
	/// This deliberately does not depend on _GuiInput so another Control overlay
	/// cannot silently prevent the Statistics/Settings buttons from receiving input.
	/// </summary>
	public override void _Input(InputEvent @event)
	{
		if (!Visible || !IsInsideTree())
			return;

		if (@event is InputEventMouseButton mouseButton &&
			mouseButton.ButtonIndex == MouseButton.Left &&
			mouseButton.Pressed)
		{
			if (GetGlobalRect().HasPoint(mouseButton.Position))
			{
				ToggleWindow();
				GetViewport().SetInputAsHandled();
			}

			return;
		}

	if (@event is InputEventScreenTouch screenTouch && screenTouch.Pressed)
		{
			if (GetGlobalRect().HasPoint(screenTouch.Position))
			{
				ToggleWindow();
				GetViewport().SetInputAsHandled();
			}
		}
	}

	public override void _Draw()
	{
		buttonRect = new Rect2(0, 0, Size.X, Size.Y);

		Color backgroundColor = wasWindowOpen
			? UiSettings.WindowColor
			: UiSettings.ButtonColor;

		DrawRect(
			buttonRect,
			backgroundColor,
			true
		);

		DrawRect(
			buttonRect,
			UiSettings.BorderColor,
			false,
			UiSettings.BorderSize
		);

		Font font = ThemeDB.FallbackFont;
		int fontSize = UiSettings.FontSizeMedium;

		Vector2 textSize = font.GetStringSize(
			ButtonText,
			HorizontalAlignment.Left,
			-1,
			fontSize
		);

		Vector2 textPosition = new Vector2(
			(Size.X - textSize.X) * 0.5f,
			(Size.Y + textSize.Y * 0.5f) * 0.5f
		);

		DrawString(
			font,
			textPosition,
			ButtonText,
			HorizontalAlignment.Left,
			-1,
			fontSize,
			UiSettings.FontColorBasic
		);
	}

	private bool IsAssociatedWindowOpen()
	{
		if (windowManager == null)
			return false;

		return windowManager.IsWindowOpen(WindowName);
	}

	private void ToggleWindow()
	{
		if (windowManager == null)
		{
			windowManager = GetNodeOrNull<UiWindowManager>(
				"/root/Main/UiWindowManager"
			);
		}

		if (windowManager == null)
		{
			GD.PushError(
				$"[UiToggleButton] UiWindowManager not found. " +
				$"Cannot toggle '{WindowName}'."
			);

			return;
		}

		GD.Print(
			$"[UiToggleButton] Click -> toggling '{WindowName}'"
		);

		windowManager.ToggleWindow(WindowName);
	}
}
