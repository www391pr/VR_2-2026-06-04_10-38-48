// ============================================================================
// ColorMixingSystem.cs — Paint color mixing on canvas
// Swinging Paint Bucket Simulation
// ============================================================================
// From the study:
//   Basic mix: C_new = (mi Ci + ms Cs) / (mi + ms)
//   Velocity-dependent: β = 1 - e^(-k|v|)
//     C_new = (1 - β) Cs + β · (mi Ci + ms Cs)/(mi + ms)
//   Diffusion: dCi/dt = Dc Σj (Cj - Ci) Wij
//     Dc = μ / K
//   Momentum on existing paint:
//     v_final = (m1 v1 + m2 v2) / (m1 + m2)
//     F_mix = μ(v_incoming - v_surface)
// ============================================================================

using UnityEngine;
using SwingingPaintBucket.Core;
using SwingingPaintBucket.Utilities;

namespace SwingingPaintBucket.Physics.Canvas
{
    /// <summary>
    /// Handles color mixing between paint particles on the canvas.
    /// Implements mass-weighted blending with velocity-dependent mixing factor
    /// and color diffusion between neighboring particles.
    /// </summary>
    public class ColorMixingSystem
    {
        // Spatial grid for neighbor search on canvas (2D)
        private const float NEIGHBOR_RADIUS = 0.02f; // meters
        private const int GRID_SIZE = 256;

        // Grid cells store indices of particles in each cell
        private int[,][] _grid;
        private int[,] _gridCounts;
        private bool _gridInitialized = false;

        /// <summary>
        /// Updates color mixing for all canvas particles.
        /// </summary>
        public void UpdatePhysics(float dt, SimulationState state, SimulationConfig config,
            ParticleManager particleManager)
        {
            var particles = particleManager.Particles;
            int[] canvasIndices = particleManager.GetCanvasIndices();
            int canvasCount = particleManager.CanvasCount;

            if (canvasCount == 0) return;

            // Color diffusion rate from study: Dc = μ / K
            float viscosity = config.GetEffectiveViscosity();
            float K = 100.0f; // Experimental constant
            float Dc = viscosity / K;
            Dc = Mathf.Clamp(Dc, 0.0f, config.paint.colorDiffusionRate);

            // ══════════════════════════════════════════════════════════════
            // COLOR DIFFUSION between neighboring particles
            // From study: dCi/dt = Dc Σj (Cj - Ci) Wij
            // ══════════════════════════════════════════════════════════════

            // Build spatial grid for neighbor search (2D canvas plane)
            BuildSpatialGrid(particles, canvasIndices, canvasCount, config);

            // Process diffusion
            for (int i = 0; i < canvasCount; i++)
            {
                int idx = canvasIndices[i];
                if (particles.states[idx] != ParticleState.OnCanvas) continue;
                if (particles.masses[idx] < PhysicsConstants.EPSILON * 0.01f) continue;

                ProcessColorDiffusion(idx, dt, Dc, particles, config);
            }

            // Update canvas coverage statistics
            UpdateCoverageStatistics(state, config, particles, canvasIndices, canvasCount);
        }

        /// <summary>
        /// Processes color diffusion for a single particle.
        /// From study: dCi/dt = Dc Σj (Cj - Ci) Wij
        /// Wij is the neighbor weight based on distance.
        /// </summary>
        private void ProcessColorDiffusion(int idx, float dt, float Dc,
            ParticleSOA particles, SimulationConfig config)
        {
            Vector3 pos = particles.positions[idx];
            Color currentColor = particles.colors[idx];

            // Find neighbors using spatial grid
            float canvasWidth = config.canvas.width;
            float canvasHeight = config.canvas.height;

            // Grid cell of this particle
            int cellX = Mathf.Clamp(
                Mathf.FloorToInt((pos.x + canvasWidth * 0.5f) / canvasWidth * GRID_SIZE),
                0, GRID_SIZE - 1);
            int cellZ = Mathf.Clamp(
                Mathf.FloorToInt((pos.z + canvasHeight * 0.5f) / canvasHeight * GRID_SIZE),
                0, GRID_SIZE - 1);

            Color colorDelta = Color.clear;
            float totalWeight = 0.0f;

            // Search neighboring cells
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    int nx = cellX + dx;
                    int nz = cellZ + dz;

                    if (nx < 0 || nx >= GRID_SIZE || nz < 0 || nz >= GRID_SIZE)
                        continue;

                    if (_gridCounts[nx, nz] == 0) continue;

                    int[] cellParticles = _grid[nx, nz];
                    int count = _gridCounts[nx, nz];

                    for (int j = 0; j < count; j++)
                    {
                        int neighborIdx = cellParticles[j];
                        if (neighborIdx == idx) continue;
                        if (particles.states[neighborIdx] != ParticleState.OnCanvas) continue;

                        Vector3 neighborPos = particles.positions[neighborIdx];
                        float distSqr = (pos - neighborPos).sqrMagnitude;
                        float radiusSqr = NEIGHBOR_RADIUS * NEIGHBOR_RADIUS;

                        if (distSqr >= radiusSqr) continue;

                        // Weight function Wij (simplified poly6 in 2D)
                        float diff = radiusSqr - distSqr;
                        float weight = diff * diff; // Simplified kernel

                        Color neighborColor = particles.colors[neighborIdx];
                        Color colorDiff = neighborColor - currentColor;

                        colorDelta += weight * colorDiff;
                        totalWeight += weight;
                    }
                }
            }

