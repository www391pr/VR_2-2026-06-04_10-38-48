// ============================================================================
// SimulationConfig.cs — ScriptableObject for all simulation parameters
// Swinging Paint Bucket Simulation
// ============================================================================
// This is the central configuration object that holds ALL tunable parameters.
// Users modify these through the UI before or during simulation.
// ============================================================================

using UnityEngine;
using System;

namespace SwingingPaintBucket.Core
{
    /// <summary>
    /// Enumeration of orifice shapes with corresponding discharge coefficients.
    /// From study: Cd values depend on shape.
    /// </summary>
    public enum OrificeShape
    {
        Circular,       // Cd ≈ 0.6,  A = πr²
        SharpEdged,     // Cd ≈ 0.62, A = πr²
        Tubular,        // Cd ≈ 0.8,  A = πr²
        LongitudinalSlit // Cd ≈ 0.6, A = w·l
    }

    /// <summary>
    /// Bucket shape determines how cross-section area varies with height.
    /// From study: cylindrical R(z) = R₀, conical R(z) = R₀ + kz
    /// </summary>
    public enum BucketShape
    {
        Cylindrical,  // R(z) = R₀ (constant cross-section)
        Conical       // R(z) = R₀ + kz (variable cross-section)
    }

    /// <summary>
    /// Orifice position on the bucket affects exit direction.
    /// From study: side orifice rotates with bucket n̂(t) = R(t)·n̂₀
    /// </summary>
    public enum OrificePosition
    {
        Bottom,  // Downward flow
        Side     // Direction rotates with bucket
    }

    /// <summary>
    /// Canvas surface type affects absorption, friction, and spread.
    /// From project requirements.
    /// </summary>
    public enum CanvasSurfaceType
    {
        Fabric,   // High absorption, high friction, low spread
        Wood,     // Medium absorption, medium friction, medium spread
        Metal,    // Very low absorption, low friction, high spread
        Paper     // Very high absorption, very high friction, low spread
    }

    // ========================================================================
    // CONFIGURATION SUB-STRUCTURES
    // ========================================================================

    /// <summary>
    /// Bucket physical properties.
    /// </summary>
    [Serializable]
    public class BucketConfig
    {
        [Header("Physical Properties")]
        [Tooltip("Mass of the empty bucket (kg)")]
        [Range(0.05f, 10.0f)]
        public float mass = Utilities.DefaultValues.BUCKET_MASS;

        [Tooltip("Bucket radius (m)")]
        [Range(0.02f, 0.5f)]
        public float radius = Utilities.DefaultValues.BUCKET_RADIUS;

        [Tooltip("Bucket height (m)")]
        [Range(0.05f, 0.5f)]
        public float height = Utilities.DefaultValues.BUCKET_HEIGHT;

        [Header("Shape")]
        [Tooltip("Bucket shape (cylindrical or conical)")]
        public BucketShape shape = BucketShape.Cylindrical;

        [Tooltip("Conical slope factor k in R(z) = R₀ + kz (only for conical)")]
        [Range(-0.5f, 0.5f)]
        public float conicalSlope = 0.0f;

        [Header("Orifice")]
        [Tooltip("Orifice radius (m)")]
        [Range(0.001f, 0.05f)]
        public float orificeRadius = Utilities.DefaultValues.ORIFICE_RADIUS;

        [Tooltip("Orifice shape")]
        public OrificeShape orificeShape = OrificeShape.Circular;

        [Tooltip("Orifice position")]
        public OrificePosition orificePosition = OrificePosition.Bottom;

        [Tooltip("Slit width (m) — only for longitudinal slit")]
        [Range(0.001f, 0.02f)]
        public float slitWidth = 0.003f;

        [Tooltip("Slit length (m) — only for longitudinal slit")]
        [Range(0.005f, 0.1f)]
        public float slitLength = 0.02f;

        [Tooltip("Distance of orifice from bucket center (m) — for side orifice")]
        [Range(0.0f, 0.5f)]
        public float orificeOffsetFromCenter = 0.0f;

