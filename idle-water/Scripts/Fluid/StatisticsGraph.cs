using Godot;
using System;
using System.Collections.Generic;

public partial class StatisticsGraph : Control
{
	// ============================================================
	// Graph configuration
	// ============================================================

	private const int MaxSamples = 120;

	private const float GraphMarginLeft = 55.0f;
	private const float GraphMarginRight = 15.0f;
	private const float GraphMarginTop = 25.0f;
	private const float GraphMarginBottom = 40.0f;

	private const float SecondGraphSpacing = 35.0f;

	// ============================================================
	// Existing graph
	// ============================================================

	private readonly List<float> particleHistory =
		new List<float>();

	private readonly List<float> energyHistory =
		new List<float>();

	private readonly List<float> fpsHistory =
		new List<float>();

	// ============================================================
	// Second statistical graph
	//
	// X = average rain amount (%)
	// Y = average energy gained
	//
	// Average active particles is also collected and stored,
	// but is NOT used for the X-axis.
	//
	// Sampling:
	//
	// 600 physics frames = 10 seconds
	//
	// The first 600 frames are a warm-up period.
	//
	// No point is created at 10 seconds.
	//
	// First point:
	//
	//     Frame 1200 = 20 seconds
	//
	// and represents:
	//
	//     Frames 601-1200
	//
	// Then:
	//
	//     Frame 1800 = Point #2
	//     Frames 1201-1800
	//
	//     Frame 2400 = Point #3
	//     Frames 1801-2400
	//
	// etc.
	//
	// This means every plotted point represents exactly
	// the preceding 10-second period.
	// ============================================================

	private readonly List<float> rainEnergyRainHistory =
		new List<float>();

	private readonly List<float> rainEnergyEnergyHistory =
		new List<float>();

	private readonly List<float> rainEnergyParticleHistory =
		new List<float>();

	private const int StatisticalSampleIntervalFrames = 600;

	private const int StatisticalFirstSampleFrame = 1200;

	private int statisticalFrameCounter = 0;

	private double statisticalRainSum = 0.0;

	private double statisticalEnergySum = 0.0;

	private double statisticalParticleSum = 0.0;

	private int statisticalSampleFrames = 0;

	// ============================================================
	// Existing graph timing
	// ============================================================

	private float graphElapsedTime = 0.0f;

	// ============================================================
	// Rendering
	// ============================================================

	private Font defaultFont;

	// ============================================================
	// Initialization
	// ============================================================

	public override void _Ready()
	{
		defaultFont =
			ThemeDB.FallbackFont;

		QueueRedraw();
	}

	// ============================================================
	// Existing graph sample
	// ============================================================

	public void AddSample(
		float activeParticles,
		double energyPerSecond,
		float fps,
		float delta)
	{
		graphElapsedTime +=
			Mathf.Max(
				delta,
				0.0f
			);

		particleHistory.Add(
			activeParticles
		);

		energyHistory.Add(
			(float)energyPerSecond
		);

		fpsHistory.Add(
			fps
		);

		while (
			particleHistory.Count >
			MaxSamples)
		{
			particleHistory.RemoveAt(0);
		}

		while (
			energyHistory.Count >
			MaxSamples)
		{
			energyHistory.RemoveAt(0);
		}

		while (
			fpsHistory.Count >
			MaxSamples)
		{
			fpsHistory.RemoveAt(0);
		}

		QueueRedraw();
	}

	// ============================================================
	// Statistical graph sample
	//
	// Expected call from FluidSimulator:
	//
	//     AddRainEnergySample(
	//         rainPercent,
	//         energyGained,
	//         averageParticles
	//     );
	//
	// This method should be called exactly once per physics frame.
	//
	// IMPORTANT:
	//
	// Frames 1-600 are intentionally ignored for the statistical
	// graph.
	//
	// Frame 1200 creates the first point using frames 601-1200.
	//
	// After that, every 600 frames creates another point.
	// ============================================================

