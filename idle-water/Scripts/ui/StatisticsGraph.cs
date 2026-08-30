using Godot;

/// <summary>
/// Compatibility controller for FluidSimulator's existing statistics API.
/// The actual rendering now lives in Graph1, Graph2 and Graph3.
/// </summary>
public partial class StatisticsGraph : Control
{
    private Graph1 graph1;
    private Graph2 graph2;
    private Graph3 graph3;

    private void ResolveGraphs()
    {
        if (graph1 != null && graph2 != null && graph3 != null)
            return;

        graph1 = GetNodeOrNull<Graph1>("VBoxContainer/Graph1");
        graph2 = GetNodeOrNull<Graph2>("VBoxContainer/Graph2");
        graph3 = GetNodeOrNull<Graph3>("VBoxContainer/Graph3");
    }

    public void AddSample(int activeParticles, double energyPerSecond, float fps, float delta)
    {
        ResolveGraphs();
        graph1?.AddSample(activeParticles, energyPerSecond, fps);
    }

    public void AddRainEnergySample(float averageRain, float averageEnergy, float averageParticles)
    {
        ResolveGraphs();
        // The old API supplied one statistical point every 10 seconds.
        // Graph2 now stores that point as particles vs energy/second.
        graph2?.AddSample(averageParticles, averageEnergy);
    }
}
