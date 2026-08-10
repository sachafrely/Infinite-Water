
using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;

/// <summary>
/// Generates PBF collision geometry from the visual TileMapLayer.
///
/// Opaque atlas pixels are treated as solid.
/// Transparent atlas pixels are treated as empty.
///
/// Collision is generated from the actual alpha mask of every
/// used tile. Exposed edges are merged and converted into thick
/// polygon colliders.
///
/// The collision geometry is intentionally thicker than the
/// visual edge. This prevents PBF particles from tunneling through
/// thin tile boundaries between solver iterations.
///
/// Coordinate hierarchy:
///
/// Main viewport
///     ├── Environment
///     └── GameView
///           └── SimulationViewport
///                 └── FluidSimulation
///
/// Collision coordinates:
///
/// TileMap local
///      ↓
/// Main viewport/global
///      ↓
/// GameView local
///      ↓
/// Simulation world
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
	/// Thickness of the generated collision strips.
	///
	/// This is deliberately larger than the visible edge so that
	/// particles cannot tunnel through the collider during a
	/// single physics step.
	/// </summary>
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

	// ============================================================
	// Constants
	// ============================================================

	private static readonly Vector2 DefaultTileSize =
		new Vector2(
			16.0f,
			16.0f
		);

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
	// Pixel edge
	// ============================================================

	private struct PixelEdge : IEquatable<PixelEdge>
	{
		public Vector2I Start;

		public Vector2I End;

		public PixelEdge(
			Vector2I start,
			Vector2I end)
		{
			Start = start;
			End = end;
		}

		public bool Equals(
			PixelEdge other)
		{
			return
				Start == other.Start &&
				End == other.End;
		}

		public override bool Equals(
			object obj)
		{
			if (!(obj is PixelEdge))
			{
				return false;
			}

			return Equals(
				(PixelEdge)obj
			);
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(
				Start,
				End
			);
		}
	}

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
	// Initialization
	// ============================================================

	private void Initialize()
	{
		if (generating)
		{
			return;
		}

		GD.Print(
			"TileMapPhysics: Initialize()"
		);

		// --------------------------------------------------------
		// Environment
		// --------------------------------------------------------

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
		// Fluid simulator
		// --------------------------------------------------------

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

		// --------------------------------------------------------
		// Solver
		// --------------------------------------------------------

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

		// --------------------------------------------------------
		// Viewport mapping
		// --------------------------------------------------------

		FindViewportMapping();

		if (
			gameView == null ||
			simulationViewport == null ||
			simulationCamera == null)
		{
			GD.PushError(
				"TileMapPhysics: Could not establish " +
				"Environment -> SimulationViewport mapping."
			);

			return;
		}

		// --------------------------------------------------------
		// Generate
		// --------------------------------------------------------

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
		{
			return;
		}

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
	// Find Environment
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

	// ============================================================
	// Find TileMapLayer
	// ============================================================

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
	// Find viewport mapping
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

	// ============================================================
	// Find node of type
	// ============================================================

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
	// Get solver
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
		{
			return;
		}

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
		// Remove old colliders from solver.
		// --------------------------------------------------------

		solver.ClearPolygonColliders();

		generatedColliders.Clear();

		debugEdges.Clear();

		GD.Print(
			"TileMapPhysics: Generating collision..."
		);

		// --------------------------------------------------------
		// Build alpha edges.
		// --------------------------------------------------------

		HashSet<PixelEdge> pixelEdges =
			BuildAlphaCollisionEdges();

		if (pixelEdges.Count == 0)
		{
			GD.PushWarning(
				"TileMapPhysics: No opaque pixels were found."
			);

			generating = false;

			return;
		}

		// --------------------------------------------------------
		// Merge edges.
		// --------------------------------------------------------

		List<PixelEdge> mergedEdges =
			MergePixelEdges(
				pixelEdges
			);

		// --------------------------------------------------------
		// Generate collision polygons.
		// --------------------------------------------------------

		for (
			int i = 0;
			i < mergedEdges.Count;
			i++)
		{
			PixelEdge edge =
				mergedEdges[i];

			Vector2 localA =
				PixelCoordinateToTileMapLocal(
					edge.Start
				);

			Vector2 localB =
				PixelCoordinateToTileMapLocal(
					edge.End
				);

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
				difference.X *
				difference.X +
				difference.Y *
				difference.Y;

			if (
				lengthSquared <
				0.0001f)
			{
				continue;
			}

			// ----------------------------------------------------
			// Important:
			//
			// The collision strip is intentionally thick.
			//
			// A thin 1-2 pixel strip can be crossed by a PBF
			// particle between solver iterations.
			// ----------------------------------------------------

			Vector2[] polygon =
				BuildSegmentPolygon(
					simulationA,
					simulationB,
					Mathf.Max(
						CollisionThickness,
						6.0f
					)
				);

			if (polygon.Length < 3)
			{
				continue;
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

			debugEdges.Add(
				new DebugEdge(
					simulationA,
					simulationB
				)
			);
		}

		generated = true;

		generating = false;

		// --------------------------------------------------------
		// Diagnostics
		// --------------------------------------------------------

		if (DebugOutput)
		{
			GD.Print(
				"========================================"
			);

			GD.Print(
				"TileMapPhysics ALPHA COLLISION"
			);

			GD.Print(
				"Used cells: " +
				environment.GetUsedCells().Count
			);

			GD.Print(
				"Alpha edges: " +
				pixelEdges.Count
			);

			GD.Print(
				"Merged edges: " +
				mergedEdges.Count
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
	// Build alpha collision edges
	// ============================================================

	private HashSet<PixelEdge>
		BuildAlphaCollisionEdges()
	{
		HashSet<PixelEdge> result =
			new HashSet<PixelEdge>();

		if (environment.TileSet == null)
		{
			GD.PushError(
				"TileMapPhysics: Environment has no TileSet."
			);

			return result;
		}

		TileSet tileSet =
			environment.TileSet;

		Godot.Collections.Array<Vector2I> cells =
			environment.GetUsedCells();

		int processedCells = 0;

		int opaquePixels = 0;

		Vector2 tileSize =
			GetTileSize();

		foreach (
			Vector2I cell in cells)
		{
			int sourceId =
				environment.GetCellSourceId(
					cell
				);

			if (sourceId < 0)
			{
				continue;
			}

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
			{
				continue;
			}

			TileSetAtlasSource atlas =
				(TileSetAtlasSource)source;

			if (
				!atlas.HasTile(
					atlasCoords
				))
			{
				continue;
			}

			Rect2I region =
				atlas.GetTileTextureRegion(
					atlasCoords
				);

			Texture2D texture =
				atlas.Texture;

			if (texture == null)
			{
				continue;
			}

			Image image =
				texture.GetImage();

			if (image == null)
			{
				continue;
			}

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
					1.0f,
					width
				);

			float scaleY =
				tileSize.Y /
				Mathf.Max(
					1.0f,
					height
				);

			bool[,] solid =
				new bool[
					width,
					height
				];

			// ----------------------------------------------------
			// Read alpha.
			// ----------------------------------------------------

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
						pixel.A >=
						AlphaThreshold)
					{
						solid[x, y] = true;

						opaquePixels++;
					}
				}
			}

			// ----------------------------------------------------
			// IMPORTANT:
			//
			// Use the actual TileMap cell position.
			//
			// Do not use texture coordinates for the tile
			// position. The atlas texture can be completely
			// different from the TileMap's world position.
			// ----------------------------------------------------

			int baseX =
				Mathf.RoundToInt(
					cell.X *
					tileSize.X
				);

			int baseY =
				Mathf.RoundToInt(
					cell.Y *
					tileSize.Y
				);

			// ----------------------------------------------------
			// Extract exposed alpha edges.
			// ----------------------------------------------------

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
					if (!solid[x, y])
					{
						continue;
					}

					// ------------------------------------------------
					// Top
					// ------------------------------------------------

					if (
						y == 0 ||
						!solid[x, y - 1])
					{
						AddPixelEdge(
							result,
							new Vector2I(
								baseX +
								Mathf.RoundToInt(
									x * scaleX
								),
								baseY +
								Mathf.RoundToInt(
									y * scaleY
								)
							),
							new Vector2I(
								baseX +
								Mathf.RoundToInt(
									(x + 1) * scaleX
								),
								baseY +
								Mathf.RoundToInt(
									y * scaleY
								)
							)
						);
					}

					// ------------------------------------------------
					// Right
					// ------------------------------------------------

					if (
						x == width - 1 ||
						!solid[x + 1, y])
					{
						AddPixelEdge(
							result,
							new Vector2I(
								baseX +
								Mathf.RoundToInt(
									(x + 1) * scaleX
								),
								baseY +
								Mathf.RoundToInt(
									y * scaleY
								)
							),
							new Vector2I(
								baseX +
								Mathf.RoundToInt(
									(x + 1) * scaleX
								),
								baseY +
								Mathf.RoundToInt(
									(y + 1) * scaleY
								)
							)
						);
					}

					// ------------------------------------------------
					// Bottom
					// ------------------------------------------------

					if (
						y == height - 1 ||
						!solid[x, y + 1])
					{
						AddPixelEdge(
							result,
							new Vector2I(
								baseX +
								Mathf.RoundToInt(
									(x + 1) * scaleX
								),
								baseY +
								Mathf.RoundToInt(
									(y + 1) * scaleY
								)
							),
							new Vector2I(
								baseX +
								Mathf.RoundToInt(
									x * scaleX
								),
								baseY +
								Mathf.RoundToInt(
									(y + 1) * scaleY
								)
							)
						);
					}

					// ------------------------------------------------
					// Left
					// ------------------------------------------------

					if (
						x == 0 ||
						!solid[x - 1, y])
					{
						AddPixelEdge(
							result,
							new Vector2I(
								baseX +
								Mathf.RoundToInt(
									x * scaleX
								),
								baseY +
								Mathf.RoundToInt(
									(y + 1) * scaleY
								)
							),
							new Vector2I(
								baseX +
								Mathf.RoundToInt(
									x * scaleX
								),
								baseY +
								Mathf.RoundToInt(
									y * scaleY
								)
							)
						);
					}
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

		return result;
	}

	// ============================================================
	// Add pixel edge
	// ============================================================

	private static void AddPixelEdge(
		HashSet<PixelEdge> edges,
		Vector2I a,
		Vector2I b)
	{
		if (a == b)
		{
			return;
		}

		PixelEdge edge =
			new PixelEdge(
				a,
				b
			);

		PixelEdge reverse =
			new PixelEdge(
				b,
				a
			);

		// --------------------------------------------------------
		// If the opposite edge already exists, the edge is
		// internal between two opaque pixels/tiles.
		//
		// Remove it so water cannot collide with an internal
		// seam while the outside boundary remains solid.
		// --------------------------------------------------------

		if (edges.Contains(reverse))
		{
			edges.Remove(reverse);

			return;
		}

		edges.Add(edge);
	}

	// ============================================================
	// Merge pixel edges
	// ============================================================

	private List<PixelEdge> MergePixelEdges(
		HashSet<PixelEdge> input)
	{
		List<PixelEdge> horizontal =
			new List<PixelEdge>();

		List<PixelEdge> vertical =
			new List<PixelEdge>();

		foreach (
			PixelEdge edge in input)
		{
			if (
				edge.Start.Y ==
				edge.End.Y)
			{
				horizontal.Add(
					NormalizeHorizontal(
						edge
					)
				);
			}
			else
			{
				vertical.Add(
					NormalizeVertical(
						edge
					)
				);
			}
		}

		List<PixelEdge> result =
			new List<PixelEdge>();

		result.AddRange(
			MergeHorizontalEdges(
				horizontal
			)
		);

		result.AddRange(
			MergeVerticalEdges(
				vertical
			)
		);

		return result;
	}

	// ============================================================
	// Normalize horizontal
	// ============================================================

	private static PixelEdge NormalizeHorizontal(
		PixelEdge edge)
	{
		if (
			edge.Start.X <=
			edge.End.X)
		{
			return edge;
		}

		return new PixelEdge(
			edge.End,
			edge.Start
		);
	}

	// ============================================================
	// Normalize vertical
	// ============================================================

	private static PixelEdge NormalizeVertical(
		PixelEdge edge)
	{
		if (
			edge.Start.Y <=
			edge.End.Y)
		{
			return edge;
		}

		return new PixelEdge(
			edge.End,
			edge.Start
		);
	}

	// ============================================================
	// Merge horizontal
	// ============================================================

	private static List<PixelEdge>
		MergeHorizontalEdges(
			List<PixelEdge> edges)
	{
		List<PixelEdge> result =
			new List<PixelEdge>();

		edges.Sort(
			(a, b) =>
			{
				int y =
					a.Start.Y.CompareTo(
						b.Start.Y
					);

				if (y != 0)
				{
					return y;
				}

				return a.Start.X.CompareTo(
					b.Start.X
				);
			}
		);

		foreach (
			PixelEdge edge in edges)
		{
			if (result.Count == 0)
			{
				result.Add(edge);

				continue;
			}

			int lastIndex =
				result.Count - 1;

			PixelEdge last =
				result[lastIndex];

			if (
				last.Start.Y ==
					edge.Start.Y &&
				last.End.X ==
					edge.Start.X)
			{
				result[lastIndex] =
					new PixelEdge(
						last.Start,
						edge.End
					);
			}
			else
			{
				result.Add(edge);
			}
		}

		return result;
	}

	// ============================================================
	// Merge vertical
	// ============================================================

	private static List<PixelEdge>
		MergeVerticalEdges(
			List<PixelEdge> edges)
	{
		List<PixelEdge> result =
			new List<PixelEdge>();

		edges.Sort(
			(a, b) =>
			{
				int x =
					a.Start.X.CompareTo(
						b.Start.X
					);

				if (x != 0)
				{
					return x;
				}

				return a.Start.Y.CompareTo(
					b.Start.Y
				);
			}
		);

		foreach (
			PixelEdge edge in edges)
		{
			if (result.Count == 0)
			{
				result.Add(edge);

				continue;
			}

			int lastIndex =
				result.Count - 1;

			PixelEdge last =
				result[lastIndex];

			if (
				last.Start.X ==
					edge.Start.X &&
				last.End.Y ==
					edge.Start.Y)
			{
				result[lastIndex] =
					new PixelEdge(
						last.Start,
						edge.End
					);
			}
			else
			{
				result.Add(edge);
			}
		}

		return result;
	}

	// ============================================================
	// Pixel coordinate -> TileMap local
	// ============================================================

	private Vector2 PixelCoordinateToTileMapLocal(
		Vector2I pixel)
	{
		return new Vector2(
			pixel.X,
			pixel.Y
		);
	}

	// ============================================================
	// Get tile size
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

		return DefaultTileSize;
	}

	// ============================================================
	// Environment -> Simulation
	// ============================================================

	private Vector2 ToSimulationSpace(
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
			(viewportPoint - screenCenter);
	}

	// ============================================================
	// Simulation -> this node local
	// ============================================================

	private Vector2 SimulationToThisLocal(
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
			(simulationPoint - cameraCenter);

		Vector2 mainViewportPoint =
			viewportPoint +
			gameView.GlobalPosition;

		return ToLocal(
			mainViewportPoint
		);
	}

	// ============================================================
	// Build segment polygon
	// ============================================================

	private static Vector2[] BuildSegmentPolygon(
		Vector2 a,
		Vector2 b,
		float thickness)
	{
		Vector2 direction =
			b - a;

		float lengthSquared =
			direction.X *
			direction.X +
			direction.Y *
			direction.Y;

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
				thickness *
				0.5f
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
	// Find FluidSimulator
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
	// Public rebuild
	// ============================================================

	public void Rebuild()
	{
		if (generating)
		{
			return;
		}

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
