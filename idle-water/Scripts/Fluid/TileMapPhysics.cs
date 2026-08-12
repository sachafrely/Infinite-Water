using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;

/// <summary>
/// Generates PBF collision geometry from the visual Environment TileMapLayer.
///
/// IMPORTANT:
/// This version treats the terrain as SOLID OCCUPIED AREA instead of
/// reconstructing only its border.
///
/// Pipeline:
///
///     visual tile pixels
///          ↓
///     solid pixel mask
///          ↓
///     rectangle decomposition
///          ↓
///     convex filled polygon colliders
///
/// This intentionally does NOT use:
/// - contour extraction
/// - border segments
/// - collision thickness
/// - endpoint extension
/// - corner thickening
/// - contour simplification
/// - artificial dilation
///
/// The collision therefore represents the actual solid volume of the
/// environment rather than a collection of thickened boundary lines.
/// </summary>
[Tool]
public partial class TileMapPhysics : Node2D
{
	// ============================================================
	// Configuration
	// ============================================================

	[Export]
	public NodePath EnvironmentPath { get; set; } =
		new NodePath("../Environment");

	/// <summary>
	/// Kept for scene compatibility.
	///
	/// NO LONGER USED for collision generation.
	/// Solid-area collision does not need artificial thickness.
	/// </summary>
	[Export]
	public float CollisionThickness { get; set; } = 0.0f;

	/// <summary>
	/// Kept for scene compatibility.
	///
	/// NO LONGER USED.
	/// </summary>
	[Export]
	public float CollisionEndExtension { get; set; } = 0.0f;

	[Export]
	public bool GenerateOnReady { get; set; } = true;

	[Export]
	public bool RebuildWhenChanged { get; set; } = false;

	[Export]
	public bool DebugOutput { get; set; } = true;

	[Export]
	public bool ShowDebugGeometry { get; set; } = true;

	[Export]
	public Color DebugColor { get; set; } =
		new Color(
			1.0f,
			0.2f,
			0.1f,
			0.9f
		);

	[Export]
	public NodePath GameViewPath { get; set; } =
		new NodePath("../GameView");

	[Export]
	public NodePath SimulationViewportPath { get; set; } =
		new NodePath(
			"../GameView/SimulationViewport"
		);

	[Export]
	public NodePath CameraPath { get; set; } =
		new NodePath(
			"../GameView/SimulationViewport/Camera2D"
		);

	// ============================================================
	// Texture classification
	// ============================================================

	[Export]
	public float AlphaThreshold { get; set; } = 0.01f;

	[Export]
	public Color EmptyColor { get; set; } =
		new Color(
			34.0f / 255.0f,
			42.0f / 255.0f,
			92.0f / 255.0f,
			1.0f
		);

	[Export]
	public float EmptyColorTolerance { get; set; } = 0.04f;

	[Export]
	public bool UseEmptyColorKey { get; set; } = true;

	// ============================================================
	// Solid-region generation
	// ============================================================

	/// <summary>
	/// Minimum rectangle width.
	/// </summary>
	[Export]
	public int MinimumRectangleWidth { get; set; } = 1;

	/// <summary>
	/// Minimum rectangle height.
	/// </summary>
	[Export]
	public int MinimumRectangleHeight { get; set; } = 1;

	/// <summary>
	/// Maximum generated solid-region colliders.
	///
	/// This is a safety limit only.
	/// </summary>
	[Export]
	public int MaximumSolidColliders { get; set; } = 5000;

	/// <summary>
	/// When true, rectangles are merged aggressively.
	///
	/// This reduces PBF collision checks substantially.
	/// </summary>
	[Export]
	public bool MergeSolidRegions { get; set; } = true;

	// ============================================================
	// Runtime references
	// ============================================================

	private TileMapLayer environment;
	private FluidSimulator simulator;
	private PbfSolver solver;

	private SubViewportContainer gameView;
	private SubViewport simulationViewport;
	private Camera2D simulationCamera;

	// ============================================================
	// Generated collision
	// ============================================================

