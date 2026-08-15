using Godot;
using System;
using System.Diagnostics;

public partial class FrameProfiler : Node
{
	// ============================================================
	// Configuration
	// ============================================================

	private const int ReportIntervalFrames = 120;
	private const int SampleCount = 240;

	// Anything above this is considered a frame spike.
	private const double SpikeThresholdMs = 33.333;

	// Number of worst individual frames displayed.
	private const int WorstFrameCount = 10;

	// ============================================================
	// Frame samples
	// ============================================================

	private readonly double[] frameTimes =
		new double[SampleCount];

	private readonly long[] frameNumbers =
		new long[SampleCount];

	private int sampleIndex = 0;
	private int samplesCollected = 0;

	private long totalFrameCounter = 0;

	private long frameStartTicks;

	// ============================================================
	// Statistics
	// ============================================================

	private double totalFrameMs = 0.0;

	private double minFrameMs =
		double.MaxValue;

	private double maxFrameMs = 0.0;

	private long minFrameNumber = 0;
	private long maxFrameNumber = 0;

	private int spikeCount = 0;

	// ============================================================
	// Worst frame tracking
	// ============================================================

	private readonly double[] worstFrameTimes =
		new double[WorstFrameCount];

	private readonly long[] worstFrameNumbers =
		new long[WorstFrameCount];

	// ============================================================
	// Ready
	// ============================================================

	public override void _Ready()
	{
		frameStartTicks =
			Stopwatch.GetTimestamp();

		ClearWorstFrames();

		GD.Print(
			"\n" +
			"============================================================\n" +
			"[FRAME PROFILER] STARTED\n" +
			"============================================================\n" +
			$"Report interval:       {ReportIntervalFrames} frames\n" +
			$"Sample window:         {SampleCount} frames\n" +
			$"Spike threshold:       {SpikeThresholdMs:F3} ms\n" +
			$"Target 60 FPS frame:   16.667 ms\n" +
			$"Target 30 FPS frame:   33.333 ms\n" +
			$"Target 20 FPS frame:   50.000 ms\n" +
			$"Target 10 FPS frame:   100.000 ms\n" +
            "============================================================"
		);

		PrintHardwareInfo();
	}

	// ============================================================
	// Process
	// ============================================================

	public override void _Process(double delta)
	{
		long now =
			Stopwatch.GetTimestamp();

		double frameMs =
			(
				double)(
					now -
					frameStartTicks
				)
				*
				1000.0
				/
				Stopwatch.Frequency;

		frameStartTicks = now;

		totalFrameCounter++;

		RecordFrame(
			frameMs,
			totalFrameCounter
		);

		if (
			totalFrameCounter %
			ReportIntervalFrames ==
			0)
		{
			PrintReport();
		}
	}

	// ============================================================
	// Record frame
	// ============================================================

	private void RecordFrame(
		double frameMs,
		long frameNumber)
	{
		frameTimes[sampleIndex] =
			frameMs;

		frameNumbers[sampleIndex] =
			frameNumber;

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

			minFrameNumber =
				frameNumber;
		}

		if (
			frameMs >
			maxFrameMs)
		{
			maxFrameMs =
				frameMs;

			maxFrameNumber =
				frameNumber;
		}

		if (
			frameMs >=
			SpikeThresholdMs)
		{
			spikeCount++;
		}

