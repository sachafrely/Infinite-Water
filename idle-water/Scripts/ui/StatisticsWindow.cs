using Godot;

public partial class StatisticsWindow : Control
{
	public override void _Ready()
	{
		// The window itself should not block clicks.
		MouseFilter = Control.MouseFilterEnum.Ignore;
	}

	public void Open()
	{
		Show();
	}

	public void Close()
	{
		Hide();
	}

	public bool IsOpen()
	{
		return Visible;
	}
}