	private readonly List<FluidPolygonCollider>
		generatedColliders =
			new List<FluidPolygonCollider>();

	private readonly List<DebugEdge>
		debugEdges =
			new List<DebugEdge>();

	private bool generated;
	private bool generating;

	// ============================================================
	// Debug edge
	// ============================================================

	private struct DebugEdge
	{
		public Vector2 A;
		public Vector2 B;

		public DebugEdge(
			Vector2 a,
			Vector2 b)
		{
			A = a;
			B = b;
		}
	}

	// ============================================================
	// Solid rectangle
	// ============================================================

	private struct SolidRectangle
	{
		public int X;
		public int Y;
		public int Width;
		public int Height;

		public SolidRectangle(
			int x,
			int y,
			int width,
			int height)
		{
			X = x;
			Y = y;
			Width = width;
			Height = height;
		}
	}

	// ============================================================
	// Ready
	// ============================================================

	public override void _Ready()
	{
		GD.Print(
			"========== TILEMAP PHYSICS READY =========="
		);

		GD.Print(
			"TileMapPhysics node: " +
			GetPath()
		);

		GD.Print(
			"Process mode: " +
			ProcessMode
		);

		GD.Print(
			"ShowDebugGeometry: " +
			ShowDebugGeometry
		);

		GD.Print(
			"Collision mode: SOLID REGION"
		);

		CallDeferred(
			nameof(Initialize)
		);
	}

	// ============================================================
	// Initialize
	// ============================================================

	private void Initialize()
	{
		if (generating)
			return;

environment =
	GetEnvironment();

if (environment == null)
{
	GD.PushError(
		"TileMapPhysics: Environment TileMapLayer " +
        "could not be found."
	);

	return;
}

// --------------------------------------------------------
// Rendering order:
// Environment is in front of wheels and water.
// --------------------------------------------------------

environment.ZIndex = 20;

simulator =
	FindFluidSimulator(
		GetTree().Root
	);


		if (simulator == null)
		{
			GD.PushError(
				"TileMapPhysics: FluidSimulator " +
				"could not be found."
			);

			return;
		}

		solver =
			GetSolver(
				simulator
			);

		if (solver == null)
		{
			GD.PushWarning(
				"TileMapPhysics: PbfSolver is not ready yet."
			);

			CallDeferred(
				nameof(Initialize)
			);

			return;
		}

		FindViewportMapping();

		if (
			gameView == null ||
			simulationViewport == null ||
			simulationCamera == null)
		{
			GD.PushError(
				"TileMapPhysics: Could not establish " +
				"viewport mapping."
			);

			return;
		}

		if (GenerateOnReady)
		{
			GenerateColliders();
		}
	}

	// ============================================================
	// Process
	// ============================================================

	public override void _Process(
		double delta)
	{
		if (ShowDebugGeometry)
		{
			QueueRedraw();
		}
	}

	// ============================================================
	// Draw
	// ============================================================

	public override void _Draw()
	{
		if (!ShowDebugGeometry)
			return;

		if (
			gameView == null ||
			simulationViewport == null ||
			simulationCamera == null)
		{
			return;
		}

		foreach (
			DebugEdge edge in debugEdges)
		{
			Vector2 a =
				SimulationToThisLocal(
					edge.A
				);

			Vector2 b =
				SimulationToThisLocal(
					edge.B
				);

			DrawLine(
				a,
				b,
				DebugColor,
				2.0f,
				true
			);
		}
	}

	// ============================================================
	// Environment
	// ============================================================

	private TileMapLayer GetEnvironment()
	{
		if (
			EnvironmentPath != null &&
			!EnvironmentPath.IsEmpty)
		{
			Node node =
				GetNodeOrNull(
					EnvironmentPath
				);

			if (node is TileMapLayer)
			{
				return (TileMapLayer)node;
			}
		}

		return FindTileMapLayer(
			GetTree().Root
		);
	}