	public void AddRainEnergySample(
		float rainPercent,
		double energyGained,
		float averageParticles)
	{
		statisticalFrameCounter++;

		// --------------------------------------------------------
		// First 600 frames:
		//
		// Warm-up period.
		//
		// Do NOT accumulate these frames because the first point
		// at frame 1200 must represent only frames 601-1200.
		// --------------------------------------------------------

		if (
			statisticalFrameCounter <=
			StatisticalSampleIntervalFrames)
		{
			return;
		}

		// --------------------------------------------------------
		// Accumulate the current frame.
		//
		// Therefore:
		//
		// Frames 601-1200
		//     = first statistical window
		//
		// Frames 1201-1800
		//     = second statistical window
		//
		// etc.
		// --------------------------------------------------------

		statisticalRainSum +=
			rainPercent;

		statisticalEnergySum +=
			energyGained;

		statisticalParticleSum +=
			averageParticles;

		statisticalSampleFrames++;

		// --------------------------------------------------------
		// Create a point only when:
		//
		// frame == 1200
		//
		// or every 600 frames after that.
		// --------------------------------------------------------

		bool shouldCreatePoint =
			statisticalFrameCounter >=
			StatisticalFirstSampleFrame &&
			(
				statisticalFrameCounter -
				StatisticalFirstSampleFrame
			) %
			StatisticalSampleIntervalFrames == 0;

		if (
			shouldCreatePoint)
		{
			CreateStatisticalPoint();
		}
	}

	// ============================================================
	// Create second graph point
	// ============================================================

	private void CreateStatisticalPoint()
	{
		if (
			statisticalSampleFrames <= 0)
		{
			return;
		}

		// --------------------------------------------------------
		// X:
		//
		// Average rain percentage during this exact
		// 10-second statistical window.
		// --------------------------------------------------------

		float averageRain =
			(float)(
				statisticalRainSum /
				statisticalSampleFrames
			);

		// --------------------------------------------------------
		// Y:
		//
		// Average energy gained during this exact
		// 10-second statistical window.
		// --------------------------------------------------------

		float averageEnergy =
			(float)(
				statisticalEnergySum /
				statisticalSampleFrames
			);

		// --------------------------------------------------------
		// Additional statistic:
		//
		// Average active particles during this exact
		// 10-second statistical window.
		//
		// Stored for future use/debugging.
		// Does NOT affect plotted coordinates.
		// --------------------------------------------------------

		float averageParticles =
			(float)(
				statisticalParticleSum /
				statisticalSampleFrames
			);

		// --------------------------------------------------------
		// Store point
		// --------------------------------------------------------

		rainEnergyRainHistory.Add(
			averageRain
		);

		rainEnergyEnergyHistory.Add(
			averageEnergy
		);

		rainEnergyParticleHistory.Add(
			averageParticles
		);

		// --------------------------------------------------------
		// Keep history bounded.
		// --------------------------------------------------------

		while (
			rainEnergyRainHistory.Count >
			MaxSamples)
		{
			rainEnergyRainHistory.RemoveAt(0);
		}

		while (
			rainEnergyEnergyHistory.Count >
			MaxSamples)
		{
			rainEnergyEnergyHistory.RemoveAt(0);
		}

		while (
			rainEnergyParticleHistory.Count >
			MaxSamples)
		{
			rainEnergyParticleHistory.RemoveAt(0);
		}

		// --------------------------------------------------------
		// Debug output
		// --------------------------------------------------------

		GD.Print(
			"STATISTICAL GRAPH: Point #" +
			rainEnergyRainHistory.Count +
			" | Frame=" +
			statisticalFrameCounter +
			" | WindowFrames=" +
			statisticalSampleFrames +
			" | AvgRain=" +
			averageRain.ToString("F2") +
			"% | AvgEnergy=" +
			averageEnergy.ToString("F4") +
			" | AvgParticles=" +
			averageParticles.ToString("F1")
		);

		// --------------------------------------------------------
		// Reset the 10-second accumulation window.
		//
		// The next frame starts a completely new window.
		// --------------------------------------------------------

		statisticalRainSum =
			0.0;

		statisticalEnergySum =
			0.0;

		statisticalParticleSum =
			0.0;

		statisticalSampleFrames =
			0;

		QueueRedraw();
	}

	// ============================================================
	// Draw
	// ============================================================

