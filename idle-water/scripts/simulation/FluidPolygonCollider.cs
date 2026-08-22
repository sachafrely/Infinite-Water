using System;
using Godot;

public class FluidWheelState
{
	private readonly Vector2 center;
	private float angle;
	private float angularVelocity;

	private const float TorqueScale = 0.003f;
	private const float BaseAngularDamping = 0.40f;
	private const float BaseMaxAngularVelocity = 20.0f;

	private float accumulatedTorque;
	private int biggerPaddlesLevel;
	private int lessFrictionLevel;
	private int moreEfficientLevel;

	public Vector2 Center => center;
	public float Angle => angle;
	public float AngularVelocity => angularVelocity;
	public int BiggerPaddlesLevel => biggerPaddlesLevel;
	public int LessFrictionLevel => lessFrictionLevel;
	public int MoreEfficientLevel => moreEfficientLevel;
	public float PaddleSizeMultiplier => 1.0f + 0.05f * biggerPaddlesLevel;
	public float WheelRadiusMultiplier => 1.0f + 0.08f * biggerPaddlesLevel;
	public float EnergyGenerationMultiplier => 1.0f + 0.20f * moreEfficientLevel;
	public float EffectiveAngularDamping => BaseAngularDamping * (1.0f - 0.20f * lessFrictionLevel);
	public float EffectiveMaxAngularVelocity => BaseMaxAngularVelocity * (1.0f + 0.20f * lessFrictionLevel);

	public FluidWheelState(Vector2 wheelCenter)
	{
		center = wheelCenter;
		angle = 0.0f;
		angularVelocity = 0.0f;
		accumulatedTorque = 0.0f;
	}

	public void SetUpgradeLevels(int biggerPaddles, int lessFriction, int moreEfficient)
	{
		biggerPaddlesLevel = Mathf.Clamp(biggerPaddles, 0, 3);
		lessFrictionLevel = Mathf.Clamp(lessFriction, 0, 3);
		moreEfficientLevel = Mathf.Clamp(moreEfficient, 0, 3);
		angularVelocity = Mathf.Clamp(angularVelocity, -EffectiveMaxAngularVelocity, EffectiveMaxAngularVelocity);
	}

	public void AddTorque(float torque) { accumulatedTorque += torque; }

	public void Step(float dt)
	{
		if (dt <= 0.0f) return;
		angularVelocity += accumulatedTorque * TorqueScale * dt;
		accumulatedTorque = 0.0f;
		float damping = Mathf.Exp(-EffectiveAngularDamping * dt);
		angularVelocity *= damping;
		angularVelocity = Mathf.Clamp(angularVelocity, -EffectiveMaxAngularVelocity, EffectiveMaxAngularVelocity);
		angle += angularVelocity * dt;
		if (angle > Mathf.Tau) angle -= Mathf.Tau;
		if (angle < -Mathf.Tau) angle += Mathf.Tau;
	}

	public Vector2 GetSurfaceVelocity(Vector2 worldPosition)
	{
		Vector2 radius = worldPosition - center;
		return new Vector2(-angularVelocity * radius.Y, angularVelocity * radius.X);
	}
}

public class FluidPolygonCollider
{
	private readonly Vector2[] localVertices;
	private readonly Vector2[] vertices;
	private readonly Vector2[] edges;
	private readonly Vector2[] edgeNormals;
	private readonly float[] edgeLengthSquared;
	private readonly bool counterClockwise;
	private const float CollisionMargin = 1.0f;
	private const float Epsilon = 0.000001f;
	private const float SweptStep = 1.5f;
	private const int MaxSweptSteps = 64;
	private float minX, maxX, minY, maxY;
	private FluidWheelState wheel;
	private bool isWheelPaddle;
	private float paddleInnerRadius;
	private float paddleOuterRadius;
	private Vector2 paddleDirection;
	private Vector2 paddleTangent;

	public bool IsWheel => wheel != null;
	public FluidWheelState Wheel => wheel;

