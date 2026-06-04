// ============================================================================
// EnvironmentSystem.cs — Environmental effects simulation
// Swinging Paint Bucket Simulation
// ============================================================================
// From the study:
//   Wind: F_wind = kw(v_wind - v_bucket)
//   Humidity: affects paint viscosity μ_eff = μ(1 + kh · humidity)
//   Temperature: affects viscosity μ_eff = μ · e^(Ea / RT)
//   Air density: affects drag forces F_d = -½ ρ_air Cd A |v| v
//   Surface interaction: v_after = -e v_normal + v_tangent (if bucket hits canvas)
// ============================================================================

using UnityEngine;
using SwingingPaintBucket.Core;
using SwingingPaintBucket.Utilities;

namespace SwingingPaintBucket.Physics.Environment
{
    /// <summary>
    /// Manages environmental effects on the simulation including
    /// wind, humidity, temperature, and air density modifications.
    /// </summary>
    public class EnvironmentSystem
    {
        private float _windPhase = 0.0f; // For wind turbulence

        /// <summary>
        /// Updates environment effects for one fixed timestep.
        /// </summary>
        public void UpdatePhysics(float dt, SimulationState state, SimulationConfig config)
        {
            // Update wind if enabled
            if (config.environment.windEnabled)
            {
                UpdateWind(dt, config);
            }

            // Check for bucket-canvas collision
            CheckBucketCanvasCollision(state, config);
        }

        /// <summary>
        /// Updates wind with optional turbulence.
        /// Wind can vary over time for more realistic simulation.
        /// Base: F_wind = kw(v_wind - v_bucket) (applied in BucketSystem/FreeFallSystem)
        /// </summary>
        private void UpdateWind(float dt, SimulationConfig config)
        {
            // Add perlin noise turbulence to wind
            _windPhase += dt * 0.1f;

            // Turbulence adds small variations to base wind velocity
            float turbulenceX = (Mathf.PerlinNoise(_windPhase, 0.0f) - 0.5f) * 0.5f;
            float turbulenceZ = (Mathf.PerlinNoise(0.0f, _windPhase) - 0.5f) * 0.5f;

            // Note: we don't modify config directly (it's the user's settings)
            // Turbulence is applied during force computation in other systems
        }

        /// <summary>
        /// Gets current wind velocity including turbulence.
        /// </summary>
        public Vector3 GetCurrentWindVelocity(SimulationConfig config)
        {
            if (!config.environment.windEnabled)
                return Vector3.zero;

            Vector3 baseWind = config.environment.windVelocity;

            // Add turbulence
            float turbX = (Mathf.PerlinNoise(_windPhase * 2.0f, 0.0f) - 0.5f) * 0.3f;
            float turbZ = (Mathf.PerlinNoise(0.0f, _windPhase * 2.0f) - 0.5f) * 0.3f;

            return baseWind + new Vector3(turbX, 0.0f, turbZ);
        }

        /// <summary>
        /// Returns effective viscosity accounting for humidity and temperature.
        /// From study:
        ///   Humidity: μ_eff = μ(1 + kh · humidity)
        ///   Temperature: μ_eff = μ · e^(Ea / RT)
        /// </summary>
        public static float GetEffectiveViscosity(SimulationConfig config)
        {
            float baseViscosity = config.paint.viscosity;

            // Humidity effect: μ_eff = μ(1 + kh · humidity)
            float humidityEffect = 1.0f + config.environment.humidityViscosityFactor *
                config.environment.humidity;

            // Temperature effect (Arrhenius-like)
            // μ_eff = μ · e^(Ea / RT)
            // Simplified: higher temp → lower viscosity
            float refTemp = 293.15f; // 20°C reference
            float tempRatio = refTemp / Mathf.Max(config.environment.temperature, 200.0f);
            float tempEffect = Mathf.Exp((tempRatio - 1.0f) * 2.0f); // Approximation

            return baseViscosity * humidityEffect * tempEffect;
        }

        /// <summary>
        /// Checks if the bucket has collided with the canvas surface.
        /// From study:
        ///   v_after = -e v_normal + v_tangent
        /// </summary>
        private void CheckBucketCanvasCollision(SimulationState state, SimulationConfig config)
        {
            float bucketBottom = state.bucketPosition.y - config.bucket.height * 0.5f;
            float canvasTop = config.canvas.yPosition;

            if (bucketBottom <= canvasTop)
            {
                // Bucket has hit the canvas!
                Vector3 canvasNormal = config.canvas.GetNormal();

                // Apply collision response from study:
                // v_after = -e v_normal + v_tangent
                float e = 0.3f; // Moderate restitution for bucket-canvas collision
                state.bucketVelocity = state.bucketVelocity.ReflectWithRestitution(canvasNormal, e);

                // Push bucket above canvas
                state.bucketPosition.y = canvasTop + config.bucket.height * 0.5f + 0.01f;
            }
        }

        /// <summary>
        /// Returns air density adjusted for temperature and humidity.
        /// </summary>
        public static float GetEffectiveAirDensity(SimulationConfig config)
        {
            float baseDensity = config.environment.airDensity;

            // Temperature correction (ideal gas law approximation)
            float refTemp = 293.15f;
            float tempCorrection = refTemp / Mathf.Max(config.environment.temperature, 200.0f);

            // Humidity slightly decreases air density (water vapor is lighter than N2)
            float humidityCorrection = 1.0f - 0.025f * config.environment.humidity;

            return baseDensity * tempCorrection * humidityCorrection;
        }
    }
}