		RecordWorstFrame(
			frameMs,
			frameNumber
		);
	}

	// ============================================================
	// Worst frame tracking
	// ============================================================

	private void RecordWorstFrame(
		double frameMs,
		long frameNumber)
	{
		int insertIndex = -1;

		double smallestWorst =
			double.MaxValue;

		int smallestIndex = -1;

		for (
			int i = 0;
			i < WorstFrameCount;
			i++)
		{
			if (
				worstFrameTimes[i] <=
				0.0)
			{
				insertIndex = i;
				break;
			}

			if (
				worstFrameTimes[i] <
				smallestWorst)
			{
				smallestWorst =
					worstFrameTimes[i];

				smallestIndex =
					i;
			}
		}

		if (
			insertIndex < 0 &&
			frameMs >
			smallestWorst)
		{
			insertIndex =
				smallestIndex;
		}

		if (
			insertIndex >= 0)
		{
			worstFrameTimes[
				insertIndex
			] =
				frameMs;

			worstFrameNumbers[
				insertIndex
			] =
				frameNumber;
		}
	}

	// ============================================================
	// Main report
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

		double averageFps =
			averageMs > 0.0
				? 1000.0 /
				  averageMs
				: 0.0;

		double worstFps =
			maxFrameMs > 0.0
				? 1000.0 /
				  maxFrameMs
				: 0.0;

		// --------------------------------------------------------
		// Godot performance monitors
		// --------------------------------------------------------

		double processMs =
			GetPerformanceMonitor(
				Performance.Monitor.TimeProcess
			) *
			1000.0;

		double physicsMs =
			GetPerformanceMonitor(
				Performance.Monitor.TimePhysicsProcess
			) *
			1000.0;

		double navigationMs =
			GetPerformanceMonitor(
				Performance.Monitor.TimeNavigationProcess
			) *
			1000.0;

		long objects =
			GetPerformanceMonitorLong(
				Performance.Monitor.ObjectCount
			);

		long nodes =
			GetPerformanceMonitorLong(
				Performance.Monitor.ObjectNodeCount
			);

		long renderedObjects =
			GetPerformanceMonitorLong(
				Performance.Monitor.RenderTotalObjectsInFrame
			);

		long drawCalls =
			GetPerformanceMonitorLong(
				Performance.Monitor.RenderTotalDrawCallsInFrame
			);

		long primitives =
			GetPerformanceMonitorLong(
				Performance.Monitor.RenderTotalPrimitivesInFrame
			);

		long videoMemory =
			GetPerformanceMonitorLong(
				Performance.Monitor.RenderVideoMemUsed
			);

		long textureMemory =
			GetPerformanceMonitorLong(
				Performance.Monitor.RenderTextureMemUsed
			);

		long bufferMemory =
			GetPerformanceMonitorLong(
				Performance.Monitor.RenderBufferMemUsed
			);

		// --------------------------------------------------------
		// Unaccounted frame time
		//
		// This is NOT GPU time.
		//
		// It is the portion of the measured frame that is not
		// represented by these CPU performance monitors.
		// --------------------------------------------------------

		double unaccountedMs =
			averageMs -
			processMs -
			physicsMs -
			navigationMs;

		if (
			unaccountedMs < 0.0)
		{
			unaccountedMs = 0.0;
		}

		// --------------------------------------------------------
		// Report
		// --------------------------------------------------------

		GD.Print(
			"\n" +
			"============================================================\n" +
			"[FRAME PROFILER]\n" +
			"============================================================\n" +
			$"Frame samples:       {samplesCollected}\n" +
			$"Total frame count:   {totalFrameCounter}\n" +
			"------------------------------------------------------------\n" +
			$"AVERAGE              {averageMs,8:F3} ms   {averageFps,7:F2} FPS\n" +
			$"P50                  {p50,8:F3} ms\n" +
			$"P95                  {p95,8:F3} ms\n" +
			$"P99                  {p99,8:F3} ms\n" +
			$"MIN                  {minFrameMs,8:F3} ms   frame {minFrameNumber}\n" +
			$"MAX                  {maxFrameMs,8:F3} ms   frame {maxFrameNumber}\n" +
			$"WORST FPS            {worstFps,8:F2}\n" +
			$"SPIKES >= {SpikeThresholdMs:F1}ms:   {spikeCount}\n" +
			"------------------------------------------------------------\n" +
			"GODOT TIMING\n" +
			"------------------------------------------------------------\n" +
			$"Process              {processMs,8:F3} ms\n" +
			$"Physics              {physicsMs,8:F3} ms\n" +
			$"Navigation            {navigationMs,8:F3} ms\n" +
			$"Unaccounted          {unaccountedMs,8:F3} ms\n" +
			"------------------------------------------------------------\n" +
			"RENDERING\n" +
			"------------------------------------------------------------\n" +
			$"Rendered objects     {renderedObjects,8}\n" +
			$"Draw calls           {drawCalls,8}\n" +
			$"Primitives           {primitives,8}\n" +
			"------------------------------------------------------------\n" +
			"MEMORY\n" +
			"------------------------------------------------------------\n" +
			$"Video memory         {FormatBytes(videoMemory),12}\n" +
			$"Texture memory       {FormatBytes(textureMemory),12}\n" +
			$"Buffer memory        {FormatBytes(bufferMemory),12}\n" +
			"------------------------------------------------------------\n" +
			"SCENE\n" +
			"------------------------------------------------------------\n" +
			$"Objects              {objects,8}\n" +
			$"Nodes                {nodes,8}\n" +
			"------------------------------------------------------------\n" +
			"WORST INDIVIDUAL FRAMES\n" +
            "------------------------------------------------------------"
		);

		PrintWorstFrames();

		GD.Print(
            "============================================================\n"
		);

		ResetStatistics();
	}

	// ============================================================
	// Worst individual frames
	// ============================================================

	private void PrintWorstFrames()
	{
		double[] times =
			new double[WorstFrameCount];

		long[] numbers =
			new long[WorstFrameCount];

		Array.Copy(
			worstFrameTimes,
			times,
			WorstFrameCount
		);

		Array.Copy(
			worstFrameNumbers,
			numbers,
			WorstFrameCount
		);

		// Sort descending.
		for (
			int i = 0;
			i < WorstFrameCount - 1;
			i++)
		{
			for (
				int j = i + 1;
				j < WorstFrameCount;
				j++)
			{
				if (
					times[j] >
					times[i])
				{
					double tempTime =
						times[i];

					times[i] =
						times[j];

					times[j] =
						tempTime;

					long tempNumber =
						numbers[i];

					numbers[i] =
						numbers[j];

					numbers[j] =
						tempNumber;
				}
			}
		}

		int printed = 0;

		for (
			int i = 0;
			i < WorstFrameCount;
			i++)
		{
			if (
				times[i] <=
				0.0)
			{
				continue;
			}

			double fps =
				times[i] > 0.0
					? 1000.0 /
					  times[i]
					: 0.0;

			GD.Print(
				$"#{printed + 1,-2}  " +
				$"frame {numbers[i],6}  " +
				$"{times[i],9:F3} ms  " +
				$"{fps,7:F2} FPS"
			);

			printed++;
		}
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

		Array.Sort(values);

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
	// Hardware information
	// ============================================================

	private void PrintHardwareInfo()
	{
		string renderer =
			"Unknown";

		string renderingMethod =
			"Unknown";

		try
		{
			renderer =
				RenderingServer.GetVideoAdapterName();
		}
		catch
		{
			// Keep default.
		}

		try
		{
			renderingMethod =
				ProjectSettings.GetSetting(
					"rendering/renderer/rendering_method",
                    "Unknown"
				).AsString();
		}
		catch
		{
			// Keep default.
		}

		GD.Print(
			"------------------------------------------------------------\n" +
			"[FRAME PROFILER] HARDWARE\n" +
			"------------------------------------------------------------\n" +
			$"Video adapter:       {renderer}\n" +
			$"Rendering method:    {renderingMethod}\n" +
            "------------------------------------------------------------"
		);
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
	// Format memory
	// ============================================================

	private static string FormatBytes(
		long bytes)
	{
		if (
			bytes <= 0)
		{
			return "0 B";
		}

		const double KB =
			1024.0;

		const double MB =
			KB * 1024.0;

		const double GB =
			MB * 1024.0;

		if (
			bytes >= GB)
		{
			return
				$"{bytes / GB:F2} GB";
		}

		if (
			bytes >= MB)
		{
			return
				$"{bytes / MB:F2} MB";
		}

		if (
			bytes >= KB)
		{
			return
				$"{bytes / KB:F2} KB";
		}

		return
			$"{bytes} B";
	}

	// ============================================================
	// Reset statistics
	// ============================================================

	private void ResetStatistics()
	{
		totalFrameMs = 0.0;

		minFrameMs =
			double.MaxValue;

		maxFrameMs = 0.0;

		minFrameNumber = 0;
		maxFrameNumber = 0;

		spikeCount = 0;

		ClearWorstFrames();
	}

	// ============================================================
	// Clear worst frames
	// ============================================================

	private void ClearWorstFrames()
	{
		for (
			int i = 0;
			i < WorstFrameCount;
			i++)
		{
			worstFrameTimes[i] =
				0.0;

			worstFrameNumbers[i] =
				0;
		}
	}
}