	private static TileMapLayer FindTileMapLayer(
		Node node)
	{
		if (node is TileMapLayer)
		{
			TileMapLayer layer =
				(TileMapLayer)node;

			if (layer.Name == "Environment")
			{
				return layer;
			}
		}

		foreach (
			Node child in node.GetChildren())
		{
			TileMapLayer result =
				FindTileMapLayer(
					child
				);

			if (result != null)
			{
				return result;
			}
		}

		return null;
	}

	// ============================================================
	// Viewport mapping
	// ============================================================

	private void FindViewportMapping()
	{
		if (
			GameViewPath != null &&
			!GameViewPath.IsEmpty)
		{
			Node node =
				GetNodeOrNull(
					GameViewPath
				);

			if (node is SubViewportContainer)
			{
				gameView =
					(SubViewportContainer)node;
			}
		}

		if (gameView == null)
		{
			gameView =
				FindNodeOfType<SubViewportContainer>(
					GetTree().Root
				);
		}

		if (
			SimulationViewportPath != null &&
			!SimulationViewportPath.IsEmpty)
		{
			Node node =
				GetNodeOrNull(
					SimulationViewportPath
				);

			if (node is SubViewport)
			{
				simulationViewport =
					(SubViewport)node;
			}
		}

		if (
			simulationViewport == null &&
			gameView != null)
		{
			foreach (
				Node child in gameView.GetChildren())
			{
				if (child is SubViewport)
				{
					simulationViewport =
						(SubViewport)child;

					break;
				}
			}
		}

		if (
			CameraPath != null &&
			!CameraPath.IsEmpty)
		{
			Node node =
				GetNodeOrNull(
					CameraPath
				);

			if (node is Camera2D)
			{
				simulationCamera =
					(Camera2D)node;
			}
		}

		if (
			simulationCamera == null &&
			simulationViewport != null)
		{
			simulationCamera =
				FindNodeOfType<Camera2D>(
					simulationViewport
				);
		}

		if (DebugOutput)
		{
			GD.Print(
				"TileMapPhysics viewport mapping:"
			);

			GD.Print(
				"  GameView: " +
				(
					gameView != null
						? gameView.GetPath().ToString()
						: "NULL"
				)
			);

			GD.Print(
				"  SimulationViewport: " +
				(
					simulationViewport != null
						? simulationViewport.GetPath().ToString()
						: "NULL"
				)
			);

			GD.Print(
				"  Camera: " +
				(
					simulationCamera != null
						? simulationCamera.GetPath().ToString()
						: "NULL"
				)
			);
		}
	}

	private static T FindNodeOfType<T>(
		Node node)
		where T : Node
	{
		if (node is T)
		{
			return (T)node;
		}

		foreach (
			Node child in node.GetChildren())
		{
			T result =
				FindNodeOfType<T>(
					child
				);

			if (result != null)
			{
				return result;
			}
		}

		return null;
	}

	// ============================================================
	// Solver
	// ============================================================

	private static PbfSolver GetSolver(
		FluidSimulator fluidSimulator)
	{
		FieldInfo solverField =
			typeof(FluidSimulator).GetField(
				"solver",
				BindingFlags.Instance |
				BindingFlags.NonPublic
			);

		if (solverField == null)
		{
			GD.PushError(
				"TileMapPhysics: Could not access " +
				"FluidSimulator.solver."
			);

			return null;
		}

		return solverField.GetValue(
			fluidSimulator
		) as PbfSolver;
	}

	// ============================================================
	// Generate colliders
	// ============================================================

