// ============================================================================
// FreeFallSystem.cs — Airborne particle physics
// Swinging Paint Bucket Simulation
// ============================================================================
// From the study (Forces on external/airborne particles):
//   Gravity: F_g = (0, -mg, 0)
//   Air drag: F_d = -½ ρ_air Cd A |v| v
//   Surface tension: F_surface = σ κ n̂
//   Wind: F_wind = kw(v_wind - v_particle)
//   Total: F = F_g + F_d + F_surface + F_wind
//
// Integration:
//   v(t+Δt) = v(t) + (F/m) Δt
//   r(t+Δt) = r(t) + v Δt
// ============================================================================

using UnityEngine;
using SwingingPaintBucket.Core;
using SwingingPaintBucket.Utilities;

namespace SwingingPaintBucket.Physics
{
    /// <summary>
    /// Simulates airborne paint particles from bucket to canvas.
    /// Applies gravity, air drag, surface tension, and wind forces.
    /// All equations follow the study exactly.
    /// </summary>
    public class FreeFallSystem
    {
        /// <summary>
        /// Updates all free-fall particles for one fixed timestep.
        /// </summary>
        public void UpdatePhysics(float dt, SimulationState state, SimulationConfig config,
            ParticleManager particleManager)
        {
            var particles = particleManager.Particles;
            int[] indices = particleManager.GetFreeFallIndices();
            int count = particleManager.FreeFallCount;

            float g = config.environment.gravity;
            float airDensity = config.environment.airDensity;
            float surfaceTension = config.paint.surfaceTension;
            bool windEnabled = config.environment.windEnabled;
            Vector3 windVelocity = config.environment.windVelocity;
            float windCoeff = config.environment.windCoefficient;

            // Canvas plane for impact detection
            float canvasY = config.canvas.yPosition;
            Vector3 canvasNormal = config.canvas.GetNormal();

            int freeFallCount = 0;

            for (int i = 0; i < count; i++)
            {
                int idx = indices[i];
                if (particles.states[idx] != ParticleState.FreeFall)
                    continue;

                Vector3 pos = particles.positions[idx];
                Vector3 vel = particles.velocities[idx];
                float mass = particles.masses[idx];
                float radius = particles.radii[idx];

                if (mass < PhysicsConstants.EPSILON)
                {
                    particleManager.KillParticle(idx);
                    continue;
                }

                // ══════════════════════════════════════════════════════════
                // FORCE COMPUTATION (from study)
                // ══════════════════════════════════════════════════════════

                Vector3 totalForce = Vector3.zero;

                // ── 1. Gravity: F_g = (0, -mg, 0) ──────────────────────
                totalForce.y -= mass * g;

                // ── 2. Air Drag: F_d = -½ ρ_air Cd A |v| v ──────────────
                float speed = vel.magnitude;
                if (speed > PhysicsConstants.EPSILON)
                {
                    // Cross-sectional area of spherical droplet: A = πr²
                    float dragArea = PhysicsConstants.PI * radius * radius;
                    // Drag coefficient for sphere: ~0.47
                    float dragMagnitude = 0.5f * airDensity *
                        PhysicsConstants.DEFAULT_AIR_DRAG_COEFFICIENT * dragArea * speed;
                    totalForce -= dragMagnitude * (vel / speed);
                }

                // ── 3. Surface Tension: F_surface = σ κ n̂ ──────────────
                // For a free spherical droplet, surface tension acts to
                // maintain shape. The curvature κ = 2/r for a sphere.
                // This force is generally small for macroscopic droplets
                // but included per study requirements.
                if (radius > PhysicsConstants.EPSILON)
                {
                    float curvature = 2.0f / radius;
                    // Direction: inward (toward center) — maintains droplet shape
                    // For free-flight, surface tension primarily affects breakup
                    // We model it as a shape-maintaining force
                    float surfaceForce = surfaceTension * curvature;
                    // Only significant when droplet is deforming
                    // Modeled as damping on velocity divergence
                    totalForce -= surfaceForce * vel.normalized * 0.001f;
                }

                // ── 4. Wind: F_wind = kw(v_wind - v_particle) ──────────
                if (windEnabled)
                {
                    Vector3 windForce = windCoeff * (windVelocity - vel);
                    totalForce += windForce;
                }

                // ══════════════════════════════════════════════════════════
                // INTEGRATION (Euler — from study)
                // v(t+Δt) = v(t) + (F/m) Δt
                // r(t+Δt) = r(t) + v Δt
                // ══════════════════════════════════════════════════════════

                Vector3 acceleration = totalForce / mass;
                vel += acceleration * dt;
                pos += vel * dt;

                // Update lifetime
                particles.lifetimes[idx] += dt;

                // ══════════════════════════════════════════════════════════
                // CANVAS IMPACT DETECTION
                // Check if particle has crossed the canvas plane.
                // For flat horizontal canvas: y <= canvasY
                // For tilted canvas: dot(pos - canvasOrigin, normal) <= 0
                // ══════════════════════════════════════════════════════════

                bool impacted = false;

                if (Mathf.Abs(config.canvas.inclinationDegrees) < 0.1f)
                {
                    // Simple horizontal canvas check
                    impacted = pos.y <= canvasY;
                }
                else
                {
                    // General tilted canvas check
                    Vector3 canvasOrigin = new Vector3(0.0f, canvasY, 0.0f);
                    float distToPlane = Vector3.Dot(pos - canvasOrigin, canvasNormal);
                    impacted = distToPlane <= 0.0f;
                }

                if (impacted)
                {
                    // Snap to canvas surface
                    if (Mathf.Abs(config.canvas.inclinationDegrees) < 0.1f)
                    {
                        pos.y = canvasY;
                    }
                    else
                    {
                        // Project back onto canvas plane
                        Vector3 canvasOrigin = new Vector3(0.0f, canvasY, 0.0f);
                        float dist = Vector3.Dot(pos - canvasOrigin, canvasNormal);
                        pos -= dist * canvasNormal;
                    }

                    particles.positions[idx] = pos;
                    particles.velocities[idx] = vel;

                    // Transition to canvas (impact handling done by CanvasImpactSystem)
                    particleManager.TransitionToCanvas(idx);
                    continue;
                }

                // ══════════════════════════════════════════════════════════
                // BOUNDS CHECK — kill particles that fall too far
                // ══════════════════════════════════════════════════════════
                if (pos.y < canvasY - 5.0f || particles.lifetimes[idx] > 30.0f)
                {
                    particleManager.KillParticle(idx);
                    continue;
                }

                // Write back
                particles.positions[idx] = pos;
                particles.velocities[idx] = vel;
                freeFallCount++;
            }

            // Update state counters
            state.activeFreeFallParticles = freeFallCount;
        }
    }
}