	public FluidPolygonCollider(Vector2[] polygon)
	{
		if (polygon == null || polygon.Length < 3) throw new ArgumentException("Polygon collider requires at least 3 vertices.");
		int count = polygon.Length;
		localVertices = new Vector2[count]; vertices = new Vector2[count]; edges = new Vector2[count]; edgeNormals = new Vector2[count]; edgeLengthSquared = new float[count];
		Array.Copy(polygon, localVertices, count); Array.Copy(polygon, vertices, count);
		counterClockwise = CalculateSignedArea() > 0.0f;
		RebuildGeometry();
	}

	public void ConfigureAsWheel(FluidWheelState wheelState) => ConfigureAsWheel(wheelState, false, 0.0f, 0.0f);

	public void ConfigureAsWheel(FluidWheelState wheelState, bool paddle, float innerRadius, float outerRadius)
	{
		wheel = wheelState; isWheelPaddle = paddle; paddleInnerRadius = innerRadius; paddleOuterRadius = outerRadius;
		if (isWheelPaddle)
		{
			Vector2 average = Vector2.Zero;
			for (int i = 0; i < localVertices.Length; i++) average += localVertices[i];
			paddleDirection = average.LengthSquared() > Epsilon ? average.Normalized() : Vector2.Right;
			paddleTangent = new Vector2(-paddleDirection.Y, paddleDirection.X);
		}
		UpdateWheelGeometry();
	}

	public void UpdateWheelGeometry()
	{
		if (wheel == null) return;
		Vector2 center = wheel.Center;
		float angle = wheel.Angle;
		float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
		float effectiveOuterRadius = isWheelPaddle ? paddleOuterRadius * wheel.WheelRadiusMultiplier : 0.0f;
		float effectiveInnerRadius = paddleInnerRadius;
		if (isWheelPaddle)
		{
			float baseLength = paddleOuterRadius - paddleInnerRadius;
			float effectiveLength = baseLength * wheel.PaddleSizeMultiplier;
			effectiveInnerRadius = effectiveOuterRadius - effectiveLength;
		}

		for (int i = 0; i < localVertices.Length; i++)
		{
			Vector2 local = localVertices[i];
			if (isWheelPaddle)
			{
				float radial = local.Dot(paddleDirection);
				float tangent = local.Dot(paddleTangent);
				float baseLength = paddleOuterRadius - paddleInnerRadius;
				if (baseLength > Epsilon)
				{
					float normalizedLength = (radial - paddleInnerRadius) / baseLength;
					radial = effectiveInnerRadius + normalizedLength * (effectiveOuterRadius - effectiveInnerRadius);
				}
				local = paddleDirection * radial + paddleTangent * tangent;
			}
			vertices[i] = center + new Vector2(local.X * cos - local.Y * sin, local.X * sin + local.Y * cos);
		}
		RebuildGeometry();
	}

	private void RebuildGeometry()
	{
		int count = vertices.Length; minX = vertices[0].X; maxX = vertices[0].X; minY = vertices[0].Y; maxY = vertices[0].Y;
		for (int i = 1; i < count; i++) { Vector2 v = vertices[i]; if (v.X < minX) minX = v.X; if (v.X > maxX) maxX = v.X; if (v.Y < minY) minY = v.Y; if (v.Y > maxY) maxY = v.Y; }
		for (int i = 0; i < count; i++)
		{
			Vector2 a = vertices[i], b = vertices[(i + 1) % count], edge = b - a; edges[i] = edge; float lengthSquared = edge.LengthSquared(); edgeLengthSquared[i] = lengthSquared;
			if (lengthSquared <= Epsilon) { edgeNormals[i] = Vector2.Zero; continue; }
			Vector2 normal = counterClockwise ? new Vector2(edge.Y, -edge.X) : new Vector2(-edge.Y, edge.X); edgeNormals[i] = normal.Normalized();
		}
	}

	public void GetBounds(out float outMinX, out float outMaxX, out float outMinY, out float outMaxY) { outMinX = minX; outMaxX = maxX; outMinY = minY; outMaxY = maxY; }

