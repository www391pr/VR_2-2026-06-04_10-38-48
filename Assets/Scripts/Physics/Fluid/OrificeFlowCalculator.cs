// ============================================================================
// OrificeFlowCalculator.cs — Paint flow through orifice
// Swinging Paint Bucket Simulation
// ============================================================================
// From the study:
//   Q = Cd · A · √(2g h_eff)
//   v_out = v_bucket + √(2gh) n̂(t) + ω × r_hole + v_vortex
//   dh/dt = -Q / A_bucket = -(Cd · A_hole · √(2gh)) / (π R²_bucket)
//
// Effective height corrections:
//   h_eff = h_fluid - h_hole_position + Δh_sloshing
//   Sloshing: tan(α) = a_t / g, h(x,t) = h₀ + x·tan(α)
//   Vibration: Δh = A sin(ωt)
// ============================================================================

using UnityEngine;
using SwingingPaintBucket.Core;
using SwingingPaintBucket.Utilities;

namespace SwingingPaintBucket.Physics.Fluid
{
    /// <summary>
    /// Computes paint flow rate through the bucket orifice and spawns
    /// free-fall particles. Implements Torricelli's law with all corrections
    /// from the study.
    /// </summary>
    public class OrificeFlowCalculator
    {
        // Accumulator for sub-particle-mass flow
        private float _flowAccumulator = 0.0f;

        // Track current color index for multi-color support
        private int _currentColorIndex = 0;
        private float _colorSwitchTimer = 0.0f;

        /// <summary>
        /// Updates orifice flow for one fixed timestep.
        /// </summary>
        public void UpdatePhysics(float dt, SimulationState state, SimulationConfig config,
            ParticleManager particleManager)
        {
            // Check if paint is depleted
            if (state.paintMassRemaining <= PhysicsConstants.EPSILON)
            {
                state.paintDepleted = true;
                state.flowRate = 0.0f;
                state.exitVelocity = Vector3.zero;
                return;
            }

            // 1. Compute effective height at orifice
            float hEff = ComputeEffectiveHeight(state, config);
            state.effectiveHeight = hEff;

            // If effective height is zero or negative, no flow
            if (hEff <= 0.0f)
            {
                state.flowRate = 0.0f;
                state.exitVelocity = Vector3.zero;
                return;
            }

            // 2. Compute flow rate: Q = Cd · A · √(2g h_eff)
            float Cd = config.bucket.GetDischargeCoefficient();
            float A_hole = config.bucket.GetOrificeArea();
            float g = config.environment.gravity;

            float Q = MathUtils.OrificeFlowRate(Cd, A_hole, g, hEff);
            state.flowRate = Q;

            // 3. Compute exit velocity from study:
            // v_out = v_bucket + √(2gh) n̂(t) + ω × r_hole + v_vortex
            Vector3 exitVelocity = ComputeExitVelocity(state, config, hEff);
            state.exitVelocity = exitVelocity;

            // 4. Update paint level
            // dh/dt = -Q / A_bucket = -(Cd · A_hole · √(2gh)) / (π R²_bucket)
            float A_bucket = config.bucket.GetCrossSectionArea(state.paintHeight);
            if (A_bucket > PhysicsConstants.EPSILON)
            {
                float dhdt = -Q / A_bucket;
                state.paintHeight += dhdt * dt;
                state.paintHeight = Mathf.Max(0.0f, state.paintHeight);
            }

            // 5. Update paint mass: ṁ = ρ · Q
            float massFlowRate = config.paint.density * Q;
            state.paintMassRemaining -= massFlowRate * dt;
            state.paintMassRemaining = Mathf.Max(0.0f, state.paintMassRemaining);

            // 6. Spawn particles
            SpawnParticles(dt, state, config, particleManager, Q, exitVelocity, massFlowRate);

            // 7. Update color cycling (for multi-color)
            UpdateColorCycling(dt, config);
        }

