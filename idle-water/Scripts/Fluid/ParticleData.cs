
using Godot;

public class ParticleData
{
	public readonly int Capacity;

	private int activeCount;

	public int Count
	{
		get
		{
			return activeCount;
		}
	}

	public float[] PosX;
	public float[] PosY;

	public float[] VelX;
	public float[] VelY;

	public float[] PredX;
	public float[] PredY;

	public ParticleData(int capacity)
	{
		Capacity = capacity;
		activeCount = 0;

		PosX = new float[capacity];
		PosY = new float[capacity];

		VelX = new float[capacity];
		VelY = new float[capacity];

		PredX = new float[capacity];
		PredY = new float[capacity];
	}

	public bool AddParticle(
		float x,
		float y,
		float velocityX = 0.0f,
		float velocityY = 0.0f)
	{
		if (
			activeCount >=
			Capacity)
		{
			return false;
		}

		int index =
			activeCount;

		PosX[index] =
			x;

		PosY[index] =
			y;

		VelX[index] =
			velocityX;

		VelY[index] =
			velocityY;

		PredX[index] =
			x;

		PredY[index] =
			y;

		activeCount++;

		return true;
	}

	// ------------------------------------------------------------
	// Remove particle
	//
	// The last active particle is moved into the removed slot.
	// This keeps removal O(1) and avoids shifting the entire array.
	// ------------------------------------------------------------

	public bool RemoveParticle(int index)
	{
		if (
			index < 0 ||
			index >= activeCount)
		{
			return false;
		}

		int lastIndex =
			activeCount - 1;

		if (
			index !=
			lastIndex)
		{
			PosX[index] =
				PosX[lastIndex];

			PosY[index] =
				PosY[lastIndex];

			VelX[index] =
				VelX[lastIndex];

			VelY[index] =
				VelY[lastIndex];

			PredX[index] =
				PredX[lastIndex];

			PredY[index] =
				PredY[lastIndex];
		}

		activeCount--;

		return true;
	}

	public Vector2 GetPosition(int i)
	{
		return new Vector2(
			PosX[i],
			PosY[i]
		);
	}

	public void SetPosition(
		int i,
		Vector2 pos)
	{
		PosX[i] =
			pos.X;

		PosY[i] =
			pos.Y;
	}
}
