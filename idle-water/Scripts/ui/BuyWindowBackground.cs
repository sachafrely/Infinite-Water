using Godot;

public partial class BuyWindowBackground : Control
{
	public override void _Draw()
	{
		RenderedWindowBackground.Draw(this);
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized)
			QueueRedraw();
	}
}