	public override void _Draw()
	{
		Rect2 rect =
			GetRect();

		if (
			rect.Size.X <= 10.0f ||
			rect.Size.Y <= 10.0f)
		{
			return;
		}

		float graphWidth =
			rect.Size.X -
			GraphMarginLeft -
			GraphMarginRight;

		float availableHeight =
			rect.Size.Y -
			SecondGraphSpacing;

		float firstGraphHeight =
			availableHeight *
			0.5f;

		float secondGraphTop =
			firstGraphHeight +
			SecondGraphSpacing;

		float secondGraphHeight =
			rect.Size.Y -
			secondGraphTop;

		// ========================================================
		// Existing graph
		// ========================================================

		DrawExistingGraph(
			new Rect2(
				GraphMarginLeft,
				GraphMarginTop,
				graphWidth,
				Mathf.Max(
					firstGraphHeight -
					GraphMarginTop,
					1.0f
				)
			)
		);

		// ========================================================
		// Second statistical graph
		// ========================================================

		DrawRainEnergyGraph(
			new Rect2(
				GraphMarginLeft,
				secondGraphTop,
				graphWidth,
				Mathf.Max(
					secondGraphHeight -
					GraphMarginBottom,
					1.0f
				)
			)
		);
	}

	// ============================================================
	// Existing graph drawing
	// ============================================================

	private void DrawExistingGraph(
		Rect2 graphRect)
	{
		if (
			graphRect.Size.X <= 0.0f ||
			graphRect.Size.Y <= 0.0f)
		{
			return;
		}

		float maxParticles =
			GetMaximumValue(
				particleHistory,
				1.0f
			);

		float maxEnergy =
			GetMaximumValue(
				energyHistory,
				1.0f
			);

		float maxFps =
			GetMaximumValue(
				fpsHistory,
				60.0f
			);

		// --------------------------------------------------------
		// Background
		// --------------------------------------------------------

		DrawRect(
			graphRect,
			new Color(
				0.05f,
				0.05f,
				0.05f,
				0.75f
			),
			true
		);

		// --------------------------------------------------------
		// Grid
		// --------------------------------------------------------

		DrawGraphGrid(
			graphRect,
			5,
			5
		);

		// --------------------------------------------------------
		// Lines
		// --------------------------------------------------------

		DrawHistoryLine(
			graphRect,
			particleHistory,
			maxParticles,
			new Color(
				0.2f,
				0.8f,
				1.0f,
				1.0f
			)
		);

		DrawHistoryLine(
			graphRect,
			energyHistory,
			maxEnergy,
			new Color(
				1.0f,
				0.75f,
				0.2f,
				1.0f
			)
		);

		DrawHistoryLine(
			graphRect,
			fpsHistory,
			maxFps,
			new Color(
				0.3f,
				1.0f,
				0.3f,
				1.0f
			)
		);

		// --------------------------------------------------------
		// Title
		// --------------------------------------------------------

		DrawString(
			defaultFont,
			new Vector2(
				graphRect.Position.X,
				graphRect.Position.Y -
				8.0f
			),
			"STATISTICS",
			HorizontalAlignment.Left,
			-1,
			14,
			Colors.White
		);
	}

	// ============================================================
	// Second graph drawing
	//
	// X = average rain %
	// Y = average energy gained
	// ============================================================

