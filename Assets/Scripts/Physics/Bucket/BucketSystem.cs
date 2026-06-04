// ============================================================================
// BucketSystem.cs — Bucket dynamics simulation
// Swinging Paint Bucket Simulation
// ============================================================================
// From the study:
//   Forces: ma = Fg + FT + Fd + Fjet + Fwind
//   Rotation: I(dω/dt) = Στ,  ω(t+Δt) = ω(t) + (τ/I)Δt
//   I = ½ m_total R²  (cylinder approximation)
//   Mass: m_total(t) = m_bucket + m_paint(t)
//   Jet reaction: F_jet = -ṁ v_out,  τ_jet = r_hole × F_jet
// ============================================================================

using UnityEngine;
using SwingingPaintBucket.Core;
using SwingingPaintBucket.Utilities;

namespace SwingingPaintBucket.Physics
{
    /// <summary>
    /// Simulates bucket dynamics including:
    /// - Dynamic mass (paint depletion)
    /// - Moment of inertia update
    /// - Rotational dynamics (angular velocity, torques)
    /// - Force summation (gravity, tension, drag, jet reaction, wind)
    /// </summary>
    public class BucketSystem
    {
        /// <summary>
        /// Updates bucket state for one fixed timestep.
        /// </summary>
        public void UpdatePhysics(float dt, SimulationState state, SimulationConfig config)
        {
            // 1. Update total mass: m_total = m_bucket + m_paint(t)
            state.totalMass = config.bucket.mass + state.paintMassRemaining;

            // 2. Update moment of inertia: I = ½ m_total R²
            // Using cylinder approximation for the bucket
            float R = config.bucket.radius;
            state.momentOfInertia = 0.5f * state.totalMass * R * R;

            // Prevent zero inertia
            if (state.momentOfInertia < PhysicsConstants.EPSILON)
                state.momentOfInertia = PhysicsConstants.EPSILON;

            // 3. Compute all forces
            Vector3 totalForce = ComputeTotalForce(state, config);

            // 4. Update bucket acceleration: a = F / m
            if (state.totalMass > PhysicsConstants.EPSILON)
                state.bucketAcceleration = totalForce / state.totalMass;

            // 5. Compute all torques and update rotation
            UpdateRotation(dt, state, config);
        }

        /// <summary>
        /// Computes the total force on the bucket.
        /// From study: ma = Fg + FT + Fd + Fjet + Fwind
        /// </summary>
        private Vector3 ComputeTotalForce(SimulationState state, SimulationConfig config)
        {
            Vector3 totalForce = Vector3.zero;

            // ── Gravity: Fg = (0, -mg, 0) ──────────────────────────────
            Vector3 gravityForce = new Vector3(0.0f, -state.totalMass * config.environment.gravity, 0.0f);
            totalForce += gravityForce;

            // ── Rope Tension: FT = T r̂ ─────────────────────────────────
            // Direction: from bucket toward suspension point
            Vector3 ropeDir = (config.suspension.position - state.bucketPosition).normalized;
            Vector3 tensionForce = state.ropeTension * ropeDir;
            totalForce += tensionForce;

            // ── Air Drag: Fd = -½ ρ Cd A |v| v ─────────────────────────
            Vector3 velocity = state.bucketVelocity;
            float speed = velocity.magnitude;
            if (speed > PhysicsConstants.EPSILON)
            {
                // Approximate bucket cross-section area as circle: A = πR²
                float dragArea = PhysicsConstants.PI * config.bucket.radius * config.bucket.radius;
                float dragMagnitude = 0.5f * config.environment.airDensity *
                    PhysicsConstants.DEFAULT_AIR_DRAG_COEFFICIENT * dragArea * speed;
                Vector3 dragForce = -dragMagnitude * velocity.normalized;
                totalForce += dragForce;
            }

            // ── Jet Reaction: Fjet = -ṁ v_out ──────────────────────────
            // Paint leaving the bucket imparts a reaction force
            if (state.flowRate > PhysicsConstants.EPSILON)
            {
                float massFlowRate = config.paint.density * state.flowRate;
                Vector3 jetForce = -massFlowRate * state.exitVelocity;
                totalForce += jetForce;
            }

            // ── Wind: Fwind = kw(v_wind - v_bucket) ─────────────────────
            if (config.environment.windEnabled)
            {
                Vector3 windForce = config.environment.windCoefficient *
                    (config.environment.windVelocity - state.bucketVelocity);
                totalForce += windForce;
            }

            return totalForce;
        }

