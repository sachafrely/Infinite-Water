internal static class PbfDensityConstraints
{
	public static float CalculateLambdas(
		int count,
		int stride,
		int[] neighborCounts,
		float[] neighborQ,
		float[] neighborGradientScale,
		float[] neighborDx,
		float[] neighborDy,
		float[] particleDensity,
		float[] lambdas,
		float inverseRestDensity,
		float lambdaEpsilon)
	{
		float maximumDensityError =
			0.0f;

		for (
			int i = 0;
			i < count;
			i++)
		{
			int start =
				i * stride;

			int end =
				start +
				neighborCounts[i];

			float density = 0.0f;
			float gradSumX = 0.0f;
			float gradSumY = 0.0f;
			float neighborGradientSquared = 0.0f;

			for (
				int index = start;
				index < end;
				index++)
			{
				float q =
					neighborQ[index];

				float q2 =
					q * q;

				density +=
					q2 * q;

				float scale =
					neighborGradientScale[index];

				float gx =
					neighborDx[index] *
					scale;

				float gy =
					neighborDy[index] *
					scale;

				gradSumX += gx;
				gradSumY += gy;

				neighborGradientSquared +=
					gx * gx +
					gy * gy;
			}

			particleDensity[i] =
				density;

			float constraint =
				density *
				inverseRestDensity -
				1.0f;

			float absoluteConstraint =
				constraint < 0.0f
					? -constraint
					: constraint;

			if (
				absoluteConstraint >
				maximumDensityError)
			{
				maximumDensityError =
					absoluteConstraint;
			}

			float denominator =
				gradSumX * gradSumX +
				gradSumY * gradSumY +
				neighborGradientSquared;

			lambdas[i] =
				-constraint /
				(
					denominator +
					lambdaEpsilon
				);
		}

		return maximumDensityError;
	}
}