	private void DrawRainEnergyGraph(
		Rect2 graphRect)
	{
		if (
			graphRect.Size.X <= 0.0f ||
			graphRect.Size.Y <= 0.0f)
		{
			return;
		}

		const float minRain =
			0.0f;

		const float maxRain =
			100.0f;

		float maxEnergy =
			GetMaximumValue(
				rainEnergyEnergyHistory,
				1.0f
			);

		if (
			maxEnergy <= 0.0f)
		{
			maxEnergy =
				1.0f;
		}

		maxEnergy *=
			1.1f;

		// --------------------------------------------------------
		// Background
		// --------------------------------------------------------

		DrawRect(
			graphRect,
			new Color(
				0.04f,
				0.04f,
				0.04f,
				0.85f
			),
			true
		);

		// --------------------------------------------------------
		// Grid
		// --------------------------------------------------------

		DrawGraphGrid(
			graphRect,
			5,
			5
		);

		// --------------------------------------------------------
		// Title
		// --------------------------------------------------------

		DrawString(
			defaultFont,
			new Vector2(
				graphRect.Position.X,
				graphRect.Position.Y -
				8.0f
			),
			"RAIN vs ENERGY",
			HorizontalAlignment.Left,
			-1,
			14,
			Colors.White
		);

		// --------------------------------------------------------
		// X-axis title
		// --------------------------------------------------------

		DrawString(
			defaultFont,
			new Vector2(
				graphRect.Position.X,
				graphRect.Position.Y +
				graphRect.Size.Y +
				25.0f
			),
			"RAIN %",
			HorizontalAlignment.Left,
			-1,
			12,
			Colors.White
		);

		// --------------------------------------------------------
		// Y-axis title
		// --------------------------------------------------------

		DrawString(
			defaultFont,
			new Vector2(
				graphRect.Position.X -
				48.0f,
				graphRect.Position.Y +
				12.0f
			),
			"ENERGY",
			HorizontalAlignment.Left,
			-1,
			11,
			Colors.White
		);

		// --------------------------------------------------------
		// X-axis labels
		// --------------------------------------------------------

		for (
			int i = 0;
			i <= 5;
			i++)
		{
			float normalized =
				i / 5.0f;

			float x =
				graphRect.Position.X +
				graphRect.Size.X *
				normalized;

			string label =
				(
					normalized *
					100.0f
				).ToString("F0") +
				"%";

			DrawString(
				defaultFont,
				new Vector2(
					x -
					10.0f,
					graphRect.Position.Y +
					graphRect.Size.Y +
					16.0f
				),
				label,
				HorizontalAlignment.Left,
				-1,
				10,
				Colors.LightGray
			);
		}

		// --------------------------------------------------------
		// Y-axis labels
		// --------------------------------------------------------

		for (
			int i = 0;
			i <= 5;
			i++)
		{
			float normalized =
				i / 5.0f;

			float y =
				graphRect.Position.Y +
				graphRect.Size.Y -
				graphRect.Size.Y *
				normalized;

			float value =
				maxEnergy *
				normalized;

			DrawString(
				defaultFont,
				new Vector2(
					graphRect.Position.X -
					45.0f,
					y +
					4.0f
				),
				value.ToString("F2"),
				HorizontalAlignment.Right,
				40,
				10,
				Colors.LightGray
			);
		}

		// --------------------------------------------------------
		// Statistical points
		// --------------------------------------------------------

		int count =
			Math.Min(
				rainEnergyRainHistory.Count,
				rainEnergyEnergyHistory.Count
			);

		if (
			count <= 0)
		{
			return;
		}

		for (
			int i = 0;
			i < count;
			i++)
		{
			float rain =
				Mathf.Clamp(
					rainEnergyRainHistory[i],
					minRain,
					maxRain
				);

			float energy =
				Mathf.Max(
					rainEnergyEnergyHistory[i],
					0.0f
				);

			float xNormalized =
				(
					rain -
					minRain
				) /
				(
					maxRain -
					minRain
				);

			float yNormalized =
				Mathf.Clamp(
					energy /
					maxEnergy,
					0.0f,
					1.0f
				);

			Vector2 point =
				new Vector2(
					graphRect.Position.X +
					xNormalized *
					graphRect.Size.X,

					graphRect.Position.Y +
					graphRect.Size.Y -
					yNormalized *
					graphRect.Size.Y
				);

			// ----------------------------------------------------
			// Connect chronological points.
			//
			// Because this is a statistical scatter graph, the
			// points are positioned by rain/energy values, but
			// the line connects them in chronological order.
			// ----------------------------------------------------

			if (
				i > 0)
			{
				float previousRain =
					Mathf.Clamp(
						rainEnergyRainHistory[i - 1],
						minRain,
						maxRain
					);

				float previousEnergy =
					Mathf.Max(
						rainEnergyEnergyHistory[i - 1],
						0.0f
					);

				float previousXNormalized =
					(
						previousRain -
						minRain
					) /
					(
						maxRain -
						minRain
					);

				float previousYNormalized =
					Mathf.Clamp(
						previousEnergy /
						maxEnergy,
						0.0f,
						1.0f
					);

				Vector2 previousPoint =
					new Vector2(
						graphRect.Position.X +
						previousXNormalized *
						graphRect.Size.X,

						graphRect.Position.Y +
						graphRect.Size.Y -
						previousYNormalized *
						graphRect.Size.Y
					);

				DrawLine(
					previousPoint,
					point,
					new Color(
						1.0f,
						0.65f,
						0.15f,
						0.65f
					),
					2.0f
				);
			}

			// ----------------------------------------------------
			// Point
			// ----------------------------------------------------

			DrawCircle(
				point,
				4.0f,
				new Color(
					1.0f,
					0.85f,
					0.2f,
					1.0f
				)
			);
		}
	}