        /// <summary>
        /// Computes effective height at the orifice.
        /// From study:
        ///   h_eff = h_fluid - h_hole_position + Δh_sloshing
        /// 
        /// Sloshing correction (from pendulum acceleration):
        ///   a_t = Lθ̈ (tangential acceleration)
        ///   tan(α) = a_t / g
        ///   h(x,t) = h₀ + x·tan(α)
        ///   h_eff = h₀ + x_hole · tan(α)
        /// 
        /// Vibration correction:
        ///   Δh = A sin(ωt)
        ///   h_eff = h + Δh
        /// </summary>
        private float ComputeEffectiveHeight(SimulationState state, SimulationConfig config)
        {
            float hFluid = state.paintHeight;

            // h_hole_position: height of the orifice above bucket bottom
            float hHolePosition = 0.0f; // Bottom orifice is at the bottom
            if (config.bucket.orificePosition == OrificePosition.Side)
            {
                hHolePosition = config.bucket.height * 0.2f; // 20% up from bottom
            }

            float hBase = hFluid - hHolePosition;

            // ── Sloshing correction ─────────────────────────────────────
            // From study: when the bucket swings, tangential acceleration
            // tilts the fluid surface.
            // a_t ≈ L θ̈ (approximate tangential acceleration)
            // tan(α) = a_t / g
            // h_eff = h₀ + x_hole · tan(α)
            float sloshingCorrection = 0.0f;
            float g = config.environment.gravity;

            if (g > PhysicsConstants.EPSILON)
            {
                // Compute approximate θ̈ from current state
                // θ̈ ≈ sin(θ)cos(θ)ϕ̇² - (g/L)sin(θ) - c_θ θ̇
                float L = state.currentRopeLength;
                float theta = state.theta;
                float sinTheta = Mathf.Sin(theta);
                float cosTheta = Mathf.Cos(theta);

                float cTheta = config.environment.airDampingCoefficient + config.rope.frictionDamping;
                float thetaDoubleDot = sinTheta * cosTheta * state.phiDot * state.phiDot
                    - (g / Mathf.Max(L, PhysicsConstants.EPSILON)) * sinTheta
                    - cTheta * state.thetaDot;

                // Tangential acceleration: a_t = L θ̈
                float at = L * thetaDoubleDot;

                // Surface tilt angle: tan(α) = a_t / g
                float tanAlpha = at / g;

                // x_hole: offset of orifice from bucket center
                float xHole = config.bucket.orificeOffsetFromCenter;

                sloshingCorrection = xHole * tanAlpha;
            }

            // ── Vibration correction ────────────────────────────────────
            // From study: h_eff = h + Δh, where Δh = A sin(ωt)
            // This models small-scale oscillation of the fluid surface
            // Amplitude scales with acceleration and fluid properties
            float vibrationAmplitude = state.bucketAcceleration.magnitude * 0.001f; // Small effect
            float vibrationFrequency = 5.0f; // Hz (typical sloshing frequency)
            float vibrationCorrection = vibrationAmplitude *
                Mathf.Sin(vibrationFrequency * PhysicsConstants.TWO_PI * state.elapsedTime);

            // Final effective height
            float hEff = hBase + sloshingCorrection + vibrationCorrection;
            return Mathf.Max(0.0f, hEff);
        }

        /// <summary>
        /// Computes exit velocity of paint from the orifice.
        /// From study:
        ///   v_out = v_bucket + √(2gh) n̂(t) + ω × r_hole + v_vortex
        /// 
        /// Where:
        ///   v_bucket: bucket translational velocity
        ///   √(2gh) n̂: Torricelli flow velocity in exit direction
        ///   ω × r_hole: contribution from bucket rotation at orifice
        ///   v_vortex = ω_f × r: internal fluid rotation
        /// </summary>
        private Vector3 ComputeExitVelocity(SimulationState state, SimulationConfig config, float hEff)
        {
            float g = config.environment.gravity;

            // 1. Bucket velocity
            Vector3 vBucket = state.bucketVelocity;

            // 2. Torricelli flow: √(2gh) in exit direction n̂
            float vTorricelli = Mathf.Sqrt(2.0f * g * hEff);
            Vector3 exitDir = GetExitDirection(state, config);
            Vector3 vFlow = vTorricelli * exitDir;

            // 3. Rotation contribution: ω × r_hole
            Vector3 rHoleLocal = GetOrificeLocalPosition(config);
            Vector3 rHoleWorld = state.bucketRotation * rHoleLocal;
            Vector3 vRotation = Vector3.Cross(state.bucketAngularVelocity, rHoleWorld);

            // 4. Vortex velocity: v_vortex = ω_f × r
            // Internal fluid angular velocity (approximated as fraction of bucket ω)
            Vector3 omegaFluid = state.bucketAngularVelocity * 0.5f; // Fluid lags behind bucket
            Vector3 vVortex = Vector3.Cross(omegaFluid, rHoleLocal);

            // 5. Total exit velocity from study
            return vBucket + vFlow + vRotation + vVortex;
        }

