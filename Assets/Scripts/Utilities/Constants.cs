// ============================================================================
// Constants.cs — Global physics constants and simulation limits
// Swinging Paint Bucket Simulation
// ============================================================================
// All constants are derived from the physics study reference.
// No value may be changed without cross-referencing the study.
// ============================================================================

namespace SwingingPaintBucket.Utilities
{
    /// <summary>
    /// Global physics constants used across all simulation subsystems.
    /// Values are in SI units unless otherwise noted.
    /// </summary>
    public static class PhysicsConstants
    {
        // ── Gravity ──────────────────────────────────────────────────────
        public const float DEFAULT_GRAVITY = 9.81f; // m/s²

        // ── Air ──────────────────────────────────────────────────────────
        public const float DEFAULT_AIR_DENSITY = 1.225f;      // kg/m³ at sea level, 15°C
        public const float DEFAULT_AIR_DRAG_COEFFICIENT = 0.47f; // sphere Cd

        // ── SPH ──────────────────────────────────────────────────────────
        /// <summary>Poly6 kernel normalization constant for 3D: 315 / (64πh⁹)</summary>
        public const float POLY6_CONSTANT_3D = 315.0f / (64.0f * PI);
        /// <summary>Spiky kernel gradient normalization for 3D: -45 / (πh⁶)</summary>
        public const float SPIKY_GRAD_CONSTANT_3D = -45.0f / PI;
        /// <summary>Viscosity kernel Laplacian normalization for 3D: 45 / (πh⁶)</summary>
        public const float VISCOSITY_LAP_CONSTANT_3D = 45.0f / PI;

        // ── Discharge Coefficients (from study: shape-dependent) ─────────
        public const float CD_CIRCULAR = 0.6f;
        public const float CD_SHARP_EDGED = 0.62f;
        public const float CD_TUBULAR = 0.8f;

        // ── Weber Number Thresholds (from study) ─────────────────────────
        public const float WEBER_NO_SPLASH = 10.0f;
        public const float WEBER_SPLASH_BEGIN = 50.0f;
        public const float WEBER_HEAVY_SPLASH = 100.0f;

        // ── Math ─────────────────────────────────────────────────────────
        public const float PI = 3.14159265358979f;
        public const float TWO_PI = 6.28318530717959f;
        public const float HALF_PI = 1.57079632679490f;
        public const float DEG_TO_RAD = PI / 180.0f;
        public const float RAD_TO_DEG = 180.0f / PI;
        public const float EPSILON = 1e-6f;
    }

    /// <summary>
    /// Simulation limits to prevent instability and memory overflow.
    /// </summary>
    public static class SimulationLimits
    {
        // ── Particle Counts ──────────────────────────────────────────────
        public const int MAX_PARTICLES = 1_100_000;
        public const int DEFAULT_MAX_PARTICLES = 1_000_000;
        public const int PARTICLE_BATCH_SIZE = 256; // compute shader thread group size

        // ── Time ─────────────────────────────────────────────────────────
        public const float FIXED_TIMESTEP = 0.005f;        // 200 Hz physics
        public const float MAX_DELTA_TIME = 0.02f;          // cap at 50ms
        public const float MIN_DELTA_TIME = 0.0001f;
        public const int MAX_PHYSICS_STEPS_PER_FRAME = 4;   // prevent spiral of death

        // ── Spatial Hash ─────────────────────────────────────────────────
        public const int SPATIAL_HASH_TABLE_SIZE = 262144;  // 2^18, power of 2 for fast modulo
        public const float DEFAULT_CELL_SIZE = 0.05f;       // meters

        // ── Canvas ───────────────────────────────────────────────────────
        public const int MAX_CANVAS_RESOLUTION = 4096;
        public const int DEFAULT_CANVAS_RESOLUTION = 2048;

        // ── Rope ─────────────────────────────────────────────────────────
        public const float MIN_ROPE_LENGTH = 0.1f;          // meters
        public const float MAX_ROPE_LENGTH = 10.0f;         // meters

        // ── Recording ────────────────────────────────────────────────────
        public const int MAX_TRAJECTORY_SAMPLES = 10000;
        public const float TRAJECTORY_SAMPLE_INTERVAL = 0.05f; // seconds
    }

    /// <summary>
    /// Default parameter values for first-run experience.
    /// </summary>
    public static class DefaultValues
    {
        // ── Bucket ───────────────────────────────────────────────────────
        public const float BUCKET_MASS = 0.5f;         // kg (empty)
        public const float BUCKET_RADIUS = 0.1f;       // m
        public const float BUCKET_HEIGHT = 0.15f;      // m
        public const float PAINT_VOLUME = 0.002f;      // m³ (~2 liters)
        public const float ORIFICE_RADIUS = 0.005f;    // m (5mm)

        // ── Rope ─────────────────────────────────────────────────────────
        public const float ROPE_LENGTH = 1.5f;         // m
        public const float ROPE_STIFFNESS = 5000.0f;   // N/m (if elastic)
        public const float ROPE_DAMPING = 10.0f;       // Ns/m
        public const float ROPE_TORSION_STIFFNESS = 0.5f; // Nm/rad

        // ── Motion ───────────────────────────────────────────────────────
        public const float INITIAL_THETA = 30.0f;      // degrees
        public const float INITIAL_PHI = 0.0f;         // degrees
        public const float INITIAL_VELOCITY = 0.0f;    // m/s

        // ── Damping ──────────────────────────────────────────────────────
        public const float AIR_DAMPING = 0.02f;        // 1/s
        public const float ROPE_FRICTION_DAMPING = 0.01f; // 1/s

        // ── Paint ────────────────────────────────────────────────────────
        public const float PAINT_DENSITY = 1300.0f;    // kg/m³ (typical acrylic)
        public const float PAINT_VISCOSITY = 0.5f;     // Pa·s
        public const float SURFACE_TENSION = 0.035f;   // N/m
        public const float PAINT_REST_DENSITY = 1300.0f;
        public const float SPH_STIFFNESS = 50.0f;      // k in P = k(ρ - ρ₀)
        public const float SPH_SMOOTHING_RADIUS = 0.04f; // h in SPH kernels

        // ── Canvas ───────────────────────────────────────────────────────
        public const float CANVAS_WIDTH = 2.0f;        // m
        public const float CANVAS_HEIGHT = 2.0f;       // m
        public const float CANVAS_ABSORPTION_RATE = 0.3f;
        public const float CANVAS_FRICTION = 0.8f;
        public const float RESTITUTION_COEFFICIENT = 0.05f; // nearly stick

        // ── Environment ──────────────────────────────────────────────────
        public const float HUMIDITY = 0.5f;             // 0-1
        public const float TEMPERATURE = 293.15f;       // K (~20°C)
        public const float WIND_SPEED = 0.0f;           // m/s (calm)

        // ── Splash ───────────────────────────────────────────────────────
        public const float SPLASH_KS = 5.0f;            // splash count coefficient
        public const float SPLASH_ETA = 0.1f;            // mass fraction for secondary droplets
        public const float SPLASH_KV = 0.3f;             // velocity fraction for secondary droplets
        public const float WEBER_CRITICAL = 50.0f;

        // ── Color ────────────────────────────────────────────────────────
        public const float COLOR_DIFFUSION_RATE = 0.01f; // Dc
        public const float MIXING_K = 2.0f;              // k in β = 1 - e^(-k|v|)

        // ── Suspension Point ─────────────────────────────────────────────
        public const float SUSPENSION_X = 0.0f;
        public const float SUSPENSION_Y = 3.0f;         // m above canvas
        public const float SUSPENSION_Z = 0.0f;
    }
}
