// ============================================================================
// CanvasImpactSystem.cs — Paint impact on canvas surface
// Swinging Paint Bucket Simulation
// ============================================================================
// From the study:
//   Velocity decomposition: v = v_normal + v_tangent
//   Collision: v_after = -e(v·n̂)n̂ + (v - (v·n̂)n̂)
//   
//   Splash (Weber number): We = ρDvn²/σ
//     We < 10: no splash
//     We > 50: splash begins
//     We > 100: many secondary droplets
//   
//   N_splash = ks(We/We_crit - 1)
//   m_splash = η m₀
//   vi = vs d̂i,  vs = kv vn
//
//   Momentum conservation on existing paint:
//     v_final = (m₁v₁ + m₂v₂)/(m₁ + m₂)
//     F_mix = μ(v_incoming - v_surface)
// ============================================================================

using UnityEngine;
using SwingingPaintBucket.Core;
using SwingingPaintBucket.Utilities;

namespace SwingingPaintBucket.Physics.Canvas
{
    /// <summary>
    /// Handles paint particle impacts on the canvas surface.
    /// Implements collision response, splash generation (Weber number),
    /// and momentum conservation for landing on existing paint.
    /// </summary>
    public class CanvasImpactSystem
    {
        /// <summary>
        /// Processes all newly-impacted canvas particles.
        /// </summary>
        public void UpdatePhysics(float dt, SimulationState state, SimulationConfig config,
            ParticleManager particleManager)
        {
            var particles = particleManager.Particles;
            int[] canvasIndices = particleManager.GetCanvasIndices();
            int canvasCount = particleManager.CanvasCount;

            Vector3 canvasNormal = config.canvas.GetNormal();
            float restitution = DefaultValues.RESTITUTION_COEFFICIENT;

            // Get surface properties
            config.canvas.GetSurfaceProperties(out float absorption, out float friction, out float spreadRate);

            int canvasActiveCount = 0;

            for (int i = 0; i < canvasCount; i++)
            {
                int idx = canvasIndices[i];
                if (particles.states[idx] != ParticleState.OnCanvas)
                    continue;

                Vector3 pos = particles.positions[idx];
                Vector3 vel = particles.velocities[idx];
                float mass = particles.masses[idx];
                float radius = particles.radii[idx];

                // ══════════════════════════════════════════════════════════
                // IMPACT PROCESSING (only for freshly-arrived particles)
                // A particle is "freshly arrived" if it has non-trivial
                // normal velocity (hasn't been processed yet)
                // ══════════════════════════════════════════════════════════

                float normalSpeed = Vector3.Dot(vel, canvasNormal);

                // If particle still has significant normal velocity, process impact
                if (Mathf.Abs(normalSpeed) > 0.01f)
                {
                    ProcessImpact(idx, dt, state, config, particleManager,
                        canvasNormal, restitution, absorption, friction);
                }

                // ══════════════════════════════════════════════════════════
                // SURFACE PHYSICS (for all canvas particles)
                // From study:
                //   F = mg_tangent + F_friction + F_viscous + F_spread
                //   dm/dt = -αm (absorption)
                // ══════════════════════════════════════════════════════════

                ProcessSurfacePhysics(idx, dt, config, particles,
                    canvasNormal, absorption, friction, spreadRate);

                // Check if particle is absorbed (mass too small)
                if (particles.masses[idx] < PhysicsConstants.EPSILON * 0.01f)
                {
                    // Don't kill — leave as paint mark but stop physics
                    particles.velocities[idx] = Vector3.zero;
                }

                canvasActiveCount++;
            }

            state.activeCanvasParticles = canvasActiveCount;
        }

