using System;
using Godot;

public class FluidWheelState
{
	private readonly Vector2 center;

	private float angle;
	private float angularVelocity;

	// ------------------------------------------------------------
	// Wheel tuning
	// ------------------------------------------------------------

	private const float TorqueScale = 0.00035f;
	private const float AngularDamping = 0.15f;
	private const float MaxAngularVelocity = 25.0f;

	private float accumulatedTorque;

	public Vector2 Center =>
		center;

	public float Angle =>
		angle;

	public float AngularVelocity =>
		angularVelocity;

	public FluidWheelState(
		Vector2 wheelCenter)
	{
		center =
			wheelCenter;

		angle =
			0.0f;

		angularVelocity =
			0.0f;

		accumulatedTorque =
			0.0f;
	}

	public void AddTorque(
		float torque)
	{
		accumulatedTorque +=
			torque;
	}

	public void Step(
		float dt)
	{
		if (dt <= 0.0f)
			return;

		// --------------------------------------------------------
		// Convert water torque into angular acceleration.
		// --------------------------------------------------------

		angularVelocity +=
			accumulatedTorque *
			TorqueScale *
			dt;

		accumulatedTorque =
			0.0f;

		// --------------------------------------------------------
		// Physical damping.
		// --------------------------------------------------------

		float damping =
			Mathf.Exp(
				-AngularDamping *
				dt
			);

		angularVelocity *=
			damping;

		// --------------------------------------------------------
		// Safety limit.
		// --------------------------------------------------------

		angularVelocity =
			Mathf.Clamp(
				angularVelocity,
				-MaxAngularVelocity,
				MaxAngularVelocity
			);

		// --------------------------------------------------------
		// Integrate angle.
		// --------------------------------------------------------

		angle +=
			angularVelocity *
			dt;

		// --------------------------------------------------------
		// Keep angle numerically small.
		// --------------------------------------------------------

		if (angle > Mathf.Tau)
			angle -= Mathf.Tau;

		if (angle < -Mathf.Tau)
			angle += Mathf.Tau;
	}

	public Vector2 GetSurfaceVelocity(
		Vector2 worldPosition)
	{
		Vector2 radius =
			worldPosition -
			center;

		return new Vector2(
			-angularVelocity *
			radius.Y,

			angularVelocity *
			radius.X
		);
	}
}

// ================================================================
// Polygon collider
// ================================================================

public class FluidPolygonCollider
{
	// ------------------------------------------------------------
	// Polygon
	// ------------------------------------------------------------

	private readonly Vector2[] localVertices;
	private readonly Vector2[] vertices;
	private readonly Vector2[] edges;
	private readonly Vector2[] edgeNormals;
	private readonly float[] edgeLengthSquared;

	private readonly bool counterClockwise;

	// ------------------------------------------------------------
	// Collision
	// ------------------------------------------------------------

	private const float CollisionMargin = 1.0f;
	private const float Epsilon = 0.000001f;

	// ------------------------------------------------------------
	// Swept collision
	// ------------------------------------------------------------

	private const float SweptStep = 1.5f;
	private const int MaxSweptSteps = 64;

	// ------------------------------------------------------------
	// AABB
	// ------------------------------------------------------------

	private float minX;
	private float maxX;
	private float minY;
	private float maxY;

	// ------------------------------------------------------------
	// Wheel
	// ------------------------------------------------------------

	private FluidWheelState wheel;

	public bool IsWheel =>
		wheel != null;

	public FluidWheelState Wheel =>
		wheel;

	// ------------------------------------------------------------
	// Constructor
	// ------------------------------------------------------------

	public FluidPolygonCollider(
		Vector2[] polygon)
	{
		if (
			polygon == null ||
			polygon.Length < 3)
		{
			throw new ArgumentException(
				"Polygon collider requires at least 3 vertices."
			);
		}

		int count =
			polygon.Length;

		localVertices =
			new Vector2[count];

		vertices =
			new Vector2[count];

		edges =
			new Vector2[count];

		edgeNormals =
			new Vector2[count];

		edgeLengthSquared =
			new float[count];

		Array.Copy(
			polygon,
			localVertices,
			count
		);

		Array.Copy(
			polygon,
			vertices,
			count
		);

		counterClockwise =
			CalculateSignedArea() >
			0.0f;

		RebuildGeometry();
	}

	// ------------------------------------------------------------
	// Configure as wheel
	// ------------------------------------------------------------

	public void ConfigureAsWheel(
		FluidWheelState wheelState)
	{
		wheel =
			wheelState;

		UpdateWheelGeometry();
	}

	// ------------------------------------------------------------
	// Rotate wheel geometry
	// ------------------------------------------------------------