	public void GenerateColliders()
	{
		GD.Print(
			"========== GENERATE SOLID COLLIDERS =========="
		);

		if (generating)
			return;

		if (environment == null)
		{
			environment =
				GetEnvironment();
		}

		if (simulator == null)
		{
			simulator =
				FindFluidSimulator(
					GetTree().Root
				);
		}

		if (
			solver == null &&
			simulator != null)
		{
			solver =
				GetSolver(
					simulator
				);
		}

		if (
			environment == null ||
			simulator == null ||
			solver == null)
		{
			GD.PushWarning(
				"TileMapPhysics: Required nodes are not ready."
			);

			return;
		}

		FindViewportMapping();

		if (
			gameView == null ||
			simulationViewport == null ||
			simulationCamera == null)
		{
			GD.PushError(
				"TileMapPhysics: Viewport mapping is invalid."
			);

			return;
		}

		generating = true;
		generated = false;

		// --------------------------------------------------------
		// Remove old PBF collision.
		// --------------------------------------------------------

		solver.ClearPolygonColliders();

		generatedColliders.Clear();
		debugEdges.Clear();

		GD.Print(
			"TileMapPhysics: Building solid terrain mask..."
		);

		// --------------------------------------------------------
		// Build the actual occupied pixel mask.
		//
		// NO dilation.
		// NO contour reconstruction.
		// --------------------------------------------------------

		HashSet<Vector2I> solidPixels =
			BuildGlobalSolidMask();

		if (solidPixels.Count == 0)
		{
			GD.PushWarning(
				"TileMapPhysics: No collision pixels found."
			);

			generating = false;

			return;
		}

		// --------------------------------------------------------
		// Convert solid pixels into merged rectangles.
		// --------------------------------------------------------

		List<SolidRectangle> rectangles =
			BuildSolidRectangles(
				solidPixels
			);

		if (DebugOutput)
		{
			GD.Print(
				"TileMapPhysics: solid pixels = " +
				solidPixels.Count
			);

			GD.Print(
				"TileMapPhysics: generated solid rectangles = " +
				rectangles.Count
			);
		}

		// --------------------------------------------------------
		// Create one convex filled collider per rectangle.
		// --------------------------------------------------------

		int generatedCount = 0;

		foreach (
			SolidRectangle rectangle in rectangles)
		{
			if (
				generatedCount >=
				MaximumSolidColliders)
			{
				GD.PushWarning(
					"TileMapPhysics: MaximumSolidColliders " +
					"reached."
				);

				break;
			}

			Vector2 topLeft =
				new Vector2(
					rectangle.X,
					rectangle.Y
				);

			Vector2 bottomRight =
				new Vector2(
					rectangle.X +
					rectangle.Width,
					rectangle.Y +
					rectangle.Height
				);

			Vector2 simulationTopLeft =
				ToSimulationSpace(
					topLeft
				);

			Vector2 simulationBottomRight =
				ToSimulationSpace(
					bottomRight
				);

			float minX =
				Mathf.Min(
					simulationTopLeft.X,
					simulationBottomRight.X
				);

			float maxX =
				Mathf.Max(
					simulationTopLeft.X,
					simulationBottomRight.X
				);

			float minY =
				Mathf.Min(
					simulationTopLeft.Y,
					simulationBottomRight.Y
				);

			float maxY =
				Mathf.Max(
					simulationTopLeft.Y,
					simulationBottomRight.Y
				);

			if (
				maxX - minX <= 0.001f ||
				maxY - minY <= 0.001f)
			{
				continue;
			}

			Vector2[] polygon =
			{
				new Vector2(minX, minY),
				new Vector2(minX, maxY),
				new Vector2(maxX, maxY),
				new Vector2(maxX, minY)
			};

			// Ensure the winding expected by the existing
			// FluidPolygonCollider implementation.
			if (
				PolygonArea(polygon) >
				0.0f)
			{
				Array.Reverse(
					polygon
				);
			}

			FluidPolygonCollider collider =
				new FluidPolygonCollider(
					polygon
				);

			solver.AddPolygonCollider(
				collider
			);

			generatedColliders.Add(
				collider
			);

			generatedCount++;
		}

		// --------------------------------------------------------
		// Build debug boundary directly from the solid mask.
		//
		// This is ONLY visualization. It is not used for physics.
		// --------------------------------------------------------

		if (ShowDebugGeometry)
		{
			BuildDebugBoundary(
				solidPixels
			);
		}

		generated =
			generatedColliders.Count > 0;

		generating = false;

		if (DebugOutput)
		{
			GD.Print(
				"========================================"
			);

			GD.Print(
				"TileMapPhysics SOLID COLLISION RESULT"
			);

			GD.Print(
				"Used cells: " +
				environment.GetUsedCells().Count
			);

			GD.Print(
				"Solid pixels: " +
				solidPixels.Count
			);

			GD.Print(
				"Solid rectangles: " +
				rectangles.Count
			);

			GD.Print(
				"Generated colliders: " +
				generatedColliders.Count
			);

			GD.Print(
				"Collision thickness: NOT USED"
			);

			GD.Print(
				"Collision extension: NOT USED"
			);

			GD.Print(
				"Dilation: NOT USED"
			);

			GD.Print(
				"========================================"
			);
		}

		QueueRedraw();
	}