        /// <summary>
        /// Returns discharge coefficient based on orifice shape.
        /// From study: Cd depends on shape.
        /// </summary>
        public float GetDischargeCoefficient()
        {
            switch (orificeShape)
            {
                case OrificeShape.Circular: return Utilities.PhysicsConstants.CD_CIRCULAR;
                case OrificeShape.SharpEdged: return Utilities.PhysicsConstants.CD_SHARP_EDGED;
                case OrificeShape.Tubular: return Utilities.PhysicsConstants.CD_TUBULAR;
                case OrificeShape.LongitudinalSlit: return Utilities.PhysicsConstants.CD_CIRCULAR;
                default: return Utilities.PhysicsConstants.CD_CIRCULAR;
            }
        }

        /// <summary>
        /// Returns orifice area based on shape.
        /// From study: circular A=πr², slit A=w·l
        /// </summary>
        public float GetOrificeArea()
        {
            switch (orificeShape)
            {
                case OrificeShape.Circular:
                case OrificeShape.SharpEdged:
                case OrificeShape.Tubular:
                    return Utilities.MathUtils.CircleArea(orificeRadius);
                case OrificeShape.LongitudinalSlit:
                    return slitWidth * slitLength;
                default:
                    return Utilities.MathUtils.CircleArea(orificeRadius);
            }
        }

        /// <summary>
        /// Returns cross-sectional area at height z.
        /// From study: cylindrical A = πR₀², conical A = π(R₀ + kz)²
        /// </summary>
        public float GetCrossSectionArea(float z)
        {
            float r = GetRadiusAtHeight(z);
            return Utilities.PhysicsConstants.PI * r * r;
        }

        /// <summary>
        /// Returns bucket radius at height z.
        /// Cylindrical: R(z) = R₀
        /// Conical: R(z) = R₀ + kz
        /// </summary>
        public float GetRadiusAtHeight(float z)
        {
            if (shape == BucketShape.Conical)
                return radius + conicalSlope * z;
            return radius;
        }
    }

    /// <summary>
    /// Rope physical properties.
    /// </summary>
    [Serializable]
    public class RopeConfig
    {
        [Header("Geometry")]
        [Tooltip("Rope length (m)")]
        [Range(0.1f, 10.0f)]
        public float length = Utilities.DefaultValues.ROPE_LENGTH;

        [Header("Elasticity")]
        [Tooltip("Enable rope elasticity (Hooke's law spring behavior)")]
        public bool isElastic = false;

        [Tooltip("Spring stiffness ks (N/m) — only when elastic")]
        [Range(100.0f, 50000.0f)]
        public float stiffness = Utilities.DefaultValues.ROPE_STIFFNESS;

        [Tooltip("Damping coefficient kd (Ns/m) — only when elastic")]
        [Range(0.0f, 100.0f)]
        public float damping = Utilities.DefaultValues.ROPE_DAMPING;

        [Header("Torsion")]
        [Tooltip("Torsion stiffness kt (Nm/rad)")]
        [Range(0.0f, 5.0f)]
        public float torsionStiffness = Utilities.DefaultValues.ROPE_TORSION_STIFFNESS;

        [Header("Damping")]
        [Tooltip("Rope friction damping coefficient k_rope")]
        [Range(0.0f, 0.5f)]
        public float frictionDamping = Utilities.DefaultValues.ROPE_FRICTION_DAMPING;
    }

    /// <summary>
    /// Initial motion parameters.
    /// </summary>
    [Serializable]
    public class MotionConfig
    {
        [Header("Initial Angles")]
        [Tooltip("Initial polar angle θ₀ (degrees, from vertical)")]
        [Range(0.0f, 90.0f)]
        public float initialThetaDegrees = Utilities.DefaultValues.INITIAL_THETA;

        [Tooltip("Initial azimuthal angle ϕ₀ (degrees)")]
        [Range(0.0f, 360.0f)]
        public float initialPhiDegrees = Utilities.DefaultValues.INITIAL_PHI;

