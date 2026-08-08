using Godot;

public class PbfSolver
{
    private readonly SpatialHash hash;

    public float SmoothingRadius = 12.0f;
    private float smoothingRadiusSq; // Cache squared radius for distance checks

    private float[] lambdas;
    private float[] deltaX;
    private float[] deltaY;
    private float[] densities; // Cache densities to avoid recalculating

    private const float RestDensity = 1.0f;
    private const float LambdaEpsilon = 0.0001f;
    private const float Relaxation = 0.01f;
    private const int Iterations = 4;

    private const float MinX = 20.0f;
    private const float MaxX = 700.0f;
    private const float MinY = 20.0f;
    private const float MaxY = 1260.0f;

    private const float CollisionDamping = 0.9f;

    // Precomputed kernel constants
    private float poly6Constant;
    private float spikyGradientConstant;

    public PbfSolver(SpatialHash hash)
    {
        this.hash = hash;
        UpdateKernelConstants();
    }

    private void UpdateKernelConstants()
    {
        float h = SmoothingRadius;
        smoothingRadiusSq = h * h;
        
        // Poly6 kernel constant: 315 / (64 * pi * h^9)
        poly6Constant = 315.0f / (64.0f * Mathf.Pi * Mathf.Pow(h, 9));
        
        // Spiky gradient constant: -45 / (pi * h^6)
        spikyGradientConstant = -45.0f / (Mathf.Pi * Mathf.Pow(h, 6));
    }

    public void Solve(ParticleData particles, float dt)
    {
        int particleCount = particles.Count;

        // Ensure working arrays are allocated for this particle count.
        if (lambdas == null || lambdas.Length < particleCount)
        {
            lambdas = new float[particleCount];
            deltaX = new float[particleCount];
            deltaY = new float[particleCount];
            densities = new float[particleCount];
        }

        // 1. Apply gravity and create predicted positions.
        ApplyGravityAndPredictPositions(particles, dt);

        // 2. Iteratively solve the density constraint using a two-phase approach.
        for (int iteration = 0; iteration < Iterations; iteration++)
        {
            // Build spatial hash for current predicted positions.
            UpdateHash(particles);

            // Phase A: compute density and lambdas for every particle (combined into single pass).
            ComputeDensitiesAndLambdas(particles);

            // Phase B: compute position deltas for every particle using lambdas.
            ComputePositionDeltas(particles);

            // Apply all deltas to predicted positions.
            ApplyPositionCorrections(particles);

            // Enforce boundary constraints after corrections.
            ConstrainToBounds(particles);
        }

        // 3. Convert corrected predicted positions back into velocity and position.
        UpdateVelocitiesAndPositions(particles, dt);
    }

    private void ApplyGravityAndPredictPositions(ParticleData particles, float dt)
    {
        int count = particles.Count;
        float gravityDt = 500.0f * dt;
        
        for (int i = 0; i < count; i++)
        {
            particles.VelY[i] += gravityDt;

            particles.PredX[i] = particles.PosX[i] + particles.VelX[i] * dt;
            particles.PredY[i] = particles.PosY[i] + particles.VelY[i] * dt;
        }
    }

    private void UpdateHash(ParticleData particles)
    {
        hash.Clear();
        int count = particles.Count;

        for (int i = 0; i < count; i++)
        {
            hash.Insert(i, particles.PredX[i], particles.PredY[i]);
        }
    }

    private void ComputeDensitiesAndLambdas(ParticleData particles)
    {
        int count = particles.Count;

        for (int i = 0; i < count; i++)
        {
            float posX = particles.PredX[i];
            float posY = particles.PredY[i];

            float density = CalculateDensityOptimized(particles, posX, posY);
            densities[i] = density;

            float constraint = density / RestDensity - 1.0f;
            float gradientSum = 0.0f;

            int neighborCount = hash.Query(posX, posY, SmoothingRadius);

            for (int n = 0; n < neighborCount; n++)
            {
                int neighbor = hash.GetResult(n);

                if (neighbor == i)
                    continue;

                float neighborX = particles.PredX[neighbor];
                float neighborY = particles.PredY[neighbor];

                float dx = posX - neighborX;
                float dy = posY - neighborY;
                float distSq = dx * dx + dy * dy;

                if (distSq <= 0.0001f || distSq >= smoothingRadiusSq)
                    continue;

                float distance = Mathf.Sqrt(distSq);
                float normalizedGradX = dx / distance;
                float normalizedGradY = dy / distance;

                float spikyGrad = SpikyGradientOptimized(distance);

                gradientSum += (normalizedGradX * normalizedGradX + normalizedGradY * normalizedGradY) * spikyGrad * spikyGrad;
            }

            float lambda = -constraint / (gradientSum + LambdaEpsilon);
            lambda *= Relaxation;

            lambdas[i] = lambda;
        }
    }