	public void UpdateWheelGeometry()
	{
		if (wheel == null)
			return;

		Vector2 center =
			wheel.Center;

		float angle =
			wheel.Angle;

		float cos =
			Mathf.Cos(angle);

		float sin =
			Mathf.Sin(angle);

		for (
			int i = 0;
			i < localVertices.Length;
			i++)
		{
			Vector2 local =
				localVertices[i];

			vertices[i] =
				center +
				new Vector2(
					local.X * cos -
					local.Y * sin,

					local.X * sin +
					local.Y * cos
				);
		}

		RebuildGeometry();
	}

	// ------------------------------------------------------------
	// Rebuild edge geometry
	// ------------------------------------------------------------

	private void RebuildGeometry()
	{
		int count =
			vertices.Length;

		minX =
			vertices[0].X;

		maxX =
			vertices[0].X;

		minY =
			vertices[0].Y;

		maxY =
			vertices[0].Y;

		for (
			int i = 1;
			i < count;
			i++)
		{
			Vector2 v =
				vertices[i];

			if (v.X < minX)
				minX = v.X;

			if (v.X > maxX)
				maxX = v.X;

			if (v.Y < minY)
				minY = v.Y;

			if (v.Y > maxY)
				maxY = v.Y;
		}

		for (
			int i = 0;
			i < count;
			i++)
		{
			Vector2 a =
				vertices[i];

			Vector2 b =
				vertices[
					(i + 1) %
					count
				];

			Vector2 edge =
				b - a;

			edges[i] =
				edge;

			float lengthSquared =
				edge.LengthSquared();

			edgeLengthSquared[i] =
				lengthSquared;

			if (
				lengthSquared <=
				Epsilon)
			{
				edgeNormals[i] =
					Vector2.Zero;

				continue;
			}

			Vector2 normal;

			if (counterClockwise)
			{
				normal =
					new Vector2(
						edge.Y,
						-edge.X
					);
			}
			else
			{
				normal =
					new Vector2(
						-edge.Y,
						edge.X
					);
			}

			edgeNormals[i] =
				normal.Normalized();
		}
	}

	// ============================================================
	// AABB access for the solver broad phase
	// ============================================================
	public void GetBounds(
		out float outMinX,
		out float outMaxX,
		out float outMinY,
		out float outMaxY)
	{
		outMinX = minX;
		outMaxX = maxX;
		outMinY = minY;
		outMaxY = maxY;
	}

	// ============================================================
	// Standard collision
	// ============================================================

	public bool ResolveCollision(
		Vector2 position,
		float particleRadius,
		out Vector2 correctedPosition,
		out Vector2 normal)
	{
		correctedPosition =
			position;

		normal =
			Vector2.Zero;

		float collisionRadius =
			particleRadius +
			CollisionMargin;

		// --------------------------------------------------------
		// Broad phase
		// --------------------------------------------------------

		if (
			position.X <
			minX - collisionRadius ||

			position.X >
			maxX + collisionRadius ||

			position.Y <
			minY - collisionRadius ||

			position.Y >
			maxY + collisionRadius)
		{
			return false;
		}

		// --------------------------------------------------------
		// Find closest edge.
		// --------------------------------------------------------

		int vertexCount =
			vertices.Length;

		float closestDistanceSquared =
			float.MaxValue;

		Vector2 closestPoint =
			Vector2.Zero;

		Vector2 closestEdgeNormal =
			Vector2.Zero;

		for (
			int i = 0;
			i < vertexCount;
			i++)
		{
			Vector2 a =
				vertices[i];

			Vector2 edge =
				edges[i];

			float edgeLengthSq =
				edgeLengthSquared[i];

			if (
				edgeLengthSq <=
				Epsilon)
			{
				continue;
			}

			Vector2 toPoint =
				position -
				a;

			float t =
				toPoint.Dot(edge) /
				edgeLengthSq;

			if (t < 0.0f)
				t = 0.0f;
			else if (t > 1.0f)
				t = 1.0f;

			Vector2 point =
				a +
				edge *
				t;

			float dx =
				position.X -
				point.X;

			float dy =
				position.Y -
				point.Y;

			float distanceSquared =
				dx * dx +
				dy * dy;

			if (
				distanceSquared <
				closestDistanceSquared)
			{
				closestDistanceSquared =
					distanceSquared;

				closestPoint =
					point;

				closestEdgeNormal =
					edgeNormals[i];
			}
		}

		if (
			closestDistanceSquared ==
			float.MaxValue)
		{
			return false;
		}

		if (
			closestEdgeNormal.LengthSquared() <=
			Epsilon)
		{
			return false;
		}

		bool inside =
			IsPointInside(
				position
			);

		float collisionRadiusSquared =
			collisionRadius *
			collisionRadius;

		if (
			!inside &&
			closestDistanceSquared >
			collisionRadiusSquared)
		{
			return false;
		}

		normal =
			closestEdgeNormal;

		// --------------------------------------------------------
		// Particle inside polygon.
		// --------------------------------------------------------

		if (inside)
		{
			correctedPosition =
				closestPoint +
				normal *
				collisionRadius;

			return true;
		}

		// --------------------------------------------------------
		// Particle intersecting polygon.
		// --------------------------------------------------------

		float collisionDistance =
			Mathf.Sqrt(
				closestDistanceSquared
			);

		float penetration =
			collisionRadius -
			collisionDistance;

		if (penetration > 0.0f)
		{
			correctedPosition =
				position +
				normal *
				penetration;

			return true;
		}

		return false;
	}

