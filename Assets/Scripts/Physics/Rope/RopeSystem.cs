// ============================================================================
// RopeSystem.cs — Rope mechanics simulation
// Swinging Paint Bucket Simulation
// ============================================================================
// From the study:
//   Rigid: |r(t) - r₀| = L (constant length)
//   Elastic: F_rope = -ks(|r - r₀| - L)r̂ - kd v
//   Torsion: τ = -kt θt
//   Tension: T = m(g cos(θ) + L(θ̇² + sin²(θ)ϕ̇²))
// ============================================================================

using UnityEngine;
using SwingingPaintBucket.Core;
using SwingingPaintBucket.Utilities;

namespace SwingingPaintBucket.Physics
{
    /// <summary>
    /// Simulates rope mechanics including tension calculation,
    /// optional elasticity (Hooke's law), and torsion.
    /// </summary>
    public class RopeSystem
    {
        /// <summary>
        /// Updates rope state for one fixed timestep.
        /// </summary>
        public void UpdatePhysics(float dt, SimulationState state, SimulationConfig config)
        {
            ComputeTension(state, config);

            if (config.rope.isElastic)
            {
                UpdateElasticRope(dt, state, config);
            }
            else
            {
                // Rigid rope: length stays constant
                state.currentRopeLength = config.rope.length;
            }

            // Torsion: τ = -kt θt
            UpdateTorsion(dt, state, config);
        }

        /// <summary>
        /// Computes rope tension for rigid rope.
        /// From study (derived from centripetal acceleration requirement):
        ///   T = m(g cos(θ) + L(θ̇² + sin²(θ)ϕ̇²))
        /// This ensures the constraint |r - r₀| = L is maintained.
        /// </summary>
        private void ComputeTension(SimulationState state, SimulationConfig config)
        {
            float m = state.totalMass;
            float g = config.environment.gravity;
            float L = state.currentRopeLength;
            float theta = state.theta;
            float thetaDot = state.thetaDot;
            float phiDot = state.phiDot;

            float cosTheta = Mathf.Cos(theta);
            float sinTheta = Mathf.Sin(theta);

            // Centripetal terms
            float centripetalTerm = L * (thetaDot * thetaDot + sinTheta * sinTheta * phiDot * phiDot);

            // Tension
            state.ropeTension = m * (g * cosTheta + centripetalTerm);

            // Tension must be non-negative (rope can't push)
            if (state.ropeTension < 0.0f)
                state.ropeTension = 0.0f;
        }

        /// <summary>
        /// Updates elastic rope using Hooke's law.
        /// From study:
        ///   F_rope = -ks(|r - r₀| - L)r̂ - kd v
        /// Where:
        ///   ks: spring stiffness
        ///   kd: damping coefficient
        ///   L: natural rope length (L₀)
        ///   |r - r₀|: current distance from suspension
        /// </summary>
        private void UpdateElasticRope(float dt, SimulationState state, SimulationConfig config)
        {
            Vector3 suspensionPos = config.suspension.position;
            Vector3 bucketPos = state.bucketPosition;

            Vector3 displacement = bucketPos - suspensionPos;
            float currentLength = displacement.magnitude;

            if (currentLength < PhysicsConstants.EPSILON)
                return;

            Vector3 direction = displacement / currentLength; // r̂

            float naturalLength = config.rope.length;
            float stretch = currentLength - naturalLength;

            // Hooke's law: F = -ks · stretch
            float springForce = -config.rope.stiffness * stretch;

            // Damping: F = -kd · v_radial
            float radialVelocity = Vector3.Dot(state.bucketVelocity, direction);
            float dampingForce = -config.rope.damping * radialVelocity;

            // Total radial force
            float totalRadialForce = springForce + dampingForce;

            // Apply force as modification to tension
            state.ropeTension = Mathf.Max(0.0f, state.ropeTension + totalRadialForce);

            // Update current rope length
            state.currentRopeLength = currentLength;
        }

        /// <summary>
        /// Updates rope torsion.
        /// From study: τ = -kt θt
        /// Torsion produces a torque on the bucket that opposes twist.
        /// </summary>
        private void UpdateTorsion(float dt, SimulationState state, SimulationConfig config)
        {
            if (config.rope.torsionStiffness < PhysicsConstants.EPSILON)
                return;

            // Torsion torque: τ = -kt θt
            float torsionTorque = -config.rope.torsionStiffness * state.twistAngle;

            // Update twist angle based on bucket angular velocity
            // The twist accumulates from the component of ω along the rope direction
            Vector3 ropeDirection = (config.suspension.position - state.bucketPosition).normalized;
            float twistRate = Vector3.Dot(state.bucketAngularVelocity, ropeDirection);

            state.twistAngle += twistRate * dt;

            // Apply torsion damping
            float torsionDamping = config.rope.frictionDamping * state.twistAngle;
            state.twistAngle -= torsionDamping * dt;
        }

        /// <summary>
        /// Returns the rope force vector for elastic rope.
        /// F_rope = -ks(|r - r₀| - L)r̂ - kd v
        /// Used by BucketSystem for force summation.
        /// </summary>
        public Vector3 GetElasticForce(SimulationState state, SimulationConfig config)
        {
            if (!config.rope.isElastic)
                return Vector3.zero;

            Vector3 displacement = state.bucketPosition - config.suspension.position;
            float currentLength = displacement.magnitude;

            if (currentLength < PhysicsConstants.EPSILON)
                return Vector3.zero;

            Vector3 direction = displacement / currentLength;
            float stretch = currentLength - config.rope.length;

            // F = -ks · stretch · r̂ - kd · v
            Vector3 springForce = -config.rope.stiffness * stretch * direction;
            float radialVelocity = Vector3.Dot(state.bucketVelocity, direction);
            Vector3 dampingForce = -config.rope.damping * radialVelocity * direction;

            return springForce + dampingForce;
        }

        /// <summary>
        /// Returns torsion torque vector.
        /// τ = -kt θt applied along rope axis.
        /// </summary>
        public Vector3 GetTorsionTorque(SimulationState state, SimulationConfig config)
        {
            Vector3 ropeDirection = (config.suspension.position - state.bucketPosition).normalized;
            float torqueMagnitude = -config.rope.torsionStiffness * state.twistAngle;
            return torqueMagnitude * ropeDirection;
        }
    }
}