            // Apply diffusion: dCi/dt = Dc Σj (Cj - Ci) Wij
            if (totalWeight > PhysicsConstants.EPSILON)
            {
                Color diffusion = Dc * dt * colorDelta;
                Color newColor = currentColor + diffusion;

                // Clamp to valid color range
                newColor.r = Mathf.Clamp01(newColor.r);
                newColor.g = Mathf.Clamp01(newColor.g);
                newColor.b = Mathf.Clamp01(newColor.b);
                newColor.a = Mathf.Clamp01(newColor.a);

                particles.colors[idx] = newColor;
            }
        }

        /// <summary>
        /// Mixes colors on impact using the velocity-dependent formula.
        /// From study:
        ///   C_new = (1 - β) Cs + β · (mi Ci + ms Cs)/(mi + ms)
        ///   β = 1 - e^(-k|v|)
        /// </summary>
        public static Color MixColorsOnImpact(
            Color surfaceColor, float surfaceMass,
            Color incomingColor, float incomingMass,
            float impactSpeed, float mixingK)
        {
            float beta = MathUtils.MixingFactor(impactSpeed, mixingK);
            return surfaceColor.MixWithBeta(incomingColor, surfaceMass, incomingMass, beta);
        }

        /// <summary>
        /// Applies momentum conservation when particle lands on existing paint.
        /// From study:
        ///   v_final = (m1 v1 + m2 v2) / (m1 + m2)
        /// </summary>
        public static Vector3 ConserveMomentum(
            Vector3 v1, float m1, Vector3 v2, float m2)
        {
            float totalMass = m1 + m2;
            if (totalMass < PhysicsConstants.EPSILON) return Vector3.zero;
            return (m1 * v1 + m2 * v2) / totalMass;
        }

        /// <summary>
        /// Builds a 2D spatial grid for fast neighbor queries on the canvas.
        /// </summary>
        private void BuildSpatialGrid(ParticleSOA particles, int[] indices, int count,
            SimulationConfig config)
        {
            if (!_gridInitialized)
            {
                _grid = new int[GRID_SIZE, GRID_SIZE][];
                _gridCounts = new int[GRID_SIZE, GRID_SIZE];

                for (int x = 0; x < GRID_SIZE; x++)
                    for (int z = 0; z < GRID_SIZE; z++)
                        _grid[x, z] = new int[32]; // max 32 particles per cell

                _gridInitialized = true;
            }

            // Clear grid counts
            for (int x = 0; x < GRID_SIZE; x++)
                for (int z = 0; z < GRID_SIZE; z++)
                    _gridCounts[x, z] = 0;

            float canvasWidth = config.canvas.width;
            float canvasHeight = config.canvas.height;

            // Insert particles into grid
            for (int i = 0; i < count; i++)
            {
                int idx = indices[i];
                if (particles.states[idx] != ParticleState.OnCanvas) continue;

                Vector3 pos = particles.positions[idx];

                int cellX = Mathf.Clamp(
                    Mathf.FloorToInt((pos.x + canvasWidth * 0.5f) / canvasWidth * GRID_SIZE),
                    0, GRID_SIZE - 1);
                int cellZ = Mathf.Clamp(
                    Mathf.FloorToInt((pos.z + canvasHeight * 0.5f) / canvasHeight * GRID_SIZE),
                    0, GRID_SIZE - 1);

                int c = _gridCounts[cellX, cellZ];
                if (c < 32) // Max particles per cell
                {
                    _grid[cellX, cellZ][c] = idx;
                    _gridCounts[cellX, cellZ] = c + 1;
                }
            }
        }

        /// <summary>
        /// Updates canvas coverage statistics.
        /// </summary>
        private void UpdateCoverageStatistics(SimulationState state, SimulationConfig config,
            ParticleSOA particles, int[] indices, int count)
        {
            if (count == 0)
            {
                state.canvasCoveragePercent = 0.0f;
                state.totalPaintArea = 0.0f;
                return;
            }

            // Count unique grid cells with paint
            int coveredCells = 0;
            for (int x = 0; x < GRID_SIZE; x++)
                for (int z = 0; z < GRID_SIZE; z++)
                    if (_gridCounts[x, z] > 0) coveredCells++;

            int totalCells = GRID_SIZE * GRID_SIZE;
            state.canvasCoveragePercent = (float)coveredCells / totalCells * 100.0f;

            float canvasArea = config.canvas.width * config.canvas.height;
            state.totalPaintArea = state.canvasCoveragePercent * canvasArea / 100.0f;
        }
    }
}
