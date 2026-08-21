using Godot;

public partial class SellEnergyButton : Control
{
	private bool isPressed;
	private EnergySystem energySystem;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Stop;
		energySystem = EnergySystem.Instance;
		QueueRedraw();
	}

	public override void _GuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseButton &&
			mouseButton.ButtonIndex == MouseButton.Left)
		{
			if (mouseButton.Pressed)
			{
				isPressed = true;
				QueueRedraw();
				GetViewport().SetInputAsHandled();
			}
			else if (isPressed)
			{
				isPressed = false;
				QueueRedraw();
				SellAllAvailableChunks();
				GetViewport().SetInputAsHandled();
			}
		}
	}

	public override void _Draw()
	{
		Color background = isPressed
			? new Color(0.72f, 0.72f, 0.72f, 1.0f)
			: UiSettings.ButtonColor;

		DrawRect(new Rect2(Vector2.Zero, Size), background, true);
		DrawRect(new Rect2(Vector2.Zero, Size), UiSettings.BorderColor, false, UiSettings.BorderSize);

		Font font = ThemeDB.FallbackFont;
		int fontSize = UiSettings.FontSizeMedium;
		string text = "Sell Energy";

		Vector2 textSize = font.GetStringSize(text, HorizontalAlignment.Left, -1, fontSize);
		Vector2 textPosition = new Vector2(
			(Size.X - textSize.X) * 0.5f,
			(Size.Y + textSize.Y * 0.5f) * 0.5f
		);

		DrawString(
			font,
			textPosition,
			text,
			HorizontalAlignment.Left,
			-1,
			fontSize,
			UiSettings.FontColorBasic
		);
	}

	private void SellAllAvailableChunks()
	{
		if (energySystem == null)
			energySystem = EnergySystem.Instance;

		if (energySystem == null)
			return;

		energySystem.SellAllAvailableEnergy();
	}
}
