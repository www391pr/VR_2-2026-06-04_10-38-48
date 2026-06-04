// ============================================================================
// PendulumSystem.cs — Pendulum physics simulation
// Swinging Paint Bucket Simulation
// ============================================================================
// Implements the full nonlinear two-angle pendulum from the study.
// Equations:
//   θ̈ = sin(θ)cos(θ)ϕ̇² - (g/L)sin(θ) - c_θ θ̇
//   ϕ̈ = -(2θ̇ϕ̇)/tan(θ) - c_ϕ ϕ̇
//
// Damping coefficients:
//   c_θ = k_air + k_rope
//   c_ϕ = k_air
// ============================================================================

using UnityEngine;
using SwingingPaintBucket.Core;
using SwingingPaintBucket.Utilities;

namespace SwingingPaintBucket.Physics
{
    /// <summary>
    /// Solves the nonlinear two-angle pendulum equations using RK4 integration.
    /// Supports large-angle motion, damping, and multi-axis swing.
    /// 
    /// From the study:
    ///   Position: x = x₀ + L sin(θ) cos(ϕ)
    ///             y = y₀ - L cos(θ)
    ///             z = z₀ + L sin(θ) sin(ϕ)
    ///   
    ///   Velocity: vx = L(cos(θ)cos(ϕ)θ̇ - sin(θ)sin(ϕ)ϕ̇)
    ///             vy = L sin(θ) θ̇
    ///             vz = L(cos(θ)sin(ϕ)θ̇ + sin(θ)cos(ϕ)ϕ̇)
    /// </summary>
    public class PendulumSystem
    {
        // Cached parameters for acceleration functions
        private float _g;
        private float _L;
        private float _cTheta;
        private float _cPhi;

        /// <summary>
        /// Updates pendulum state for one fixed timestep.
        /// </summary>
        /// <param name="dt">Fixed timestep Δt</param>
        /// <param name="state">Simulation state (modified in place)</param>
        /// <param name="config">Simulation configuration</param>
        public void UpdatePhysics(float dt, SimulationState state, SimulationConfig config)
        {
            // Cache parameters for acceleration functions
            _g = config.environment.gravity;
            _L = state.currentRopeLength; // May differ from config if rope is elastic
            
            // Damping coefficients from study:
            // c_θ = k_air + k_rope
            // c_ϕ = k_air
            _cTheta = config.environment.airDampingCoefficient + config.rope.frictionDamping;
            _cPhi = config.environment.airDampingCoefficient;

            // Prevent division by zero when θ ≈ 0 (ϕ equation has 1/tan(θ))
            // When θ is very small, ϕ motion is physically negligible
            float theta = state.theta;
            float thetaDot = state.thetaDot;
            float phi = state.phi;
            float phiDot = state.phiDot;

            // Solve coupled system using RK4
            RK4Integrator.StepCoupled(
                ref theta, ref thetaDot,
                ref phi, ref phiDot,
                dt,
                ThetaAcceleration,
                PhiAcceleration
            );

            // Clamp theta to valid range [epsilon, π - epsilon]
            // θ = 0 would cause division by zero in ϕ equation
            theta = Mathf.Clamp(theta, -PhysicsConstants.PI + 0.001f, PhysicsConstants.PI - 0.001f);

            // Write back
            state.theta = theta;
            state.thetaDot = thetaDot;
            state.phi = phi;
            state.phiDot = phiDot;

            // Compute bucket position from angles
            // From study:
            // x = x₀ + L sin(θ) cos(ϕ)
            // y = y₀ - L cos(θ)
            // z = z₀ + L sin(θ) sin(ϕ)
            ComputeBucketPositionAndVelocity(state, config);
        }

        /// <summary>
        /// θ̈ = sin(θ)cos(θ)ϕ̇² - (g/L)sin(θ) - c_θ θ̇
        /// From the study: equation of motion for polar angle.
        /// </summary>
        private float ThetaAcceleration(float theta, float thetaDot, float phi, float phiDot)
        {
            if (_L < PhysicsConstants.EPSILON) return 0.0f;

            float sinTheta = Mathf.Sin(theta);
            float cosTheta = Mathf.Cos(theta);

            // Term 1: Centripetal coupling from ϕ motion
            float term1 = sinTheta * cosTheta * phiDot * phiDot;

            // Term 2: Gravity restoring force
            float term2 = -(_g / _L) * sinTheta;

            // Term 3: Damping
            float term3 = -_cTheta * thetaDot;

            return term1 + term2 + term3;
        }

