using Godot;
using System.Collections.Generic;

public partial class Graph3 : Control
{
    private readonly List<Sample> samples = new List<Sample>(120);
    private const int MaxSamples = 120;
    private const float MarginLeft = 55.0f;
    private const float MarginRight = 35.0f;
    private const float MarginTop = 35.0f;
    private const float MarginBottom = 60.0f;

    private float maxParticles = 100.0f;
    private float maxEnergy = 0.001f;

    private struct Sample
    {
        public float AverageRain;
        public float AverageEnergy;
        public float AverageParticles;

        public Sample(float averageRain, float averageEnergy, float averageParticles)
        {
            AverageRain = averageRain;
            AverageEnergy = averageEnergy;
            AverageParticles = averageParticles;
        }
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        QueueRedraw();
    }

    public void AddSample(float averageRain, float averageEnergy, float averageParticles)
    {
        samples.Add(new Sample(averageRain, averageEnergy, averageParticles));
        while (samples.Count > MaxSamples)
            samples.RemoveAt(0);

        maxParticles = 1.0f;
        maxEnergy = 0.001f;
        foreach (Sample sample in samples)
        {
            maxParticles = Mathf.Max(maxParticles, sample.AverageParticles);
            maxEnergy = Mathf.Max(maxEnergy, sample.AverageEnergy);
        }

        maxParticles = Mathf.Max(maxParticles * 1.15f, 1.0f);
        maxEnergy = Mathf.Max(maxEnergy * 1.15f, 0.001f);
        QueueRedraw();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
            QueueRedraw();
    }

    public override void _Draw()
    {
        Rect2 rect = GetGraphRect();
        if (rect.Size.X <= 0.0f || rect.Size.Y <= 0.0f)
            return;

        DrawRect(rect, new Color(0.04f, 0.04f, 0.05f, 0.92f), true);
        DrawGrid(rect);
        DrawRect(rect, UiSettings.BorderColor, false, UiSettings.BorderSize);
        DrawString(ThemeDB.FallbackFont, new Vector2(rect.Position.X, rect.Position.Y - 7.0f), "Energy Per Particle", HorizontalAlignment.Left, -1, UiSettings.FontSizeBig, UiSettings.FontColorEnabled);
        DrawString(ThemeDB.FallbackFont, new Vector2(rect.Position.X - 50.0f, rect.Position.Y + rect.Size.Y * 0.5f + 7.0f), "Energy", HorizontalAlignment.Right, 45.0f, UiSettings.FontSizeSmall, UiSettings.FontColorEnabled);

        if (samples.Count == 0)
        {
            DrawString(ThemeDB.FallbackFont, new Vector2(rect.Position.X + rect.Size.X * 0.5f - 55.0f, rect.Position.Y + rect.Size.Y * 0.5f), "Collecting data...", HorizontalAlignment.Left, -1, UiSettings.FontSizeMedium, new Color(0.55f, 0.55f, 0.55f));
            return;
        }

        foreach (Sample sample in samples)
        {
            float x = rect.Position.X + Mathf.Clamp(sample.AverageParticles / maxParticles, 0.0f, 1.0f) * rect.Size.X;
            float y = rect.End.Y - Mathf.Clamp(sample.AverageEnergy / maxEnergy, 0.0f, 1.0f) * rect.Size.Y;
            DrawRect(new Rect2(new Vector2(x - 2.0f, y - 2.0f), new Vector2(4.0f, 4.0f)), new Color(1.0f, 1.0f, 1.0f), true);
        }

        DrawString(ThemeDB.FallbackFont, new Vector2(rect.Position.X, rect.End.Y + 24.0f), "0", HorizontalAlignment.Left, -1, UiSettings.FontSizeSmall, new Color(0.65f, 0.65f, 0.65f));
        DrawString(ThemeDB.FallbackFont, new Vector2(rect.End.X - 30.0f, rect.End.Y + 24.0f), FormatParticleCount(maxParticles), HorizontalAlignment.Left, -1, UiSettings.FontSizeSmall, new Color(0.65f, 0.65f, 0.65f));
    }

    private Rect2 GetGraphRect()
    {
        return new Rect2(MarginLeft, MarginTop, Mathf.Max(Size.X - MarginLeft - MarginRight, 100.0f), Mathf.Max(Size.Y - MarginTop - MarginBottom, 80.0f));
    }

    private void DrawGrid(Rect2 rect)
    {
        Color gridColor = new Color(0.18f, 0.18f, 0.20f, 0.8f);
        for (int i = 1; i < 5; i++)
        {
            float y = rect.Position.Y + rect.Size.Y * i / 5.0f;
            DrawLine(new Vector2(rect.Position.X, y), new Vector2(rect.End.X, y), gridColor, 1.0f);
            float x = rect.Position.X + rect.Size.X * i / 5.0f;
            DrawLine(new Vector2(x, rect.Position.Y), new Vector2(x, rect.End.Y), gridColor, 1.0f);
        }
    }

    private string FormatParticleCount(float particles)
    {
        int rounded = Mathf.RoundToInt(particles);
        if (rounded >= 1000)
        {
            float thousands = rounded / 1000.0f;
            return thousands >= 10.0f ? thousands.ToString("F0") + "k" : thousands.ToString("F1") + "k";
        }
        return rounded.ToString();
    }

    public int SampleCount => samples.Count;
}
