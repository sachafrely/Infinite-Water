using Godot;

public class PbfSolver
{
    private readonly SpatialHash hash;

    public float SmoothingRadius = 12.0f;

    private const float RestDensity = 1.0f;
    private const float LambdaEpsilon = 0.0001f;
    private const float Relaxation = 0.01f;
    private const int Iterations = 4;

    private const float MinX = 20.0f;
    private const float MaxX = 700.0f;
    private const float MinY = 20.0f;
    private const float MaxY = 1260.0f;

    private const float CollisionDamping = 0.9f;

    public PbfSolver(SpatialHash hash)
    {
        this.hash = hash;
    }

    public void Solve(ParticleData particles, float dt)
    {
        // 1. Apply gravity and create predicted positions.
        for (int i = 0; i < particles.Count; i++)
        {
            particles.VelY[i] += 500.0f * dt;

            particles.PredX[i] =
                particles.PosX[i] +
                particles.VelX[i] * dt;

            particles.PredY[i] =
                particles.PosY[i] +
                particles.VelY[i] * dt;
        }

        // 2. Iteratively solve the density constraint.
        for (int iteration = 0; iteration < Iterations; iteration++)
        {
            UpdateHash(particles);

            for (int i = 0; i < particles.Count; i++)
            {
                Vector2 pos = GetPredictedPosition(particles, i);

                float density = CalculateDensity(particles, pos);

                float constraint =
                    density / RestDensity - 1.0f;

                float gradientSum = 0.0f;

                int count = hash.Query(
                    pos,
                    SmoothingRadius
                );

                for (int n = 0; n < count; n++)
                {
                    int neighbor = hash.GetResult(n);

                    if (neighbor == i)
                        continue;

                    Vector2 neighborPos =
                        GetPredictedPosition(
                            particles,
                            neighbor
                        );

                    Vector2 direction =
                        pos - neighborPos;

                    float distance =
                        direction.Length();

                    if (distance <= 0.00001f ||
                        distance >= SmoothingRadius)
                    {
                        continue;
                    }

                    Vector2 gradient =
                        direction.Normalized() *
                        SpikyGradient(distance);

                    gradientSum += gradient.LengthSquared();
                }

                float lambda =
                    -constraint /
                    (gradientSum + LambdaEpsilon);

                lambda *= Relaxation;

                Vector2 correction = Vector2.Zero;

                for (int n = 0; n < count; n++)
                {
                    int neighbor = hash.GetResult(n);

                    if (neighbor == i)
                        continue;

                    Vector2 neighborPos =
                        GetPredictedPosition(
                            particles,
                            neighbor
                        );

                    Vector2 direction =
                        pos - neighborPos;

                    float distance =
                        direction.Length();

                    if (distance <= 0.00001f ||
                        distance >= SmoothingRadius)
                    {
                        continue;
                    }

                    Vector2 gradient =
                        direction.Normalized() *
                        SpikyGradient(distance);

                    correction +=
                        lambda * gradient;
                }

                particles.PredX[i] += correction.X;
                particles.PredY[i] += correction.Y;
            }

            ConstrainToBounds(particles);
        }

        // 3. Convert corrected predicted positions
        //    back into velocity and position.
        for (int i = 0; i < particles.Count; i++)
        {
            float oldX = particles.PosX[i];
            float oldY = particles.PosY[i];

            float newVelocityX =
                (particles.PredX[i] - oldX) / dt;

            float newVelocityY =
                (particles.PredY[i] - oldY) / dt;

            if (particles.PredX[i] <= MinX ||
                particles.PredX[i] >= MaxX)
            {
                newVelocityX *= -CollisionDamping;
            }

            if (particles.PredY[i] <= MinY ||
                particles.PredY[i] >= MaxY)
            {
                newVelocityY *= -CollisionDamping;
            }

            particles.VelX[i] = newVelocityX;
            particles.VelY[i] = newVelocityY;

            particles.PosX[i] =
                particles.PredX[i];

            particles.PosY[i] =
                particles.PredY[i];
        }
    }

    private void UpdateHash(ParticleData particles)
    {
        hash.Clear();

        for (int i = 0; i < particles.Count; i++)
        {
            hash.Insert(
                i,
                GetPredictedPosition(particles, i)
            );
        }
    }

    private Vector2 GetPredictedPosition(
        ParticleData particles,
        int i)
    {
        return new Vector2(
            particles.PredX[i],
            particles.PredY[i]
        );
    }

    private float CalculateDensity(
        ParticleData particles,
        Vector2 position)
    {
        float density = 0.0f;

        int count = hash.Query(
            position,
            SmoothingRadius
        );

        for (int n = 0; n < count; n++)
        {
            int neighbor = hash.GetResult(n);

            Vector2 neighborPos =
                GetPredictedPosition(
                    particles,
                    neighbor
                );

            float distance =
                position.DistanceTo(neighborPos);

            if (distance >= SmoothingRadius)
                continue;

            density += Poly6Kernel(distance);
        }

        return density;
    }

    private float Poly6Kernel(float distance)
    {
        float h = SmoothingRadius;

        if (distance >= h)
            return 0.0f;

        float value =
            h * h -
            distance * distance;

        return value * value * value;
    }

    private float SpikyGradient(float distance)
    {
        float h = SmoothingRadius;

        if (distance <= 0.0f ||
            distance >= h)
        {
            return 0.0f;
        }

        float value = h - distance;

        return value * value;
    }

    private void ConstrainToBounds(ParticleData particles)
    {
        for (int i = 0; i < particles.Count; i++)
        {
            particles.PredX[i] = Mathf.Clamp(
                particles.PredX[i],
                MinX,
                MaxX
            );

            particles.PredY[i] = Mathf.Clamp(
                particles.PredY[i],
                MinY,
                MaxY
            );
        }
    }
}
