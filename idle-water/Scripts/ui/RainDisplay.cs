using System.Reflection;
using Godot;

/// <summary>
/// Displays the current rain amount as ten small pixel-style bars.
/// Each bar represents 10% rain.
/// </summary>
public partial class RainDisplay : Control
{
	private const int SegmentCount = 10;
	private const float SegmentWidth = 21.0f;
	private const float SegmentHeight = 30.0f;
	private const float SegmentGap = 4.5f;

	private FieldInfo rainSystemField;
	private PropertyInfo rainPercentProperty;
	private float rainPercent;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		CacheRainAccess();
		QueueRedraw();
	}

	public override void _Process(double delta)
	{
		rainPercent = GetCurrentRainPercent();
		QueueRedraw();
	}

	public override void _Draw()
	{
		int activeSegments = Mathf.RoundToInt(rainPercent / 10.0f);
		float totalWidth = SegmentCount * SegmentWidth + (SegmentCount - 1) * SegmentGap;
		float startX = Mathf.Max(0.0f, (Size.X - totalWidth) * 0.5f);
		float startY = Mathf.Max(0.0f, (Size.Y - SegmentHeight) * 0.5f);

		for (int i = 0; i < SegmentCount; i++)
		{
			bool active = i < activeSegments;
			Color fill = active
				? UiSettings.FontColorWater
				: new Color(0.30f, 0.30f, 0.30f, 1.0f);

			Rect2 rect = new Rect2(
				startX + i * (SegmentWidth + SegmentGap),
				startY,
				SegmentWidth,
				SegmentHeight
			);

			DrawRect(rect, fill, true);
			DrawRect(rect, UiSettings.BorderColor, false, 1.0f);
		}
	}

	private void CacheRainAccess()
	{
		try
		{
			rainSystemField = typeof(FluidSimulator).GetField(
				"rainSystem",
				BindingFlags.Instance | BindingFlags.NonPublic
			);

			if (rainSystemField != null)
				rainPercentProperty = rainSystemField.FieldType.GetProperty("CurrentRainPercent");
		}
		catch
		{
			rainSystemField = null;
			rainPercentProperty = null;
		}
	}

	private FluidSimulator FindSimulator()
	{
		return GetTree().CurrentScene?.FindChild("FluidSimulation", true, false) as FluidSimulator;
	}

	private float GetCurrentRainPercent()
	{
		FluidSimulator simulator = FindSimulator();
		if (simulator == null || rainSystemField == null || rainPercentProperty == null)
			return 0.0f;

		try
		{
			object rainSystem = rainSystemField.GetValue(simulator);
			object value = rainSystem == null ? null : rainPercentProperty.GetValue(rainSystem);
			return value is float percent ? Mathf.Clamp(percent, 0.0f, 100.0f) : 0.0f;
		}
		catch
		{
			return 0.0f;
		}
	}
}
