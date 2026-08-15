using Godot;

public partial class UiToggleButton : Control
{
	// ============================================================
	// Inspector properties
	// ============================================================

	[Export]
	public string ButtonText { get; set; } = "Statistics";

	[Export]
	public int FontSize { get; set; } = 24;

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
		// --------------------------------------------------------
		// Button rectangle
		// --------------------------------------------------------

		buttonRect = new Rect2(
			0,
			0,
			Size.X,
			Size.Y
		);

		// --------------------------------------------------------
		// Pixel-style button background
		// --------------------------------------------------------

		Color backgroundColor = isHovered
			? new Color(0.22f, 0.22f, 0.22f, 1.0f)
			: new Color(0.12f, 0.12f, 0.12f, 1.0f);

		Color borderColor = new Color(
			0.75f,
			0.75f,
			0.75f,
			1.0f
		);

		DrawRect(
			buttonRect,
			backgroundColor,
			true
		);

		DrawRect(
			buttonRect,
			borderColor,
			false,
			2.0f
		);

		// --------------------------------------------------------
		// Text
		// --------------------------------------------------------

		Font font = ThemeDB.FallbackFont;

		Vector2 textSize = font.GetStringSize(
			ButtonText,
			HorizontalAlignment.Left,
			-1,
			FontSize
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
			FontSize,
			new Color(1, 1, 1, 1)
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
