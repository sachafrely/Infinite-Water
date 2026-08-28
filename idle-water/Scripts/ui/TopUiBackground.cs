// This script initiates the background function
// from "res://Scripts/ui/global/RenderedWindowBackground.cs".

using Godot;

public partial class TopUiBackground : Control
{
	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		QueueRedraw();
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized)
			QueueRedraw();
	}

	public override void _Draw()
	{
		RenderedWindowBackground.Draw(this);
	}
}