	// ============================================================
	// Swept collision
	//
	// Tests the whole path from startPosition to endPosition.
	//
	// This is specifically important for the small wheel:
	// a particle may be outside the blade at the beginning and
	// outside the blade again at the end, while crossing directly
	// through the blade in between.
	// ============================================================

	public bool ResolveSweptCollision(
		Vector2 startPosition,
		Vector2 endPosition,
		float particleRadius,
		out Vector2 correctedPosition,
		out Vector2 normal,
		out Vector2 contactPosition)
	{
		correctedPosition =
			endPosition;

		normal =
			Vector2.Zero;

		contactPosition =
			endPosition;

		Vector2 movement =
			endPosition -
			startPosition;

		float distanceSquared =
			movement.LengthSquared();

		// --------------------------------------------------------
		// First check the starting position.
		//
		// This handles a particle that was already touching the
		// blade from the previous iteration.
		// --------------------------------------------------------

		if (
			ResolveCollision(
				startPosition,
				particleRadius,
				out Vector2 startCorrected,
				out Vector2 startNormal
			))
		{
			correctedPosition =
				startCorrected;

			normal =
				startNormal;

			contactPosition =
				startPosition;

			return true;
		}

		if (
			distanceSquared <=
			Epsilon)
		{
			if (
				ResolveCollision(
					endPosition,
					particleRadius,
					out correctedPosition,
					out normal
				))
			{
				contactPosition =
					endPosition;

				return true;
			}

			return false;
		}

		float distance =
			Mathf.Sqrt(
				distanceSquared
			);

		int steps =
			(int)MathF.Ceiling(
				distance /
				SweptStep
			);

		if (steps < 1)
			steps = 1;

		if (steps > MaxSweptSteps)
			steps = MaxSweptSteps;

		// --------------------------------------------------------
		// Walk along the particle path.
		// --------------------------------------------------------

		Vector2 previous =
			startPosition;

		for (
			int step = 1;
			step <= steps;
			step++)
		{
			float t =
				(float)step /
				steps;

			Vector2 current =
				startPosition +
				movement *
				t;

			if (
				ResolveCollision(
					current,
					particleRadius,
					out Vector2 currentCorrected,
					out Vector2 currentNormal
				))
			{
				// ------------------------------------------------
				// The first sampled collision is the contact.
				// ------------------------------------------------

				// Refine the collision location between the last
				// non-colliding point and the first colliding point.
				Vector2 low =
					previous;

				Vector2 high =
					current;

				for (
					int refinement = 0;
					refinement < 4;
					refinement++)
				{
					Vector2 middle =
						(low + high) *
						0.5f;

					if (
						ResolveCollision(
							middle,
							particleRadius,
							out _,
							out _
						))
					{
						high =
							middle;
					}
					else
					{
						low =
							middle;
					}
				}

				Vector2 hitPosition =
					high;

				ResolveCollision(
					hitPosition,
					particleRadius,
					out Vector2 hitCorrected,
					out Vector2 hitNormal
				);

				correctedPosition =
					hitCorrected;

				normal =
					hitNormal;

				contactPosition =
					hitPosition;

				return true;
			}

			previous =
				current;
		}

		return false;
	}

	// ------------------------------------------------------------
	// Signed area
	// ------------------------------------------------------------

	private float CalculateSignedArea()
	{
		float area =
			0.0f;

		int count =
			localVertices.Length;

		for (
			int i = 0;
			i < count;
			i++)
		{
			Vector2 a =
				localVertices[i];

			Vector2 b =
				localVertices[
					(i + 1) %
					count
				];

			area +=
				a.X * b.Y -
				b.X * a.Y;
		}

		return area *
			0.5f;
	}

	// ------------------------------------------------------------
	// Point inside convex polygon
	// ------------------------------------------------------------

	private bool IsPointInside(
		Vector2 point)
	{
		bool hasPositive =
			false;

		bool hasNegative =
			false;

		int count =
			vertices.Length;

		for (
			int i = 0;
			i < count;
			i++)
		{
			Vector2 a =
				vertices[i];

			Vector2 edge =
				edges[i];

			Vector2 toPoint =
				point -
				a;

			float cross =
				edge.X *
				toPoint.Y -
				edge.Y *
				toPoint.X;

			if (
				cross >
				Epsilon)
			{
				hasPositive =
					true;
			}
			else if (
				cross <
				-Epsilon)
			{
				hasNegative =
					true;
			}

			if (
				hasPositive &&
				hasNegative)
			{
				return false;
			}
		}

		return true;
	}
}
