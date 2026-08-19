using Godot;

public partial class UiToggleButton : Control
{
	// ============================================================
	// Inspector properties
	// ============================================================

	[Export]
	public string ButtonText { get; set; } = "Statistics";

	[Export]
	public string WindowName { get; set; } = "StatisticsWindow";

	// ============================================================
	// Internal
	// ============================================================

	private UiWindowManager windowManager;
	private Rect2 buttonRect;
	private bool isHovered = false;

	// ============================================================
	// Godot
	// ============================================================

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

		QueueRedraw();
	}

	public override void _GuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseButton)
		{
			if (mouseButton.ButtonIndex == MouseButton.Left &&
				mouseButton.Pressed)
			{
				ToggleWindow();
				GetViewport().SetInputAsHandled();
			}
		}
	}

	public override void _Notification(int what)
	{
		if (what == NotificationMouseEnter)
		{
			isHovered = true;
			QueueRedraw();
		}
		else if (what == NotificationMouseExit)
		{
			isHovered = false;
			QueueRedraw();
		}
	}

	public override void _Draw()
	{
		buttonRect = new Rect2(0, 0, Size.X, Size.Y);

		Color backgroundColor = isHovered
			? UiSettings.ButtonHoverColor
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

	// ============================================================
	// Window toggle
	// ============================================================

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

		windowManager.ToggleWindow(WindowName);
	}
}
