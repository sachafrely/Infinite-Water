/// <summary>
/// PbfBoundaryConstraints — enforces the world-AABB boundary constraint
/// for all particles after each PBF iteration.
///
/// Clamps predicted positions to the world boundary (with a small skin) and
/// records an impact normal so the integration step can apply restitution.
/// Polygon-collider boundary enforcement remains in
/// <c>PbfSolver.ApplyPolygonCollision</c> for now.
/// </summary>
internal static class PbfBoundaryConstraints
{
	/// <summary>
	/// Clamps each particle to the world bounds and records its contact
	/// normal in <see cref="PbfState.Impacted"/> /
	/// <see cref="PbfState.ImpactNormalX"/> /
	/// <see cref="PbfState.ImpactNormalY"/>.
	/// </summary>
	public static void ConstrainToBounds(
		float[] predX,
		float[] predY,
		int count,
		PbfState state)
	{
		float left =
			PbfSolver.MinX +
			PbfSolver.BoundarySkin;

		float right =
			PbfSolver.MaxX -
			PbfSolver.BoundarySkin;

		float top =
			PbfSolver.MinY +
			PbfSolver.BoundarySkin;

		float bottom =
			PbfSolver.MaxY -
			PbfSolver.BoundarySkin;

		bool[] impacted =
			state.Impacted;

		float[] normalX =
			state.ImpactNormalX;

		float[] normalY =
			state.ImpactNormalY;

		for (
			int i = 0;
			i < count;
			i++)
		{
			float x = predX[i];
			float y = predY[i];

			if (x < left)
			{
				x = left;
				impacted[i] = true;
				normalX[i] = 1.0f;
				normalY[i] = 0.0f;
			}
			else if (x > right)
			{
				x = right;
				impacted[i] = true;
				normalX[i] = -1.0f;
				normalY[i] = 0.0f;
			}

			if (y < top)
			{
				y = top;
				impacted[i] = true;
				normalX[i] = 0.0f;
				normalY[i] = 1.0f;
			}
			else if (y > bottom)
			{
				y = bottom;
				impacted[i] = true;
				normalX[i] = 0.0f;
				normalY[i] = -1.0f;
			}

			predX[i] = x;
			predY[i] = y;
		}
	}
}