    private void ComputePositionDeltas(ParticleData particles)
    {
        int count = particles.Count;

        // Clear deltas
        for (int i = 0; i < count; i++)
        {
            deltaX[i] = 0.0f;
            deltaY[i] = 0.0f;
        }

        for (int i = 0; i < count; i++)
        {
            float posX = particles.PredX[i];
            float posY = particles.PredY[i];

            int neighborCount = hash.Query(posX, posY, SmoothingRadius);

            for (int n = 0; n < neighborCount; n++)
            {
                int neighbor = hash.GetResult(n);

                if (neighbor == i)
                    continue;

                float neighborX = particles.PredX[neighbor];
                float neighborY = particles.PredY[neighbor];

                float dx = posX - neighborX;
                float dy = posY - neighborY;
                float distSq = dx * dx + dy * dy;

                if (distSq <= 0.0001f || distSq >= smoothingRadiusSq)
                    continue;

                float distance = Mathf.Sqrt(distSq);
                float normalizedGradX = dx / distance;
                float normalizedGradY = dy / distance;

                float spikyGrad = SpikyGradientOptimized(distance);

                // Position delta contribution from this neighbor.
                float scalar = (lambdas[i] + lambdas[neighbor]) * spikyGrad;

                deltaX[i] += scalar * normalizedGradX;
                deltaY[i] += scalar * normalizedGradY;
            }
        }
    }

    private void ApplyPositionCorrections(ParticleData particles)
    {
        int count = particles.Count;
        float inverseDensity = 1.0f / RestDensity;

        for (int i = 0; i < count; i++)
        {
            particles.PredX[i] += deltaX[i] * inverseDensity;
            particles.PredY[i] += deltaY[i] * inverseDensity;
        }
    }

    private void UpdateVelocitiesAndPositions(ParticleData particles, float dt)
    {
        int count = particles.Count;
        float invDt = 1.0f / dt;

        for (int i = 0; i < count; i++)
        {
            float oldX = particles.PosX[i];
            float oldY = particles.PosY[i];

            float predX = particles.PredX[i];
            float predY = particles.PredY[i];

            float newVelocityX = (predX - oldX) * invDt;
            float newVelocityY = (predY - oldY) * invDt;

            // Collision damping
            if (predX <= MinX || predX >= MaxX)
            {
                newVelocityX *= -CollisionDamping;
            }

            if (predY <= MinY || predY >= MaxY)
            {
                newVelocityY *= -CollisionDamping;
            }

            particles.VelX[i] = newVelocityX;
            particles.VelY[i] = newVelocityY;

            particles.PosX[i] = predX;
            particles.PosY[i] = predY;
        }
    }

    private float CalculateDensityOptimized(ParticleData particles, float posX, float posY)
    {
        float density = 0.0f;

        int count = hash.Query(posX, posY, SmoothingRadius);

        for (int n = 0; n < count; n++)
        {
            int neighbor = hash.GetResult(n);

            float neighborX = particles.PredX[neighbor];
            float neighborY = particles.PredY[neighbor];

            float dx = posX - neighborX;
            float dy = posY - neighborY;
            float distSq = dx * dx + dy * dy;

            if (distSq >= smoothingRadiusSq)
                continue;

            float distance = Mathf.Sqrt(distSq);
            density += Poly6KernelOptimized(distance);
        }

        return density;
    }

    private float Poly6KernelOptimized(float distance)
    {
        float h = SmoothingRadius;

        if (distance >= h)
            return 0.0f;

        float value = h * h - distance * distance;
        return poly6Constant * value * value * value;
    }

    private float SpikyGradientOptimized(float distance)
    {
        float h = SmoothingRadius;

        if (distance <= 0.0f || distance >= h)
            return 0.0f;

        float value = h - distance;
        return spikyGradientConstant * value * value / distance;
    }

    private void ConstrainToBounds(ParticleData particles)
    {
        int count = particles.Count;

        for (int i = 0; i < count; i++)
        {
            if (particles.PredX[i] < MinX)
                particles.PredX[i] = MinX;
            else if (particles.PredX[i] > MaxX)
                particles.PredX[i] = MaxX;

            if (particles.PredY[i] < MinY)
                particles.PredY[i] = MinY;
            else if (particles.PredY[i] > MaxY)
                particles.PredY[i] = MaxY;
        }
    }
}