	// ============================================================
	// Build global solid mask
	// ============================================================

	private HashSet<Vector2I>
		BuildGlobalSolidMask()
	{
		HashSet<Vector2I> solidPixels =
			new HashSet<Vector2I>();

		if (
			environment == null ||
			environment.TileSet == null)
		{
			return solidPixels;
		}

		TileSet tileSet =
			environment.TileSet;

		Vector2 tileSize =
			GetTileSize();

		Godot.Collections.Array<Vector2I> cells =
			environment.GetUsedCells();

		Dictionary<int, Image> imageCache =
			new Dictionary<int, Image>();

		int processedCells = 0;
		int collisionPixels = 0;
		int ignoredBackgroundPixels = 0;
		int ignoredTransparentPixels = 0;

		foreach (
			Vector2I cell in cells)
		{
			int sourceId =
				environment.GetCellSourceId(
					cell
				);

			if (sourceId < 0)
				continue;

			Vector2I atlasCoords =
				environment.GetCellAtlasCoords(
					cell
				);

			if (
				atlasCoords.X < 0 ||
				atlasCoords.Y < 0)
			{
				continue;
			}

			TileSetSource source =
				tileSet.GetSource(
					sourceId
				);

			if (!(source is TileSetAtlasSource))
				continue;

			TileSetAtlasSource atlas =
				(TileSetAtlasSource)source;

			if (!atlas.HasTile(atlasCoords))
				continue;

			Texture2D texture =
				atlas.Texture;

			if (texture == null)
				continue;

			Image image;

			if (
				!imageCache.TryGetValue(
					sourceId,
					out image
				))
			{
				image =
					texture.GetImage();

				if (image == null)
					continue;

				imageCache[sourceId] =
					image;
			}

			Rect2I region =
				atlas.GetTileTextureRegion(
					atlasCoords
				);

			Rect2I textureRect =
				new Rect2I(
					0,
					0,
					image.GetWidth(),
					image.GetHeight()
				);

			Rect2I clipped =
				region.Intersection(
					textureRect
				);

			if (
				clipped.Size.X <= 0 ||
				clipped.Size.Y <= 0)
			{
				continue;
			}

			int width =
				clipped.Size.X;

			int height =
				clipped.Size.Y;

			float scaleX =
				tileSize.X /
				Mathf.Max(
					1,
					width
				);

			float scaleY =
				tileSize.Y /
				Mathf.Max(
					1,
					height
				);

			Vector2 cellCenter =
				environment.MapToLocal(
					cell
				);

			Vector2 tileTopLeft =
				cellCenter -
				tileSize *
				0.5f;

			for (
				int y = 0;
				y < height;
				y++)
			{
				for (
					int x = 0;
					x < width;
					x++)
				{
					Color pixel =
						image.GetPixel(
							clipped.Position.X + x,
							clipped.Position.Y + y
						);

					if (
						pixel.A <
						AlphaThreshold)
					{
						ignoredTransparentPixels++;
						continue;
					}

					if (
						UseEmptyColorKey &&
						IsEmptyBackgroundPixel(
							pixel
						))
					{
						ignoredBackgroundPixels++;
						continue;
					}

					int worldX =
						Mathf.FloorToInt(
							tileTopLeft.X +
							x * scaleX
						);

					int worldY =
						Mathf.FloorToInt(
							tileTopLeft.Y +
							y * scaleY
						);

					solidPixels.Add(
						new Vector2I(
							worldX,
							worldY
						)
					);

					collisionPixels++;
				}
			}

			processedCells++;
		}

		if (DebugOutput)
		{
			GD.Print(
				"TileMapPhysics: processed " +
				processedCells +
				" cells."
			);

			GD.Print(
				"TileMapPhysics: collision pixels = " +
				collisionPixels
			);

			GD.Print(
				"TileMapPhysics: ignored background pixels = " +
				ignoredBackgroundPixels
			);

			GD.Print(
				"TileMapPhysics: ignored transparent pixels = " +
				ignoredTransparentPixels
			);
		}

		return solidPixels;
	}

