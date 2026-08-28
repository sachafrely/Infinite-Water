// THIS FILE SHALL BE GONE SOON //

using Godot;

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
		windowManager = GetNodeOrNull<UiWindowManager>("/root/Main/UiWindowManager");
		if (windowManager == null)
			GD.PushError($"[UiToggleButton] Could not find UiWindowManager for {GetPath()}");
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

	public override void _GuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseButton && mouseButton.ButtonIndex == MouseButton.Left && mouseButton.Pressed)
		{
			ToggleWindow();
			GetViewport().SetInputAsHandled();
		}
	}

	public override void _Draw()
	{
		buttonRect = new Rect2(0, 0, Size.X, Size.Y);
		Color backgroundColor = wasWindowOpen ? UiSettings.ButtonPressedColor : UiSettings.ButtonUnpressedColor;
		DrawRect(buttonRect, backgroundColor, true);
		DrawRect(buttonRect, UiSettings.BorderColor, false, UiSettings.BorderSize);
		Font font = ThemeDB.FallbackFont;
		int fontSize = UiSettings.FontSizeBig;
		Vector2 textSize = font.GetStringSize(ButtonText, HorizontalAlignment.Left, -1, fontSize);
		Vector2 textPosition = new Vector2((Size.X - textSize.X) * 0.5f, (Size.Y + textSize.Y * 0.5f) * 0.5f);
		DrawString(font, textPosition, ButtonText, HorizontalAlignment.Left, -1, fontSize, UiSettings.FontColorEnabled);
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
			windowManager = GetNodeOrNull<UiWindowManager>("/root/Main/UiWindowManager");
		if (windowManager == null)
		{
			GD.PushError($"[UiToggleButton] UiWindowManager not found. Cannot toggle '{WindowName}'.");
			return;
		}
		windowManager.ToggleWindow(WindowName);
	}
}
