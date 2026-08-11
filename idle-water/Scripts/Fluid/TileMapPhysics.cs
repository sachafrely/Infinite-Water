using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;

/// <summary>
/// Generates collision geometry for the Environment TileMapLayer.
///
/// The TileMap is converted into one global alpha mask.
/// Exposed edges are extracted and merged into longer segments.
/// Each segment becomes a thick convex polygon collider.
///
/// This avoids:
///   - Tile-to-tile collision seams
///   - Thousands of tiny colliders
///   - Concave FluidPolygonCollider shapes
///   - Triangulation problems
///
/// The generated collision exists in PBF simulation coordinates.
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

	[Export]
	public float CollisionThickness { get; set; } = 8.0f;

	[Export]
	public bool GenerateOnReady { get; set; } = true;

	[Export]
	public bool RebuildWhenChanged { get; set; } = false;

	[Export]
	public bool DebugOutput { get; set; } = true;

	[Export]
	public bool ShowDebugGeometry { get; set; } = false;

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

	/// <summary>
	/// Alpha >= this value is considered solid.
	/// </summary>
	[Export]
	public float AlphaThreshold { get; set; } = 0.01f;

	/// <summary>
	/// Small dilation applied to the collision mask.
	/// </summary>
	[Export]
	public int CollisionSealPixels { get; set; } = 1;

	/// <summary>
	/// Simplification tolerance in simulation pixels.
	/// </summary>
	[Export]
	public float ContourSimplification { get; set; } = 1.5f;

	/// <summary>
	/// Ignore very small contour loops.
	/// </summary>
	[Export]
	public float MinimumContourArea { get; set; } = 4.0f;

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
	// Grid edge
	// ============================================================

	private struct GridEdge : IEquatable<GridEdge>
	{
		public Vector2I A;
		public Vector2I B;

		public GridEdge(
			Vector2I a,
			Vector2I b)
		{
			A = a;
			B = b;
		}

		public bool Equals(
			GridEdge other)
		{
			return A == other.A &&
				   B == other.B;
		}

		public override bool Equals(
			object obj)
		{
			return obj is GridEdge &&
				   Equals((GridEdge)obj);
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(
				A,
				B
			);
		}
	}

	// ============================================================
	// Ready
	// ============================================================

	public override void _Ready()
	{
		GD.Print(
			"TileMapPhysics: _Ready()"
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
				2.0f
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
				return result;
		}

		return null;
	}

	// ============================================================
	// Viewport mapping
	// ============================================================

	private void FindViewportMapping()
	{
		// --------------------------------------------------------
		// GameView
		// --------------------------------------------------------

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

		// --------------------------------------------------------
		// Simulation viewport
		// --------------------------------------------------------

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

		// --------------------------------------------------------
		// Camera
		// --------------------------------------------------------

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
			return (T)node;

		foreach (
			Node child in node.GetChildren())
		{
			T result =
				FindNodeOfType<T>(
					child
				);

			if (result != null)
				return result;
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
		if (generating)
			return;

		if (environment == null)
			environment = GetEnvironment();

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
		// Remove old collision.
		// --------------------------------------------------------

		solver.ClearPolygonColliders();

		generatedColliders.Clear();
		debugEdges.Clear();

		GD.Print(
			"TileMapPhysics: Generating collision..."
		);

		// --------------------------------------------------------
		// Build global solid mask.
		// --------------------------------------------------------

		HashSet<Vector2I> solidPixels =
			BuildGlobalSolidMask();

		if (solidPixels.Count == 0)
		{
			GD.PushWarning(
				"TileMapPhysics: No opaque pixels found."
			);

			generating = false;
			return;
		}

		// --------------------------------------------------------
		// Seal tiny gaps.
		// --------------------------------------------------------

		if (CollisionSealPixels > 0)
		{
			solidPixels =
				DilateMask(
					solidPixels,
					CollisionSealPixels
				);
		}

		// --------------------------------------------------------
		// Extract global boundary.
		// --------------------------------------------------------

		List<List<Vector2I>> loops =
			ExtractBoundaryLoops(
				solidPixels
			);

		if (loops.Count == 0)
		{
			GD.PushWarning(
				"TileMapPhysics: No boundary loops found."
			);

			generating = false;
			return;
		}

		int totalSegments = 0;

		// ========================================================
		// Process every contour.
		// ========================================================

		foreach (
			List<Vector2I> loop in loops)
		{
			if (loop.Count < 3)
				continue;

			List<Vector2> contour =
				new List<Vector2>(
					loop.Count
				);

			foreach (
				Vector2I point in loop)
			{
				contour.Add(
					GridToTileMapLocal(
						point
					)
				);
			}

			// ----------------------------------------------------
			// Simplify the closed contour.
			// ----------------------------------------------------

			contour =
				SimplifyClosedPolygon(
					contour,
					ContourSimplification
				);

			if (contour.Count < 3)
				continue;

			float area =
				Mathf.Abs(
					PolygonArea(
						contour
					)
				);

			if (
				area <
				MinimumContourArea)
			{
				continue;
			}

			// ----------------------------------------------------
			// IMPORTANT:
			//
			// We deliberately do NOT triangulate the contour.
			//
			// FluidPolygonCollider works extremely well with
			// convex segment strips. Each boundary segment is
			// converted into one thick quadrilateral.
			// ----------------------------------------------------

			for (
				int i = 0;
				i < contour.Count;
				i++)
			{
				Vector2 localA =
					contour[i];

				Vector2 localB =
					contour[
						(i + 1) %
						contour.Count
					];

				Vector2 simulationA =
					ToSimulationSpace(
						localA
					);

				Vector2 simulationB =
					ToSimulationSpace(
						localB
					);

				Vector2 difference =
					simulationB -
					simulationA;

				float lengthSquared =
					difference.LengthSquared();

				if (
					lengthSquared <
					0.0001f)
				{
					continue;
				}

				// ------------------------------------------------
				// Build thick convex collision strip.
				// ------------------------------------------------

				Vector2[] polygon =
					BuildSegmentPolygon(
						simulationA,
						simulationB,
						Mathf.Max(
							CollisionThickness,
							6.0f
						)
					);

				if (polygon.Length < 4)
					continue;

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

				totalSegments++;

				// ------------------------------------------------
				// Debug line.
				// ------------------------------------------------

				debugEdges.Add(
					new DebugEdge(
						simulationA,
						simulationB
					)
				);
			}
		}

		generated = true;
		generating = false;

		if (DebugOutput)
		{
			GD.Print(
				"========================================"
			);

			GD.Print(
				"TileMapPhysics GLOBAL ALPHA COLLISION"
			);

			GD.Print(
				"Used cells: " +
				environment.GetUsedCells().Count
			);

			GD.Print(
				"Global solid pixels: " +
				solidPixels.Count
			);

			GD.Print(
				"Boundary loops: " +
				loops.Count
			);

			GD.Print(
				"Generated collision segments: " +
				totalSegments
			);

			GD.Print(
				"PBF colliders: " +
				generatedColliders.Count
			);

			GD.Print(
				"Alpha threshold: " +
				AlphaThreshold
			);

			GD.Print(
				"Collision seal pixels: " +
				CollisionSealPixels
			);

			GD.Print(
				"Contour simplification: " +
				ContourSimplification
			);

			GD.Print(
				"Collision thickness: " +
				Mathf.Max(
					CollisionThickness,
					6.0f
				)
			);

			GD.Print(
				"Tile size: " +
				GetTileSize()
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
		int opaquePixels = 0;

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

			// ----------------------------------------------------
			// Actual TileMap cell center.
			//
			// This is important for TileMapLayer transforms and
			// for the 32x32 tileset.
			// ----------------------------------------------------

			Vector2 cellCenter =
				environment.MapToLocal(
					cell
				);

			Vector2 tileTopLeft =
				cellCenter -
				tileSize * 0.5f;

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

					opaquePixels++;
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
				"TileMapPhysics: opaque pixels = " +
				opaquePixels
			);
		}

		return solidPixels;
	}

	// ============================================================
	// Dilate mask
	// ============================================================

	private static HashSet<Vector2I>
		DilateMask(
			HashSet<Vector2I> source,
			int radius)
	{
		if (
			radius <= 0 ||
			source.Count == 0)
		{
			return source;
		}

		HashSet<Vector2I> result =
			new HashSet<Vector2I>(
				source
			);

		foreach (
			Vector2I p in source)
		{
			for (
				int y = -radius;
				y <= radius;
				y++)
			{
				for (
					int x = -radius;
					x <= radius;
					x++)
				{
					result.Add(
						new Vector2I(
							p.X + x,
							p.Y + y
						)
					);
				}
			}
		}

		return result;
	}

	// ============================================================
	// Extract boundary loops
	// ============================================================

	private List<List<Vector2I>>
		ExtractBoundaryLoops(
			HashSet<Vector2I> solid)
	{
		HashSet<GridEdge> edges =
			new HashSet<GridEdge>();

		foreach (
			Vector2I p in solid)
		{
			int x = p.X;
			int y = p.Y;

			// ----------------------------------------------------
			// Top
			// ----------------------------------------------------

			if (
				!solid.Contains(
					new Vector2I(
						x,
						y - 1
					)
				))
			{
				AddBoundaryEdge(
					edges,
					new Vector2I(
						x,
						y
					),
					new Vector2I(
						x + 1,
						y
					)
				);
			}

			// ----------------------------------------------------
			// Right
			// ----------------------------------------------------

			if (
				!solid.Contains(
					new Vector2I(
						x + 1,
						y
					)
				))
			{
				AddBoundaryEdge(
					edges,
					new Vector2I(
						x + 1,
						y
					),
					new Vector2I(
						x + 1,
						y + 1
					)
				);
			}

			// ----------------------------------------------------
			// Bottom
			// ----------------------------------------------------

			if (
				!solid.Contains(
					new Vector2I(
						x,
						y + 1
					)
				))
			{
				AddBoundaryEdge(
					edges,
					new Vector2I(
						x + 1,
						y + 1
					),
					new Vector2I(
						x,
						y + 1
					)
				);
			}

			// ----------------------------------------------------
			// Left
			// ----------------------------------------------------

			if (
				!solid.Contains(
					new Vector2I(
						x - 1,
						y
					)
				))
			{
				AddBoundaryEdge(
					edges,
					new Vector2I(
						x,
						y + 1
					),
					new Vector2I(
						x,
						y
					)
				);
			}
		}

		// --------------------------------------------------------
		// Build adjacency.
		// --------------------------------------------------------

		Dictionary<Vector2I, List<Vector2I>>
			nextMap =
				new Dictionary<
					Vector2I,
					List<Vector2I>
				>();

		foreach (
			GridEdge edge in edges)
		{
			List<Vector2I> list;

			if (
				!nextMap.TryGetValue(
					edge.A,
					out list
				))
			{
				list =
					new List<Vector2I>();

				nextMap[edge.A] =
					list;
			}

			list.Add(
				edge.B
			);
		}

		HashSet<GridEdge> remaining =
			new HashSet<GridEdge>(
				edges
			);

		List<List<Vector2I>> loops =
			new List<List<Vector2I>>();

		while (remaining.Count > 0)
		{
			GridEdge first =
				default;

			foreach (
				GridEdge edge in remaining)
			{
				first = edge;
				break;
			}

			List<Vector2I> loop =
				new List<Vector2I>();

			Vector2I start =
				first.A;

			Vector2I current =
				first.A;

			Vector2I next =
				first.B;

			RemoveEdge(
				remaining,
				current,
				next
			);

			loop.Add(current);

			int safety = 0;

			while (
				next != start &&
				safety < 100000)
			{
				safety++;

				loop.Add(next);

				current =
					next;

				Vector2I candidate =
					default;

				bool found =
					false;

				List<Vector2I> candidates;

				if (
					nextMap.TryGetValue(
						current,
						out candidates
					))
				{
					foreach (
						Vector2I c in candidates)
					{
						GridEdge e =
							new GridEdge(
								current,
								c
							);

						if (remaining.Contains(e))
						{
							candidate = c;
							found = true;
							break;
						}
					}
				}

				if (!found)
					break;

				next =
					candidate;

				RemoveEdge(
					remaining,
					current,
					next
				);
			}

			if (
				loop.Count >= 3 &&
				next == start)
			{
				loops.Add(
					loop
				);
			}
		}

		return loops;
	}

	// ============================================================
	// Boundary edge
	// ============================================================

	private static void AddBoundaryEdge(
		HashSet<GridEdge> edges,
		Vector2I a,
		Vector2I b)
	{
		GridEdge edge =
			new GridEdge(
				a,
				b
			);

		GridEdge reverse =
			new GridEdge(
				b,
				a
			);

		if (edges.Contains(reverse))
		{
			edges.Remove(reverse);
			return;
		}

		edges.Add(edge);
	}

	private static void RemoveEdge(
		HashSet<GridEdge> edges,
		Vector2I a,
		Vector2I b)
	{
		edges.Remove(
			new GridEdge(
				a,
				b
			)
		);
	}

	// ============================================================
	// Simplify closed polygon
	// ============================================================

	private static List<Vector2>
		SimplifyClosedPolygon(
			List<Vector2> polygon,
			float tolerance)
	{
		if (
			polygon.Count <= 3 ||
			tolerance <= 0.0f)
		{
			return polygon;
		}

		List<Vector2> result =
			new List<Vector2>(
				polygon
			);

		bool changed = true;

		float toleranceSquared =
			tolerance *
			tolerance;

		while (
			changed &&
			result.Count > 3)
		{
			changed = false;

			for (
				int i = 0;
				i < result.Count;
				i++)
			{
				Vector2 previous =
					result[
						(i - 1 +
						 result.Count) %
						result.Count
					];

				Vector2 current =
					result[i];

				Vector2 next =
					result[
						(i + 1) %
						result.Count
					];

				float distance =
					DistancePointToSegmentSquared(
						current,
						previous,
						next
					);

				if (
					distance <=
					toleranceSquared)
				{
					result.RemoveAt(i);
					changed = true;
					break;
				}
			}
		}

		return result;
	}

	private static float
		DistancePointToSegmentSquared(
			Vector2 p,
			Vector2 a,
			Vector2 b)
	{
		Vector2 ab =
			b - a;

		float lengthSquared =
			ab.LengthSquared();

		if (lengthSquared <= 0.000001f)
		{
			return p.DistanceSquaredTo(a);
		}

		float t =
			(p - a).Dot(ab) /
			lengthSquared;

		t =
			Mathf.Clamp(
				t,
				0.0f,
				1.0f
			);

		Vector2 closest =
			a +
			ab * t;

		return p.DistanceSquaredTo(
			closest
		);
	}

	// ============================================================
	// Polygon area
	// ============================================================

	private static float PolygonArea(
		List<Vector2> polygon)
	{
		float area = 0.0f;

		for (
			int i = 0;
			i < polygon.Count;
			i++)
		{
			Vector2 a =
				polygon[i];

			Vector2 b =
				polygon[
					(i + 1) %
					polygon.Count
				];

			area +=
				a.X * b.Y -
				b.X * a.Y;
		}

		return area * 0.5f;
	}

	// ============================================================
	// Grid -> TileMap local
	// ============================================================

	private static Vector2
		GridToTileMapLocal(
			Vector2I point)
	{
		return new Vector2(
			point.X,
			point.Y
		);
	}

	// ============================================================
	// Build thick convex segment polygon
	// ============================================================

	private static Vector2[]
		BuildSegmentPolygon(
			Vector2 a,
			Vector2 b,
			float thickness)
	{
		Vector2 direction =
			b - a;

		float lengthSquared =
			direction.LengthSquared();

		if (
			lengthSquared <=
			0.000001f)
		{
			return Array.Empty<Vector2>();
		}

		float inverseLength =
			1.0f /
			Mathf.Sqrt(
				lengthSquared
			);

		direction *=
			inverseLength;

		Vector2 normal =
			new Vector2(
				-direction.Y,
				direction.X
			);

		float halfThickness =
			Mathf.Max(
				3.0f,
				thickness * 0.5f
			);

		Vector2 offset =
			normal *
			halfThickness;

		return new[]
		{
			a - offset,
			a + offset,
			b + offset,
			b - offset
		};
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
			16.0f,
			16.0f
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
	// Simulation -> this local
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
	// Fluid simulator
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
				return result;
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