        [Header("Initial Velocity")]
        [Tooltip("Initial tangential velocity (m/s)")]
        [Range(0.0f, 10.0f)]
        public float initialVelocity = Utilities.DefaultValues.INITIAL_VELOCITY;

        [Tooltip("Direction of initial velocity push (degrees, 0 = along θ)")]
        [Range(0.0f, 360.0f)]
        public float initialVelocityDirectionDegrees = 0.0f;

        [Header("Swing")]
        [Tooltip("Maximum number of swings (0 = infinite)")]
        [Range(0, 1000)]
        public int maxSwingCount = 0;

        /// <summary>Initial θ in radians.</summary>
        public float InitialThetaRad => initialThetaDegrees * Utilities.PhysicsConstants.DEG_TO_RAD;

        /// <summary>Initial ϕ in radians.</summary>
        public float InitialPhiRad => initialPhiDegrees * Utilities.PhysicsConstants.DEG_TO_RAD;
    }

    /// <summary>
    /// Paint physical properties.
    /// </summary>
    [Serializable]
    public class PaintConfig
    {
        [Header("Volume")]
        [Tooltip("Initial paint volume (m³)")]
        [Range(0.0001f, 0.01f)]
        public float initialVolume = Utilities.DefaultValues.PAINT_VOLUME;

        [Header("Physical Properties")]
        [Tooltip("Paint density ρ (kg/m³)")]
        [Range(800.0f, 2000.0f)]
        public float density = Utilities.DefaultValues.PAINT_DENSITY;

        [Tooltip("Paint viscosity μ (Pa·s)")]
        [Range(0.01f, 10.0f)]
        public float viscosity = Utilities.DefaultValues.PAINT_VISCOSITY;

        [Tooltip("Surface tension σ (N/m)")]
        [Range(0.001f, 0.1f)]
        public float surfaceTension = Utilities.DefaultValues.SURFACE_TENSION;

        [Header("SPH Parameters")]
        [Tooltip("Rest density ρ₀ for SPH pressure calculation")]
        [Range(800.0f, 2000.0f)]
        public float restDensity = Utilities.DefaultValues.PAINT_REST_DENSITY;

        [Tooltip("Stiffness k in pressure equation P = k(ρ - ρ₀)")]
        [Range(1.0f, 500.0f)]
        public float sphStiffness = Utilities.DefaultValues.SPH_STIFFNESS;

        [Tooltip("SPH smoothing radius h")]
        [Range(0.01f, 0.2f)]
        public float smoothingRadius = Utilities.DefaultValues.SPH_SMOOTHING_RADIUS;

        [Header("Color")]
        [Tooltip("Paint colors (supports multiple)")]
        public Color[] colors = new Color[] { Color.red };

        [Header("Splash Parameters")]
        [Tooltip("Critical Weber number for splash")]
        [Range(10.0f, 200.0f)]
        public float weberCritical = Utilities.DefaultValues.WEBER_CRITICAL;

        [Tooltip("Splash count coefficient ks in N_splash = ks(We/We_crit - 1)")]
        [Range(1.0f, 20.0f)]
        public float splashCountCoefficient = Utilities.DefaultValues.SPLASH_KS;

        [Tooltip("Mass fraction η for secondary droplets")]
        [Range(0.01f, 0.5f)]
        public float splashMassFraction = Utilities.DefaultValues.SPLASH_ETA;

        [Tooltip("Velocity fraction kv for secondary droplets: vs = kv * vn")]
        [Range(0.1f, 1.0f)]
        public float splashVelocityFraction = Utilities.DefaultValues.SPLASH_KV;

        [Header("Color Mixing")]
        [Tooltip("Color diffusion rate Dc")]
        [Range(0.001f, 0.1f)]
        public float colorDiffusionRate = Utilities.DefaultValues.COLOR_DIFFUSION_RATE;

        [Tooltip("Mixing constant k in β = 1 - e^(-k|v|)")]
        [Range(0.1f, 10.0f)]
        public float mixingConstant = Utilities.DefaultValues.MIXING_K;

        /// <summary>Initial paint mass = density × volume</summary>
        public float InitialMass => density * initialVolume;