	public bool ResolveCollision(Vector2 position, float particleRadius, out Vector2 correctedPosition, out Vector2 normal)
	{
		correctedPosition = position; normal = Vector2.Zero; float collisionRadius = particleRadius + CollisionMargin;
		if (position.X < minX - collisionRadius || position.X > maxX + collisionRadius || position.Y < minY - collisionRadius || position.Y > maxY + collisionRadius) return false;
		int vertexCount = vertices.Length; float closestDistanceSquared = float.MaxValue; Vector2 closestPoint = Vector2.Zero, closestEdgeNormal = Vector2.Zero;
		for (int i = 0; i < vertexCount; i++)
		{
			Vector2 a = vertices[i], edge = edges[i]; float edgeLengthSq = edgeLengthSquared[i]; if (edgeLengthSq <= Epsilon) continue;
			Vector2 toPoint = position - a; float t = toPoint.Dot(edge) / edgeLengthSq; if (t < 0.0f) t = 0.0f; else if (t > 1.0f) t = 1.0f;
			Vector2 point = a + edge * t; float dx = position.X - point.X, dy = position.Y - point.Y; float distanceSquared = dx * dx + dy * dy;
			if (distanceSquared < closestDistanceSquared) { closestDistanceSquared = distanceSquared; closestPoint = point; closestEdgeNormal = edgeNormals[i]; }
		}
		if (closestDistanceSquared == float.MaxValue || closestEdgeNormal.LengthSquared() <= Epsilon) return false;
		normal = closestEdgeNormal; bool inside = IsPointInside(position); float collisionRadiusSquared = collisionRadius * collisionRadius;
		if (!inside && closestDistanceSquared > collisionRadiusSquared) return false;
		if (inside) { correctedPosition = closestPoint + normal * collisionRadius; return true; }
		float collisionDistance = Mathf.Sqrt(closestDistanceSquared), penetration = collisionRadius - collisionDistance;
		if (penetration > 0.0f) { correctedPosition = position + normal * penetration; return true; }
		return false;
	}

	public bool ResolveSweptCollision(Vector2 startPosition, Vector2 endPosition, float particleRadius, out Vector2 correctedPosition, out Vector2 normal, out Vector2 contactPosition)
	{
		correctedPosition = endPosition; normal = Vector2.Zero; contactPosition = endPosition; Vector2 movement = endPosition - startPosition; float distanceSquared = movement.LengthSquared();
		if (ResolveCollision(startPosition, particleRadius, out Vector2 startCorrected, out Vector2 startNormal)) { correctedPosition = startCorrected; normal = startNormal; contactPosition = startPosition; return true; }
		if (distanceSquared <= Epsilon) { if (ResolveCollision(endPosition, particleRadius, out correctedPosition, out normal)) { contactPosition = endPosition; return true; } return false; }
		float distance = Mathf.Sqrt(distanceSquared); int steps = (int)MathF.Ceiling(distance / SweptStep); if (steps < 1) steps = 1; if (steps > MaxSweptSteps) steps = MaxSweptSteps;
		Vector2 previous = startPosition;
		for (int step = 1; step <= steps; step++)
		{
			float t = (float)step / steps; Vector2 current = startPosition + movement * t;
			if (ResolveCollision(current, particleRadius, out _, out _))
			{
				Vector2 low = previous, high = current;
				for (int refinement = 0; refinement < 4; refinement++) { Vector2 middle = (low + high) * 0.5f; if (ResolveCollision(middle, particleRadius, out _, out _)) high = middle; else low = middle; }
				Vector2 hitPosition = high; ResolveCollision(hitPosition, particleRadius, out Vector2 hitCorrected, out Vector2 hitNormal); correctedPosition = hitCorrected; normal = hitNormal; contactPosition = hitPosition; return true;
			}
			previous = current;
		}
		return false;
	}

	private float CalculateSignedArea()
	{
		float area = 0.0f; int count = localVertices.Length; for (int i = 0; i < count; i++) { Vector2 a = localVertices[i], b = localVertices[(i + 1) % count]; area += a.X * b.Y - b.X * a.Y; } return area * 0.5f;
	}

	private bool IsPointInside(Vector2 point)
	{
		bool hasPositive = false, hasNegative = false; int count = vertices.Length;
		for (int i = 0; i < count; i++) { Vector2 a = vertices[i], edge = edges[i], toPoint = point - a; float cross = edge.X * toPoint.Y - edge.Y * toPoint.X; if (cross > Epsilon) hasPositive = true; else if (cross < -Epsilon) hasNegative = true; if (hasPositive && hasNegative) return false; }
		return true;
	}
}