        /// <summary>
        /// ϕ̈ = -(2θ̇ϕ̇)/tan(θ) - c_ϕ ϕ̇
        /// From the study: equation of motion for azimuthal angle.
        /// </summary>
        private float PhiAcceleration(float theta, float thetaDot, float phi, float phiDot)
        {
            float sinTheta = Mathf.Sin(theta);

            // When θ ≈ 0, tan(θ) ≈ 0, causing singularity.
            // In this case, ϕ motion is physically undefined (at the vertical).
            // We clamp to prevent numerical explosion.
            float tanTheta = Mathf.Tan(theta);
            if (Mathf.Abs(tanTheta) < PhysicsConstants.EPSILON)
            {
                // Only apply damping when near vertical
                return -_cPhi * phiDot;
            }

            // Term 1: Coupling from θ motion (Coriolis-like)
            float term1 = -(2.0f * thetaDot * phiDot) / tanTheta;

            // Term 2: Damping
            float term2 = -_cPhi * phiDot;

            return term1 + term2;
        }

        /// <summary>
        /// Computes bucket position and velocity from pendulum angles.
        /// From the study:
        ///   Position:
        ///     x = x₀ + L sin(θ) cos(ϕ)
        ///     y = y₀ - L cos(θ)
        ///     z = z₀ + L sin(θ) sin(ϕ)
        ///   Velocity:
        ///     vx = L(cos(θ)cos(ϕ)θ̇ - sin(θ)sin(ϕ)ϕ̇)
        ///     vy = L sin(θ) θ̇
        ///     vz = L(cos(θ)sin(ϕ)θ̇ + sin(θ)cos(ϕ)ϕ̇)
        /// </summary>
        private void ComputeBucketPositionAndVelocity(SimulationState state, SimulationConfig config)
        {
            Vector3 suspensionPos = config.suspension.position;
            float L = state.currentRopeLength;
            float theta = state.theta;
            float phi = state.phi;
            float thetaDot = state.thetaDot;
            float phiDot = state.phiDot;

            float sinTheta = Mathf.Sin(theta);
            float cosTheta = Mathf.Cos(theta);
            float sinPhi = Mathf.Sin(phi);
            float cosPhi = Mathf.Cos(phi);

            // Position from study (derived for the rope end/hanger point)
            Vector3 prevPosition = state.bucketPosition;
            Vector3 hangerPosition = new Vector3(
                suspensionPos.x + L * sinTheta * cosPhi,
                suspensionPos.y - L * cosTheta,
                suspensionPos.z + L * sinTheta * sinPhi
            );

            // Shift the center of mass (state.bucketPosition) down by the hanger offset
            Vector3 rOffsetWorld = state.bucketRotation * new Vector3(0.0f, 1.55f * config.bucket.height, 0.0f);
            state.bucketPosition = hangerPosition - rOffsetWorld;

            // Velocity from study (derived for the hanger point)
            Vector3 vHanger = new Vector3(
                L * (cosTheta * cosPhi * thetaDot - sinTheta * sinPhi * phiDot),
                L * sinTheta * thetaDot,
                L * (cosTheta * sinPhi * thetaDot + sinTheta * cosPhi * phiDot)
            );
            // v_bucket = v_hanger - w x r_offset
            state.bucketVelocity = vHanger - Vector3.Cross(state.bucketAngularVelocity, rOffsetWorld);

            // Acceleration (numerical from velocity change)
            // a = dv/dt — we approximate using finite difference
            // This is used for inertial forces on fluid particles
            // Note: more accurate would be analytical, but this suffices for the inertial frame
            if (state.physicsStepCount > 0)
            {
                float dt = config.fixedTimeStep;
                if (dt > PhysicsConstants.EPSILON)
                {
                    state.bucketAcceleration = (state.bucketVelocity - 
                        ((state.bucketPosition - prevPosition) / dt - state.bucketVelocity)) / dt;
                }
            }
        }

        /// <summary>
        /// Returns the pendulum period for small-angle validation.
        /// T = 2π√(L/g)
        /// Used in Section 14 (Validation Against Reality).
        /// </summary>
        public static float SmallAnglePeriod(float L, float g)
        {
            return PhysicsConstants.TWO_PI * Mathf.Sqrt(L / g);
        }

        /// <summary>
        /// Computes tangential acceleration of the bucket.
        /// a_t = Lθ̈ (from the study, used for sloshing calculation)
        /// </summary>
        public static float TangentialAcceleration(float L, float thetaDoubleDot)
        {
            return L * thetaDoubleDot;
        }
    }
}