        /// <summary>
        /// Initial paint height inside bucket.
        /// For cylindrical bucket: h = V / (πR²)
        /// </summary>
        public float GetInitialHeight(float bucketRadius)
        {
            float area = Utilities.PhysicsConstants.PI * bucketRadius * bucketRadius;
            return initialVolume / area;
        }
    }

    /// <summary>
    /// Canvas physical properties.
    /// </summary>
    [Serializable]
    public class CanvasConfig
    {
        [Header("Dimensions")]
        [Tooltip("Canvas width (m)")]
        [Range(0.5f, 10.0f)]
        public float width = Utilities.DefaultValues.CANVAS_WIDTH;

        [Tooltip("Canvas height/depth (m)")]
        [Range(0.5f, 10.0f)]
        public float height = Utilities.DefaultValues.CANVAS_HEIGHT;

        [Header("Surface")]
        [Tooltip("Surface type")]
        public CanvasSurfaceType surfaceType = CanvasSurfaceType.Fabric;

        [Header("Orientation")]
        [Tooltip("Canvas inclination angle from horizontal (degrees). 0 = flat horizontal, 90 = vertical")]
        [Range(0.0f, 90.0f)]
        public float inclinationDegrees = 0.0f;

        [Tooltip("Canvas inclination direction (degrees, azimuth)")]
        [Range(0.0f, 360.0f)]
        public float inclinationDirectionDegrees = 0.0f;

        [Header("Position")]
        [Tooltip("Canvas center Y position (m)")]
        public float yPosition = 0.0f;

        [Header("Rendering")]
        [Tooltip("Canvas texture resolution")]
        [Range(512, 4096)]
        public int textureResolution = Utilities.SimulationLimits.DEFAULT_CANVAS_RESOLUTION;

        /// <summary>
        /// Returns surface properties based on surface type.
        /// </summary>
        public void GetSurfaceProperties(out float absorption, out float friction, out float spreadRate)
        {
            switch (surfaceType)
            {
                case CanvasSurfaceType.Fabric:
                    absorption = 0.3f; friction = 0.8f; spreadRate = 0.1f; break;
                case CanvasSurfaceType.Wood:
                    absorption = 0.1f; friction = 0.5f; spreadRate = 0.3f; break;
                case CanvasSurfaceType.Metal:
                    absorption = 0.01f; friction = 0.2f; spreadRate = 0.8f; break;
                case CanvasSurfaceType.Paper:
                    absorption = 0.5f; friction = 0.9f; spreadRate = 0.05f; break;
                default:
                    absorption = 0.3f; friction = 0.8f; spreadRate = 0.1f; break;
            }
        }

        /// <summary>
        /// Canvas normal vector (from inclination).
        /// Flat canvas normal = (0,1,0).
        /// Tilted canvas rotates the normal.
        /// </summary>
        public Vector3 GetNormal()
        {
            if (inclinationDegrees < Utilities.PhysicsConstants.EPSILON)
                return Vector3.up;

            float incRad = inclinationDegrees * Utilities.PhysicsConstants.DEG_TO_RAD;
            float dirRad = inclinationDirectionDegrees * Utilities.PhysicsConstants.DEG_TO_RAD;

            return new Vector3(
                -Mathf.Sin(incRad) * Mathf.Cos(dirRad),
                Mathf.Cos(incRad),
                -Mathf.Sin(incRad) * Mathf.Sin(dirRad)
            ).normalized;
        }
    }

    /// <summary>
    /// Environment physical properties.
    /// </summary>
    [Serializable]
    public class EnvironmentConfig
    {
        [Header("Gravity")]
        [Tooltip("Gravitational acceleration (m/s²)")]
        [Range(0.1f, 30.0f)]
        public float gravity = Utilities.PhysicsConstants.DEFAULT_GRAVITY;

        [Header("Air")]
        [Tooltip("Air density (kg/m³)")]
        [Range(0.5f, 2.0f)]
        public float airDensity = Utilities.PhysicsConstants.DEFAULT_AIR_DENSITY;

