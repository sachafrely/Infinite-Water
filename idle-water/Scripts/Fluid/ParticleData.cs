using Godot;

public class ParticleData
{
	public readonly int Count;

	public float[] PosX;
	public float[] PosY;

	public float[] VelX;
	public float[] VelY;

	public float[] PredX;
	public float[] PredY;


	public ParticleData(int count)
	{
		Count = count;

		PosX = new float[count];
		PosY = new float[count];

		VelX = new float[count];
		VelY = new float[count];

		PredX = new float[count];
		PredY = new float[count];
	}


	public Vector2 GetPosition(int i)
	{
		return new Vector2(
			PosX[i],
			PosY[i]
		);
	}


	public void SetPosition(int i, Vector2 pos)
	{
		PosX[i] = pos.X;
		PosY[i] = pos.Y;
	}
}