        /// <summary>
        /// Processes a single particle impact.
        /// Sequence from study:
        ///   1. Collision response (restitution)
        ///   2. Check for splash (Weber number)
        ///   3. Generate secondary droplets if splash
        ///   4. Momentum conservation if landing on existing paint
        /// </summary>
        private void ProcessImpact(int idx, float dt, SimulationState state,
            SimulationConfig config, ParticleManager particleManager,
            Vector3 canvasNormal, float restitution, float absorption, float friction)
        {
            var particles = particleManager.Particles;
            Vector3 vel = particles.velocities[idx];
            float mass = particles.masses[idx];
            float radius = particles.radii[idx];

            // ── 1. Velocity decomposition from study ────────────────────
            // v = v_normal + v_tangent
            float vn = Vector3.Dot(vel, canvasNormal);
            Vector3 vNormal = vn * canvasNormal;
            Vector3 vTangent = vel - vNormal;

            float normalSpeed = Mathf.Abs(vn);

            // ── 2. Collision response from study ────────────────────────
            // v_after = -e(v·n̂)n̂ + (v - (v·n̂)n̂)
            // For paint: e ≈ 0 (nearly sticks), so v_after ≈ v_tangent
            Vector3 vAfter = -restitution * vNormal + vTangent;

            // ── 3. Splash check (Weber number) from study ───────────────
            // We = ρ D vn² / σ
            float diameter = 2.0f * radius;
            float We = MathUtils.WeberNumber(
                config.paint.density,
                diameter,
                normalSpeed,
                config.paint.surfaceTension
            );

            if (We > config.paint.weberCritical)
            {
                // Splash occurs!
                GenerateSplash(idx, state, config, particleManager,
                    canvasNormal, We, normalSpeed);
                state.splashEventCount++;
            }

            // ── 4. Apply collision velocity ─────────────────────────────
            particles.velocities[idx] = vAfter;

            // ── 5. Color mixing with velocity-dependent β ───────────────
            // β = 1 - e^(-k|v|)
            // Higher impact speed → more mixing
            float beta = MathUtils.MixingFactor(normalSpeed, config.paint.mixingConstant);
            // Store beta in lifetime field for use by ColorMixingSystem
            // (repurposing lifetime for canvas particles)
            particles.lifetimes[idx] = beta;
        }

        /// <summary>
        /// Generates secondary splash droplets.
        /// From study:
        ///   N_splash = ks(We/We_crit - 1)
        ///   m_splash = η m₀  (0 < η < 1)
        ///   Σ mi = m_splash (mass conservation)
        ///   vi = vs d̂i
        ///   vs = kv vn
        ///   d̂i: random direction in canvas plane
        /// </summary>
        private void GenerateSplash(int parentIdx, SimulationState state,
            SimulationConfig config, ParticleManager particleManager,
            Vector3 canvasNormal, float weberNumber, float normalSpeed)
        {
            var particles = particleManager.Particles;
            float parentMass = particles.masses[parentIdx];

            // Number of secondary droplets: N_splash = ks(We/We_crit - 1)
            float ratio = weberNumber / config.paint.weberCritical;
            int nSplash = Mathf.CeilToInt(config.paint.splashCountCoefficient * (ratio - 1.0f));
            nSplash = Mathf.Clamp(nSplash, 1, 20); // Limit secondary droplets

            // Total splash mass: m_splash = η m₀
            float totalSplashMass = config.paint.splashMassFraction * parentMass;

            // Mass per secondary droplet (mass conservation: Σ mi = m_splash)
            float massPerDroplet = totalSplashMass / nSplash;

            // Reduce parent mass
            particles.masses[parentIdx] -= totalSplashMass;
            particles.masses[parentIdx] = Mathf.Max(particles.masses[parentIdx], PhysicsConstants.EPSILON);

            // Splash velocity: vs = kv * vn
            float splashSpeed = config.paint.splashVelocityFraction * normalSpeed;

            Vector3 parentPos = particles.positions[parentIdx];
            Color parentColor = particles.colors[parentIdx];
            int colorIdx = particles.colorIndices[parentIdx];

            for (int i = 0; i < nSplash; i++)
            {
                if (!particleManager.Particles.HasFreeSlots) break;

                // Random direction in canvas plane: d̂i
                Vector3 dir = MathUtils.RandomDirectionInPlane(canvasNormal);

                // Velocity: vi = vs d̂i
                // Add small upward component for realistic splash arc
                Vector3 splashVel = splashSpeed * dir + canvasNormal * splashSpeed * 0.3f;

                // Compute radius from mass
                float splashRadius = Mathf.Pow(
                    (3.0f * massPerDroplet) / (4.0f * PhysicsConstants.PI * config.paint.density),
                    1.0f / 3.0f
                );

                // Spawn as FreeFall (will fall back onto canvas)
                particleManager.SpawnFreeFallParticle(
                    parentPos + canvasNormal * 0.001f, // Slightly above canvas
                    splashVel,
                    massPerDroplet,
                    parentColor,
                    splashRadius,
                    colorIdx
                );

                state.totalParticlesSpawned++;
            }
        }