        [Tooltip("Air drag coefficient for pendulum damping")]
        [Range(0.0f, 0.5f)]
        public float airDampingCoefficient = Utilities.DefaultValues.AIR_DAMPING;

        [Header("Wind")]
        [Tooltip("Enable wind")]
        public bool windEnabled = false;

        [Tooltip("Wind velocity vector (m/s)")]
        public Vector3 windVelocity = Vector3.zero;

        [Tooltip("Wind coupling constant kw")]
        [Range(0.0f, 5.0f)]
        public float windCoefficient = 1.0f;

        [Header("Atmosphere")]
        [Tooltip("Relative humidity (0-1)")]
        [Range(0.0f, 1.0f)]
        public float humidity = Utilities.DefaultValues.HUMIDITY;

        [Tooltip("Temperature (Kelvin)")]
        [Range(250.0f, 350.0f)]
        public float temperature = Utilities.DefaultValues.TEMPERATURE;

        [Tooltip("Humidity effect on viscosity: μ_eff = μ(1 + k_h · humidity)")]
        [Range(0.0f, 1.0f)]
        public float humidityViscosityFactor = 0.2f;
    }

    /// <summary>
    /// Suspension point configuration.
    /// </summary>
    [Serializable]
    public class SuspensionConfig
    {
        [Tooltip("Suspension point position")]
        public Vector3 position = new Vector3(
            Utilities.DefaultValues.SUSPENSION_X,
            Utilities.DefaultValues.SUSPENSION_Y,
            Utilities.DefaultValues.SUSPENSION_Z
        );
    }

    // ========================================================================
    // MAIN CONFIGURATION SCRIPTABLE OBJECT
    // ========================================================================

    /// <summary>
    /// Master configuration object for the entire simulation.
    /// Create instances via Assets → Create → SwingingPaintBucket → SimulationConfig
    /// </summary>
    [CreateAssetMenu(fileName = "SimulationConfig", menuName = "SwingingPaintBucket/SimulationConfig")]
    public class SimulationConfig : ScriptableObject
    {
        [Header("=== BUCKET ===")]
        public BucketConfig bucket = new BucketConfig();

        [Header("=== ROPE ===")]
        public RopeConfig rope = new RopeConfig();

        [Header("=== MOTION ===")]
        public MotionConfig motion = new MotionConfig();

        [Header("=== PAINT ===")]
        public PaintConfig paint = new PaintConfig();

        [Header("=== CANVAS ===")]
        public CanvasConfig canvas = new CanvasConfig();

        [Header("=== ENVIRONMENT ===")]
        public EnvironmentConfig environment = new EnvironmentConfig();

        [Header("=== SUSPENSION POINT ===")]
        public SuspensionConfig suspension = new SuspensionConfig();

        [Header("=== SIMULATION QUALITY ===")]
        [Tooltip("Fixed physics timestep (seconds)")]
        [Range(0.001f, 0.02f)]
        public float fixedTimeStep = Utilities.SimulationLimits.FIXED_TIMESTEP;

        [Tooltip("Maximum number of particles")]
        [Range(1000, 1100000)]
        public int maxParticles = Utilities.SimulationLimits.DEFAULT_MAX_PARTICLES;

        [Tooltip("Particle mass (kg) — derived from paint density and particle count")]
        public float particleMass = 0.001f;

        /// <summary>
        /// Creates a deep copy of this configuration for experiment snapshots.
        /// </summary>
        public SimulationConfig DeepCopy()
        {
            SimulationConfig copy = CreateInstance<SimulationConfig>();
            string json = JsonUtility.ToJson(this);
            JsonUtility.FromJsonOverwrite(json, copy);
            return copy;
        }

        /// <summary>
        /// Returns effective paint viscosity accounting for environment.
        /// μ_eff = μ · (1 + k_h · humidity)
        /// From the study: environmental effects on viscosity.
        /// </summary>
        public float GetEffectiveViscosity()
        {
            return paint.viscosity * (1.0f + environment.humidityViscosityFactor * environment.humidity);
        }
    }
}