	// ============================================================
	// Empty background
	// ============================================================

	private bool IsEmptyBackgroundPixel(
		Color pixel)
	{
		float dr =
			pixel.R -
			EmptyColor.R;

		float dg =
			pixel.G -
			EmptyColor.G;

		float db =
			pixel.B -
			EmptyColor.B;

		float distanceSquared =
			dr * dr +
			dg * dg +
			db * db;

		float tolerance =
			Mathf.Max(
				0.0f,
				EmptyColorTolerance
			);

		return
			distanceSquared <=
			tolerance * tolerance;
	}

	// ============================================================
	// Solid rectangle decomposition
	// ============================================================

	private List<SolidRectangle>
		BuildSolidRectangles(
			HashSet<Vector2I> solid)
	{
		List<SolidRectangle> rectangles =
			new List<SolidRectangle>();

		if (solid.Count == 0)
			return rectangles;

		// --------------------------------------------------------
		// Find bounds.
		// --------------------------------------------------------

		int minX = int.MaxValue;
		int minY = int.MaxValue;
		int maxX = int.MinValue;
		int maxY = int.MinValue;

		foreach (
			Vector2I p in solid)
		{
			if (p.X < minX)
				minX = p.X;

			if (p.Y < minY)
				minY = p.Y;

			if (p.X > maxX)
				maxX = p.X;

			if (p.Y > maxY)
				maxY = p.Y;
		}

		int width =
			maxX -
			minX +
			1;

		int height =
			maxY -
			minY +
			1;

		// --------------------------------------------------------
		// A compact byte grid is much cheaper than repeatedly
		// scanning a HashSet during rectangle decomposition.
		// --------------------------------------------------------

		bool[,] occupied =
			new bool[
				width,
				height
			];

		foreach (
			Vector2I p in solid)
		{
			occupied[
				p.X - minX,
				p.Y - minY
			] = true;
		}

		// --------------------------------------------------------
		// Greedy maximal rectangle decomposition.
		//
		// At every unprocessed solid pixel:
		//
		// 1. Find the widest horizontal run.
		// 2. Expand that run downward while all pixels remain solid.
		// 3. Emit one rectangle.
		//
		// The consumed area is removed from the working grid.
		//
		// This produces filled collision regions rather than
		// individual boundary segments.
		// --------------------------------------------------------

		for (
			int y = 0;
			y < height;
			y++)
		{
			for (
				int x = 0;
				x < width;
				x++)
			{
				if (!occupied[x, y])
					continue;

				int rectangleWidth =
					FindMaximumWidth(
						occupied,
						x,
						y,
						width
					);

				if (
					rectangleWidth <
					MinimumRectangleWidth)
				{
					rectangleWidth = 1;
				}

				int rectangleHeight =
					1;

				while (
					y + rectangleHeight <
					height)
				{
					bool entireRow =
						true;

					for (
						int xx = x;
						xx <
							x +
							rectangleWidth;
						xx++)
					{
						if (
							!occupied[
								xx,
								y +
								rectangleHeight
							])
						{
							entireRow = false;
							break;
						}
					}

					if (!entireRow)
						break;

					rectangleHeight++;
				}

				if (
					rectangleHeight <
					MinimumRectangleHeight)
				{
					rectangleHeight = 1;
				}

				SolidRectangle rectangle =
					new SolidRectangle(
						minX + x,
						minY + y,
						rectangleWidth,
						rectangleHeight
					);

				rectangles.Add(
					rectangle
				);

				// ------------------------------------------------
				// Mark the entire rectangle as consumed.
				// ------------------------------------------------

				for (
					int yy = y;
					yy <
						y +
						rectangleHeight;
					yy++)
				{
					for (
						int xx = x;
						xx <
							x +
							rectangleWidth;
						xx++)
					{
						occupied[
							xx,
							yy
						] = false;
					}
				}
			}
		}

		if (MergeSolidRegions)
		{
			rectangles =
				MergeAdjacentRectangles(
					rectangles
				);
		}

		return rectangles;
	}

