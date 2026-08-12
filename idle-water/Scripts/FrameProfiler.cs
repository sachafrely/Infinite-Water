using Godot;
using System;
using System.Diagnostics;

public partial class FrameProfiler : Node
{
	// ============================================================
	// Configuration
	// ============================================================

	// How often a profiler report is printed.
	private const int ReportIntervalFrames = 120;

	// Number of frames retained for percentile calculations.
	private const int SampleCount = 120;

	// ============================================================
	// Frame timing
	// ============================================================

	private readonly double[] frameTimes = new double[SampleCount];

	private int sampleIndex = 0;
	private int samplesCollected = 0;
	private int frameCounter = 0;

	private long frameStartTicks;

	// ============================================================
	// Statistics
	// ============================================================

	private double totalFrameMs = 0.0;
	private double minFrameMs = double.MaxValue;
	private double maxFrameMs = 0.0;

	private double processMs = 0.0;
	private double physicsMs = 0.0;

	// ============================================================
	// Godot lifecycle
	// ============================================================

	public override void _Ready()
	{
		frameStartTicks = Stopwatch.GetTimestamp();

		GD.Print(
			"[FRAME PROFILER] Started. " +
			$"Reporting every {ReportIntervalFrames} frames."
		);
	}

	public override void _Process(double delta)
	{
		long now = Stopwatch.GetTimestamp();

		double frameMs =
			(double)(
				now -
				frameStartTicks
			) *
			1000.0 /
			Stopwatch.Frequency;

		frameStartTicks = now;

		RecordFrame(frameMs);

		frameCounter++;

		if (
			frameCounter >=
			ReportIntervalFrames)
		{
			frameCounter = 0;

			PrintReport();
		}
	}

	// ============================================================
	// Record frame
	// ============================================================

	private void RecordFrame(
		double frameMs)
	{
		frameTimes[sampleIndex] =
			frameMs;

		sampleIndex++;

		if (
			sampleIndex >=
			SampleCount)
		{
			sampleIndex = 0;
		}

		if (
			samplesCollected <
			SampleCount)
		{
			samplesCollected++;
		}

		totalFrameMs +=
			frameMs;

		if (
			frameMs <
			minFrameMs)
		{
			minFrameMs =
				frameMs;
		}

		if (
			frameMs >
			maxFrameMs)
		{
			maxFrameMs =
				frameMs;
		}
	}

	// ============================================================
	// Report
	// ============================================================

	private void PrintReport()
	{
		if (
			samplesCollected <= 0)
		{
			return;
		}

		double averageMs =
			totalFrameMs /
			samplesCollected;

		double p50 =
			CalculatePercentile(
				0.50
			);

		double p95 =
			CalculatePercentile(
				0.95
			);

		double p99 =
			CalculatePercentile(
				0.99
			);

		double fps =
			averageMs > 0.0
				? 1000.0 / averageMs
				: 0.0;

		double worstFps =
			maxFrameMs > 0.0
				? 1000.0 / maxFrameMs
				: 0.0;

		double processTime =
			GetPerformanceMonitor(
				Performance.Monitor.TimeProcess
			);

		double physicsTime =
			GetPerformanceMonitor(
				Performance.Monitor.TimePhysicsProcess
			);

		double navigationTime =
			GetPerformanceMonitor(
				Performance.Monitor.TimeNavigationProcess
			);

		long drawCalls =
			GetPerformanceMonitorLong(
				Performance.Monitor.RenderTotalDrawCallsInFrame
			);

		long primitives =
			GetPerformanceMonitorLong(
				Performance.Monitor.RenderTotalPrimitivesInFrame
			);

		long objects =
			GetPerformanceMonitorLong(
				Performance.Monitor.ObjectCount
			);

		GD.Print(
			"\n" +
			"============================================================\n" +
			"[FRAME PROFILER]\n" +
			"============================================================\n" +
			$"Samples:        {samplesCollected}\n" +
			$"Average:        {averageMs:F3} ms ({fps:F2} FPS)\n" +
			$"P50:            {p50:F3} ms\n" +
			$"P95:            {p95:F3} ms\n" +
			$"P99:            {p99:F3} ms\n" +
			$"Min:            {minFrameMs:F3} ms\n" +
			$"Max:            {maxFrameMs:F3} ms ({worstFps:F2} FPS)\n" +
			"------------------------------------------------------------\n" +
			$"Godot Process:  {processTime:F3} ms\n" +
			$"Godot Physics:  {physicsTime:F3} ms\n" +
			$"Navigation:     {navigationTime:F3} ms\n" +
			"------------------------------------------------------------\n" +
			$"Draw Calls:     {drawCalls}\n" +
			$"Primitives:     {primitives}\n" +
			$"Objects:        {objects}\n" +
            "============================================================"
		);

		ResetStatistics();
	}

	// ============================================================
	// Percentile
	// ============================================================

	private double CalculatePercentile(
		double percentile)
	{
		if (
			samplesCollected <= 0)
		{
			return 0.0;
		}

		double[] values =
			new double[samplesCollected];

		for (
			int i = 0;
			i < samplesCollected;
			i++)
		{
			values[i] =
				frameTimes[i];
		}

		Array.Sort(
			values
		);

		double position =
			percentile *
			(values.Length - 1);

		int lower =
			(int)Math.Floor(
				position
			);

		int upper =
			(int)Math.Ceiling(
				position
			);

		if (
			lower ==
			upper)
		{
			return values[lower];
		}

		double fraction =
			position -
			lower;

		return
			values[lower] +
			(
				values[upper] -
				values[lower]
			) *
			fraction;
	}

	// ============================================================
	// Performance monitor
	// ============================================================

	private static double GetPerformanceMonitor(
		Performance.Monitor monitor)
	{
		try
		{
			return Performance.GetMonitor(
				monitor
			);
		}
		catch
		{
			return 0.0;
		}
	}

	// ============================================================
	// Performance monitor - integer
	// ============================================================

	private static long GetPerformanceMonitorLong(
		Performance.Monitor monitor)
	{
		try
		{
			return Convert.ToInt64(
				Performance.GetMonitor(
					monitor
				)
			);
		}
		catch
		{
			return 0;
		}
	}

	// ============================================================
	// Reset
	// ============================================================

	private void ResetStatistics()
	{
		totalFrameMs = 0.0;

		minFrameMs =
			double.MaxValue;

		maxFrameMs = 0.0;
	}
}