	// ============================================================
	// Draw graph grid
	// ============================================================

	private void DrawGraphGrid(
		Rect2 graphRect,
		int verticalLines,
		int horizontalLines)
	{
		for (
			int i = 0;
			i <= verticalLines;
			i++)
		{
			float normalized =
				i /
				(float)verticalLines;

			float x =
				graphRect.Position.X +
				graphRect.Size.X *
				normalized;

			DrawLine(
				new Vector2(
					x,
					graphRect.Position.Y
				),
				new Vector2(
					x,
					graphRect.Position.Y +
					graphRect.Size.Y
				),
				new Color(
					0.2f,
					0.2f,
					0.2f,
					0.6f
				),
				1.0f
			);
		}

		for (
			int i = 0;
			i <= horizontalLines;
			i++)
		{
			float normalized =
				i /
				(float)horizontalLines;

			float y =
				graphRect.Position.Y +
				graphRect.Size.Y -
				graphRect.Size.Y *
				normalized;

			DrawLine(
				new Vector2(
					graphRect.Position.X,
					y
				),
				new Vector2(
					graphRect.Position.X +
					graphRect.Size.X,
					y
				),
				new Color(
					0.2f,
					0.2f,
					0.2f,
					0.6f
				),
				1.0f
			);
		}
	}

	// ============================================================
	// Draw regular history line
	// ============================================================

	private void DrawHistoryLine(
		Rect2 graphRect,
		List<float> history,
		float maxValue,
		Color color)
	{
		if (
			history == null ||
			history.Count < 2 ||
			maxValue <= 0.0f)
		{
			return;
		}

		for (
			int i = 1;
			i < history.Count;
			i++)
		{
			float previousNormalized =
				Mathf.Clamp(
					history[i - 1] /
					maxValue,
					0.0f,
					1.0f
				);

			float currentNormalized =
				Mathf.Clamp(
					history[i] /
					maxValue,
					0.0f,
					1.0f
				);

			float previousX =
				graphRect.Position.X +
				(
					(i - 1) /
					(float)(history.Count - 1)
				) *
				graphRect.Size.X;

			float currentX =
				graphRect.Position.X +
				(
					i /
					(float)(history.Count - 1)
				) *
				graphRect.Size.X;

			float previousY =
				graphRect.Position.Y +
				graphRect.Size.Y -
				previousNormalized *
				graphRect.Size.Y;

			float currentY =
				graphRect.Position.Y +
				graphRect.Size.Y -
				currentNormalized *
				graphRect.Size.Y;

			DrawLine(
				new Vector2(
					previousX,
					previousY
				),
				new Vector2(
					currentX,
					currentY
				),
				color,
				2.0f
			);
		}
	}

	// ============================================================
	// Maximum value helper
	// ============================================================

	private float GetMaximumValue(
		List<float> values,
		float minimum)
	{
		float maximum =
			minimum;

		for (
			int i = 0;
			i < values.Count;
			i++)
		{
			if (
				values[i] >
				maximum)
			{
				maximum =
					values[i];
			}
		}

		return maximum;
	}

	// ============================================================
	// Public reset
	// ============================================================

	public void ClearStatistics()
	{
		particleHistory.Clear();

		energyHistory.Clear();

		fpsHistory.Clear();

		rainEnergyRainHistory.Clear();

		rainEnergyEnergyHistory.Clear();

		rainEnergyParticleHistory.Clear();

		graphElapsedTime =
			0.0f;

		statisticalFrameCounter =
			0;

		statisticalRainSum =
			0.0;

		statisticalEnergySum =
			0.0;

		statisticalParticleSum =
			0.0;

		statisticalSampleFrames =
			0;

		QueueRedraw();
	}
}