	// ============================================================
	// Maximum horizontal run
	// ============================================================

	private static int FindMaximumWidth(
		bool[,] occupied,
		int x,
		int y,
		int width)
	{
		int result = 0;

		for (
			int xx = x;
			xx < width;
			xx++)
		{
			if (!occupied[xx, y])
				break;

			result++;
		}

		return result;
	}

	// ============================================================
	// Merge adjacent rectangles
	// ============================================================

	private static List<SolidRectangle>
		MergeAdjacentRectangles(
			List<SolidRectangle> source)
	{
		if (source.Count <= 1)
			return source;

		List<SolidRectangle> result =
			new List<SolidRectangle>(
				source
			);

		bool changed = true;

		// Keep this deliberately bounded. The initial greedy
		// decomposition already does the majority of the work.
		int safety =
			Mathf.Min(
				64,
				result.Count
			);

		while (
			changed &&
			safety-- > 0)
		{
			changed = false;

			for (
				int i = 0;
				i < result.Count &&
				!changed;
				i++)
			{
				SolidRectangle a =
					result[i];

				for (
					int j = i + 1;
					j < result.Count;
					j++)
				{
					SolidRectangle b =
						result[j];

					// ------------------------------------------------
					// Horizontal merge.
					// ------------------------------------------------

					if (
						a.Y == b.Y &&
						a.Height == b.Height)
					{
						if (
							a.X +
							a.Width ==
							b.X)
						{
							a.Width += b.Width;

							result[i] = a;
							result.RemoveAt(j);

							changed = true;
							break;
						}

						if (
							b.X +
							b.Width ==
							a.X)
						{
							a.X = b.X;
							a.Width += b.Width;

							result[i] = a;
							result.RemoveAt(j);

							changed = true;
							break;
						}
					}

					// ------------------------------------------------
					// Vertical merge.
					// ------------------------------------------------

					if (
						a.X == b.X &&
						a.Width == b.Width)
					{
						if (
							a.Y +
							a.Height ==
							b.Y)
						{
							a.Height += b.Height;

							result[i] = a;
							result.RemoveAt(j);

							changed = true;
							break;
						}

						if (
							b.Y +
							b.Height ==
							a.Y)
						{
							a.Y = b.Y;
							a.Height += b.Height;

							result[i] = a;
							result.RemoveAt(j);

							changed = true;
							break;
						}
					}
				}
			}
		}

		return result;
	}

	// ============================================================
	// Debug boundary
	// ============================================================

	private void BuildDebugBoundary(
		HashSet<Vector2I> solid)
	{
		debugEdges.Clear();

		foreach (
			Vector2I p in solid)
		{
			int x = p.X;
			int y = p.Y;

			// Top.
			if (
				!solid.Contains(
					new Vector2I(
						x,
						y - 1
					)
				))
			{
				AddDebugEdge(
					new Vector2(
						x,
						y
					),
					new Vector2(
						x + 1,
						y
					)
				);
			}

			// Right.
			if (
				!solid.Contains(
					new Vector2I(
						x + 1,
						y
					)
				))
			{
				AddDebugEdge(
					new Vector2(
						x + 1,
						y
					),
					new Vector2(
						x + 1,
						y + 1
					)
				);
			}

			// Bottom.
			if (
				!solid.Contains(
					new Vector2I(
						x,
						y + 1
					)
				))
			{
				AddDebugEdge(
					new Vector2(
						x + 1,
						y + 1
					),
					new Vector2(
						x,
						y + 1
					)
				);
			}

			// Left.
			if (
				!solid.Contains(
					new Vector2I(
						x - 1,
						y
					)
				))
			{
				AddDebugEdge(
					new Vector2(
						x,
						y + 1
					),
					new Vector2(
						x,
						y
					)
				);
			}
		}
	}