        /// <summary>
        /// Spawns particles based on flow rate.
        /// Converts continuous mass flow into discrete particles.
        /// </summary>
        private void SpawnParticles(float dt, SimulationState state, SimulationConfig config,
            ParticleManager particleManager, float flowRate, Vector3 exitVelocity, float massFlowRate)
        {
            if (!particleManager.Particles.HasFreeSlots)
                return;

            // Accumulate mass flow
            float massThisStep = massFlowRate * dt;
            _flowAccumulator += massThisStep;

            // Compute particle mass (divide flow into discrete particles)
            float particleMass = config.particleMass;
            if (particleMass <= PhysicsConstants.EPSILON)
            {
                // Auto-compute from desired particle count
                particleMass = config.paint.InitialMass / Mathf.Max(config.maxParticles * 0.1f, 1.0f);
                particleMass = Mathf.Max(particleMass, 1e-6f);
            }

            // Spawn particles as accumulator exceeds particle mass
            Vector3 orificePos = GetOrificeWorldPosition(state, config);
            int maxSpawnPerStep = 100; // Limit to prevent burst spawning
            int spawned = 0;

            while (_flowAccumulator >= particleMass && spawned < maxSpawnPerStep)
            {
                _flowAccumulator -= particleMass;

                // Add small random perturbation to exit velocity
                // This creates natural spray pattern
                Vector3 perturbation = new Vector3(
                    Random.Range(-0.1f, 0.1f),
                    Random.Range(-0.1f, 0.1f),
                    Random.Range(-0.1f, 0.1f)
                );
                Vector3 particleVelocity = exitVelocity + perturbation;

                // Slightly randomize spawn position within orifice area
                float orificeRadius = config.bucket.orificeRadius;
                Vector2 randomOffset = Random.insideUnitCircle * orificeRadius;
                Vector3 spawnPos = orificePos + new Vector3(randomOffset.x, 0.0f, randomOffset.y) * 0.5f;

                // Get current paint color
                Color paintColor = GetCurrentColor(config);

                // Compute droplet radius from mass and density
                // V = m/ρ = (4/3)πr³ → r = (3m/(4πρ))^(1/3)
                float dropletRadius = Mathf.Pow(
                    (3.0f * particleMass) / (4.0f * PhysicsConstants.PI * config.paint.density),
                    1.0f / 3.0f
                );

                int idx = particleManager.TransitionBucketToFreeFall(
                    spawnPos, particleVelocity, particleMass,
                    paintColor, dropletRadius, _currentColorIndex
                );

                if (idx < 0) break; // Pool full

                state.totalParticlesSpawned++;
                spawned++;
            }
        }

        /// <summary>
        /// Gets exit direction based on orifice position and bucket rotation.
        /// </summary>
        private Vector3 GetExitDirection(SimulationState state, SimulationConfig config)
        {
            switch (config.bucket.orificePosition)
            {
                case OrificePosition.Bottom:
                    return Vector3.down;
                case OrificePosition.Side:
                    // Side orifice: direction rotates with bucket
                    // From study: n̂(t) = R(t) · n̂₀
                    Vector3 localDir = Vector3.right; // Initial outward direction
                    return state.bucketRotation * localDir;
                default:
                    return Vector3.down;
            }
        }

        private Vector3 GetOrificeLocalPosition(SimulationConfig config)
        {
            switch (config.bucket.orificePosition)
            {
                case OrificePosition.Bottom:
                    return new Vector3(config.bucket.orificeOffsetFromCenter, -config.bucket.height * 0.5f, 0.0f);
                case OrificePosition.Side:
                    return new Vector3(config.bucket.radius, -config.bucket.height * 0.3f, 0.0f);
                default:
                    return Vector3.down * config.bucket.height * 0.5f;
            }
        }

        private Vector3 GetOrificeWorldPosition(SimulationState state, SimulationConfig config)
        {
            Vector3 localPos = GetOrificeLocalPosition(config);
            return state.bucketPosition + state.bucketRotation * localPos;
        }

        private Color GetCurrentColor(SimulationConfig config)
        {
            if (config.paint.colors == null || config.paint.colors.Length == 0)
                return Color.red;
            return config.paint.colors[_currentColorIndex % config.paint.colors.Length];
        }

        private void UpdateColorCycling(float dt, SimulationConfig config)
        {
            if (config.paint.colors == null || config.paint.colors.Length <= 1)
                return;

            // Cycle colors every 2 seconds (configurable)
            _colorSwitchTimer += dt;
            if (_colorSwitchTimer > 2.0f)
            {
                _colorSwitchTimer = 0.0f;
                _currentColorIndex = (_currentColorIndex + 1) % config.paint.colors.Length;
            }
        }
    }
}