        /// <summary>
        /// Updates bucket rotation.
        /// From study:
        ///   I(dω/dt) = Στ
        ///   ω(t+Δt) = ω(t) + (τ/I)Δt
        /// 
        /// Torques include:
        ///   - Jet reaction torque: τ_jet = r_hole × F_jet
        ///   - Rope torsion: τ = -kt θt
        ///   - Air drag torque (proportional to angular velocity)
        /// </summary>
        private void UpdateRotation(float dt, SimulationState state, SimulationConfig config)
        {
            Vector3 totalTorque = Vector3.zero;

            // ── Jet reaction torque: τ_jet = r_hole × F_jet ────────────
            if (state.flowRate > PhysicsConstants.EPSILON)
            {
                // r_hole: position of orifice relative to bucket center
                Vector3 rHoleLocal = GetOrificeLocalPosition(config);
                // Rotate to world frame
                Vector3 rHoleWorld = state.bucketRotation * rHoleLocal;

                float massFlowRate = config.paint.density * state.flowRate;
                Vector3 jetForce = -massFlowRate * state.exitVelocity;

                // τ = r × F
                Vector3 jetTorque = Vector3.Cross(rHoleWorld, jetForce);
                totalTorque += jetTorque;
            }

            // ── Rope torsion: τ = -kt θt ───────────────────────────────
            if (config.rope.torsionStiffness > PhysicsConstants.EPSILON)
            {
                Vector3 ropeDir = (config.suspension.position - state.bucketPosition).normalized;
                float torsionMagnitude = -config.rope.torsionStiffness * state.twistAngle;
                totalTorque += torsionMagnitude * ropeDir;
            }

            // ── Rope tension restoring torque (since attachment point is at the hanger, offset from center of mass) ──
            {
                Vector3 rAttachmentLocal = new Vector3(0.0f, 1.55f * config.bucket.height, 0.0f);
                Vector3 rAttachmentWorld = state.bucketRotation * rAttachmentLocal;
                Vector3 ropeDir = (config.suspension.position - state.bucketPosition).normalized;
                Vector3 tensionForce = state.ropeTension * ropeDir;
                Vector3 tensionTorque = Vector3.Cross(rAttachmentWorld, tensionForce);
                totalTorque += tensionTorque;
            }

            // ── Air drag torque (rotational damping) ────────────────────
            float rotDampCoeff = config.environment.airDampingCoefficient * 0.1f; // Reduced for rotation
            totalTorque -= rotDampCoeff * state.bucketAngularVelocity;

            // ── Update angular velocity: ω(t+Δt) = ω(t) + (τ/I)Δt ────
            Vector3 angularAcceleration = totalTorque / state.momentOfInertia;
            state.bucketAngularVelocity += angularAcceleration * dt;

            // ── Update rotation quaternion ──────────────────────────────
            Quaternion deltaRotation = MathUtils.AngularVelocityToQuaternion(state.bucketAngularVelocity, dt);
            state.bucketRotation = deltaRotation * state.bucketRotation;
            state.bucketRotation.Normalize();

            // ── Update twist angle ──────────────────────────────────────
            Vector3 ropeDirection = (config.suspension.position - state.bucketPosition).normalized;
            float twistRate = Vector3.Dot(state.bucketAngularVelocity, ropeDirection);
            state.twistAngle += twistRate * dt;
        }

        /// <summary>
        /// Returns the local position of the orifice relative to bucket center.
        /// Bottom orifice: (0, -height/2, 0)
        /// Side orifice: (offset, 0, 0) — rotated with bucket
        /// </summary>
        private Vector3 GetOrificeLocalPosition(SimulationConfig config)
        {
            switch (config.bucket.orificePosition)
            {
                case OrificePosition.Bottom:
                    return new Vector3(config.bucket.orificeOffsetFromCenter, -config.bucket.height * 0.5f, 0.0f);
                case OrificePosition.Side:
                    return new Vector3(config.bucket.radius, -config.bucket.height * 0.3f, 0.0f);
                default:
                    return new Vector3(0.0f, -config.bucket.height * 0.5f, 0.0f);
            }
        }

        /// <summary>
        /// Returns the world position of the orifice.
        /// For side holes: n̂(t) = R(t) · n̂₀ (rotates with bucket)
        /// </summary>
        public Vector3 GetOrificeWorldPosition(SimulationState state, SimulationConfig config)
        {
            Vector3 localPos = GetOrificeLocalPosition(config);
            return state.bucketPosition + state.bucketRotation * localPos;
        }

        /// <summary>
        /// Returns the orifice exit direction in world space.
        /// Bottom: straight down (0, -1, 0)
        /// Side: rotates with bucket: n̂(t) = R(t) · n̂₀
        /// From study: n̂(t) = R(t) · n̂₀ for side orifice
        /// </summary>
        public Vector3 GetOrificeExitDirection(SimulationState state, SimulationConfig config)
        {
            switch (config.bucket.orificePosition)
            {
                case OrificePosition.Bottom:
                    return Vector3.down;
                case OrificePosition.Side:
                    // Outward from the bucket center at the orifice position
                    Vector3 localDir = new Vector3(1.0f, 0.0f, 0.0f).normalized;
                    return state.bucketRotation * localDir;
                default:
                    return Vector3.down;
            }
        }

        /// <summary>
        /// Computes center of mass considering paint level.
        /// h_cm = (m_bucket · h_bucket_cm + m_paint · h_paint_cm) / m_total
        /// </summary>
        public static float ComputeCenterOfMassHeight(SimulationConfig config, float paintHeight)
        {
            float bucketMass = config.bucket.mass;
            float paintMass = config.paint.density * PhysicsConstants.PI *
                config.bucket.radius * config.bucket.radius * paintHeight;

            float bucketCm = config.bucket.height * 0.5f;
            float paintCm = paintHeight * 0.5f; // center of fluid column

            float totalMass = bucketMass + paintMass;
            if (totalMass < PhysicsConstants.EPSILON) return bucketCm;

            return (bucketMass * bucketCm + paintMass * paintCm) / totalMass;
        }
    }
}