	private void AddDebugEdge(
		Vector2 a,
		Vector2 b)
	{
		if (!ShowDebugGeometry)
			return;

		debugEdges.Add(
			new DebugEdge(
				ToSimulationSpace(a),
				ToSimulationSpace(b)
			)
		);
	}

	// ============================================================
	// Polygon area
	// ============================================================

	private static float PolygonArea(
		Vector2[] polygon)
	{
		if (
			polygon == null ||
			polygon.Length < 3)
		{
			return 0.0f;
		}

		float area = 0.0f;

		for (
			int i = 0;
			i < polygon.Length;
			i++)
		{
			Vector2 a =
				polygon[i];

			Vector2 b =
				polygon[
					(i + 1) %
					polygon.Length
				];

			area +=
				a.X * b.Y -
				b.X * a.Y;
		}

		return area * 0.5f;
	}

	// ============================================================
	// Tile size
	// ============================================================

	private Vector2 GetTileSize()
	{
		if (
			environment != null &&
			environment.TileSet != null)
		{
			Vector2I size =
				environment.TileSet.TileSize;

			if (
				size.X > 0 &&
				size.Y > 0)
			{
				return new Vector2(
					size.X,
					size.Y
				);
			}
		}

		return new Vector2(
			32.0f,
			32.0f
		);
	}

	// ============================================================
	// TileMap -> Simulation
	// ============================================================

	private Vector2
		ToSimulationSpace(
			Vector2 tileMapLocal)
	{
		Vector2 mainViewportPoint =
			environment.ToGlobal(
				tileMapLocal
			);

		Vector2 viewportPoint =
			mainViewportPoint -
			gameView.GlobalPosition;

		Vector2 viewportSize =
			new Vector2(
				simulationViewport.Size.X,
				simulationViewport.Size.Y
			);

		Vector2 screenCenter =
			viewportSize *
			0.5f;

		Vector2 cameraCenter =
			simulationCamera.GetScreenCenterPosition();

		return
			cameraCenter +
			(
				viewportPoint -
				screenCenter
			);
	}

	// ============================================================
	// Simulation -> local
	// ============================================================

	private Vector2
		SimulationToThisLocal(
			Vector2 simulationPoint)
	{
		Vector2 viewportSize =
			new Vector2(
				simulationViewport.Size.X,
				simulationViewport.Size.Y
			);

		Vector2 screenCenter =
			viewportSize *
			0.5f;

		Vector2 cameraCenter =
			simulationCamera.GetScreenCenterPosition();

		Vector2 viewportPoint =
			screenCenter +
			(
				simulationPoint -
				cameraCenter
			);

		Vector2 mainViewportPoint =
			viewportPoint +
			gameView.GlobalPosition;

		return ToLocal(
			mainViewportPoint
		);
	}

	// ============================================================
	// Find fluid simulator
	// ============================================================

	private static FluidSimulator
		FindFluidSimulator(
			Node node)
	{
		if (node is FluidSimulator)
		{
			return (FluidSimulator)node;
		}

		foreach (
			Node child in node.GetChildren())
		{
			FluidSimulator result =
				FindFluidSimulator(
					child
				);

			if (result != null)
			{
				return result;
			}
		}

		return null;
	}

	// ============================================================
	// Rebuild
	// ============================================================

	public void Rebuild()
	{
		if (generating)
			return;

		GenerateColliders();
	}

	// ============================================================
	// Information
	// ============================================================

	public int GeneratedColliderCount
	{
		get
		{
			return generatedColliders.Count;
		}
	}

	public bool IsGenerated
	{
		get
		{
			return generated;
		}
	}

	// ============================================================
	// Exit
	// ============================================================

	public override void _ExitTree()
	{
		generated = false;

		generatedColliders.Clear();
		debugEdges.Clear();

		environment = null;
		simulator = null;
		solver = null;
		gameView = null;
		simulationViewport = null;
		simulationCamera = null;
	}
}