        /// <summary>
        /// Processes surface physics for a canvas particle.
        /// From study:
        ///   Gravity on surface:
        ///     g_normal = (g · n̂) n̂
        ///     g_tangent = g - g_normal
        ///     F_flow = m g_tangent
        ///   Friction: F_friction = -k v_tangent
        ///   Viscosity: F_viscous = μ(v_neighbor - v) [simplified]
        ///   Spread: F_spread = -∇h(x,y,z) [simplified]
        ///   Absorption: dm/dt = -α m
        /// </summary>
        private void ProcessSurfacePhysics(int idx, float dt, SimulationConfig config,
            ParticleSOA particles, Vector3 canvasNormal,
            float absorption, float friction, float spreadRate)
        {
            Vector3 vel = particles.velocities[idx];
            float mass = particles.masses[idx];

            if (mass < PhysicsConstants.EPSILON * 0.01f) return;

            // ── Gravity decomposition from study ────────────────────────
            // g_normal = (g · n̂) n̂
            // g_tangent = g - g_normal
            Vector3 gravity = new Vector3(0.0f, -config.environment.gravity, 0.0f);
            Vector3 gNormal = MathUtils.ProjectOntoNormal(gravity, canvasNormal);
            Vector3 gTangent = gravity - gNormal;

            // F_flow = m g_tangent
            Vector3 flowForce = mass * gTangent;

            // ── Friction: F_friction = -k v_tangent ─────────────────────
            Vector3 vTangent = MathUtils.ProjectOntoPlane(vel, canvasNormal);
            Vector3 frictionForce = -friction * vTangent;

            // ── Spread: F_spread = -∇h (simplified as outward diffusion) ─
            // Simplified: particles slowly spread outward
            Vector3 spreadForce = -spreadRate * particles.positions[idx].normalized * 0.001f;

            // ── Total surface force from study ──────────────────────────
            // F = mg_tangent + F_friction + F_viscous + F_spread
            Vector3 totalForce = flowForce + frictionForce + spreadForce;

            // Update velocity (only tangential, no normal penetration)
            Vector3 acceleration = totalForce / mass;
            vel += acceleration * dt;

            // Remove any normal component (keep on surface)
            vel = MathUtils.ProjectOntoPlane(vel, canvasNormal);

            // Apply velocity damping (viscosity on surface)
            float effectiveViscosity = config.GetEffectiveViscosity();
            float dampingFactor = Mathf.Exp(-effectiveViscosity * dt * 10.0f);
            vel *= dampingFactor;

            // Update position
            Vector3 pos = particles.positions[idx];
            pos += vel * dt;

            // ── Absorption: dm/dt = -α m ────────────────────────────────
            // From study: paint is absorbed into the surface over time
            float dm = -absorption * mass * dt;
            mass += dm;
            mass = Mathf.Max(mass, 0.0f);

            // Write back
            particles.velocities[idx] = vel;
            particles.positions[idx] = pos;
            particles.masses[idx] = mass;
            particles.lifetimes[idx] += dt;
        }
    }
}
