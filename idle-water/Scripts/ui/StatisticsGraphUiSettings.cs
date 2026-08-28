// THIS FILE SHALL BE GONE SOON //

using Godot;

/// <summary>
/// Applies the central UI palette to StatisticsGraph.
/// </summary>
public partial class StatisticsGraph : Control
{
	public override void _EnterTree()
	{
		particlesColor = UiSettings.FontColorWater;
		energyColor = UiSettings.FontColorEnergy;
		fpsColor = UiSettings.FontColorFps;
	}
}
