// ============================================================================
// RK4Integrator.cs — 4th-order Runge-Kutta integration
// Swinging Paint Bucket Simulation
// ============================================================================
// Used for solving the nonlinear pendulum equations of motion.
// RK4 is chosen over Euler for accuracy at large angles.
// ============================================================================

namespace SwingingPaintBucket.Physics
{
    /// <summary>
    /// Generic 4th-order Runge-Kutta integrator for second-order ODEs.
    /// Converts y'' = f(t, y, y') into first-order system and integrates.
    /// 
    /// Used for pendulum equations:
    ///   θ̈ = sin(θ)cos(θ)ϕ̇² - (g/L)sin(θ) - c_θ θ̇
    ///   ϕ̈ = -(2θ̇ϕ̇)/tan(θ) - c_ϕ ϕ̇
    /// </summary>
    public static class RK4Integrator
    {
        /// <summary>
        /// Delegate for computing acceleration (second derivative).
        /// </summary>
        /// <param name="angle">Current angle</param>
        /// <param name="angularVelocity">Current angular velocity</param>
        /// <param name="otherAngle">Other coupled angle</param>
        /// <param name="otherAngularVelocity">Other coupled angular velocity</param>
        /// <returns>Angular acceleration</returns>
        public delegate float AccelerationFunc(
            float angle, float angularVelocity,
            float otherAngle, float otherAngularVelocity);

        /// <summary>
        /// Result of one integration step.
        /// </summary>
        public struct IntegrationResult
        {
            public float angle;
            public float angularVelocity;
        }

        /// <summary>
        /// Performs one RK4 integration step for a coupled second-order ODE.
        /// 
        /// Given y'' = f(y, y', other_y, other_y'), integrates:
        ///   y(t+dt) and y'(t+dt)
        /// 
        /// The system is:
        ///   dy/dt = y'
        ///   dy'/dt = f(y, y', other_y, other_y')
        /// </summary>
        /// <param name="angle">Current angle y</param>
        /// <param name="angularVelocity">Current angular velocity y'</param>
        /// <param name="otherAngle">Other coupled angle (for pendulum coupling)</param>
        /// <param name="otherAngularVelocity">Other coupled angular velocity</param>
        /// <param name="dt">Time step Δt</param>
        /// <param name="accelerationFunc">Function computing y''</param>
        /// <returns>Updated angle and angular velocity</returns>
        public static IntegrationResult Step(
            float angle, float angularVelocity,
            float otherAngle, float otherAngularVelocity,
            float dt, AccelerationFunc accelerationFunc)
        {
            // k1
            float k1_v = angularVelocity;
            float k1_a = accelerationFunc(angle, angularVelocity, otherAngle, otherAngularVelocity);

            // k2
            float halfDt = dt * 0.5f;
            float y2 = angle + k1_v * halfDt;
            float v2 = angularVelocity + k1_a * halfDt;
            float k2_v = v2;
            float k2_a = accelerationFunc(y2, v2, otherAngle, otherAngularVelocity);

            // k3
            float y3 = angle + k2_v * halfDt;
            float v3 = angularVelocity + k2_a * halfDt;
            float k3_v = v3;
            float k3_a = accelerationFunc(y3, v3, otherAngle, otherAngularVelocity);

            // k4
            float y4 = angle + k3_v * dt;
            float v4 = angularVelocity + k3_a * dt;
            float k4_v = v4;
            float k4_a = accelerationFunc(y4, v4, otherAngle, otherAngularVelocity);

            // Combine
            float dtOver6 = dt / 6.0f;
            IntegrationResult result;
            result.angle = angle + dtOver6 * (k1_v + 2.0f * k2_v + 2.0f * k3_v + k4_v);
            result.angularVelocity = angularVelocity + dtOver6 * (k1_a + 2.0f * k2_a + 2.0f * k3_a + k4_a);

            return result;
        }

        /// <summary>
        /// Performs fully coupled RK4 for the two-angle pendulum system.
        /// Both θ and ϕ are integrated simultaneously so coupling is handled correctly.
        /// 
        /// System:
        ///   θ̈ = fθ(θ, θ̇, ϕ, ϕ̇)
        ///   ϕ̈ = fϕ(θ, θ̇, ϕ, ϕ̇)
        /// </summary>
        public static void StepCoupled(
            ref float theta, ref float thetaDot,
            ref float phi, ref float phiDot,
            float dt,
            AccelerationFunc thetaAccelFunc,
            AccelerationFunc phiAccelFunc)
        {
            // State vector: [θ, θ̇, ϕ, ϕ̇]
            float halfDt = dt * 0.5f;

            // ── k1 ──────────────────────────────────────────────────────
            float k1_thetaV = thetaDot;
            float k1_thetaA = thetaAccelFunc(theta, thetaDot, phi, phiDot);
            float k1_phiV = phiDot;
            float k1_phiA = phiAccelFunc(theta, thetaDot, phi, phiDot);

            // ── k2 ──────────────────────────────────────────────────────
            float t2_theta = theta + k1_thetaV * halfDt;
            float t2_thetaDot = thetaDot + k1_thetaA * halfDt;
            float t2_phi = phi + k1_phiV * halfDt;
            float t2_phiDot = phiDot + k1_phiA * halfDt;

            float k2_thetaV = t2_thetaDot;
            float k2_thetaA = thetaAccelFunc(t2_theta, t2_thetaDot, t2_phi, t2_phiDot);
            float k2_phiV = t2_phiDot;
            float k2_phiA = phiAccelFunc(t2_theta, t2_thetaDot, t2_phi, t2_phiDot);

            // ── k3 ──────────────────────────────────────────────────────
            float t3_theta = theta + k2_thetaV * halfDt;
            float t3_thetaDot = thetaDot + k2_thetaA * halfDt;
            float t3_phi = phi + k2_phiV * halfDt;
            float t3_phiDot = phiDot + k2_phiA * halfDt;

            float k3_thetaV = t3_thetaDot;
            float k3_thetaA = thetaAccelFunc(t3_theta, t3_thetaDot, t3_phi, t3_phiDot);
            float k3_phiV = t3_phiDot;
            float k3_phiA = phiAccelFunc(t3_theta, t3_thetaDot, t3_phi, t3_phiDot);

            // ── k4 ──────────────────────────────────────────────────────
            float t4_theta = theta + k3_thetaV * dt;
            float t4_thetaDot = thetaDot + k3_thetaA * dt;
            float t4_phi = phi + k3_phiV * dt;
            float t4_phiDot = phiDot + k3_phiA * dt;

            float k4_thetaV = t4_thetaDot;
            float k4_thetaA = thetaAccelFunc(t4_theta, t4_thetaDot, t4_phi, t4_phiDot);
            float k4_phiV = t4_phiDot;
            float k4_phiA = phiAccelFunc(t4_theta, t4_thetaDot, t4_phi, t4_phiDot);

            // ── Combine ─────────────────────────────────────────────────
            float dtOver6 = dt / 6.0f;

            theta += dtOver6 * (k1_thetaV + 2.0f * k2_thetaV + 2.0f * k3_thetaV + k4_thetaV);
            thetaDot += dtOver6 * (k1_thetaA + 2.0f * k2_thetaA + 2.0f * k3_thetaA + k4_thetaA);
            phi += dtOver6 * (k1_phiV + 2.0f * k2_phiV + 2.0f * k3_phiV + k4_phiV);
            phiDot += dtOver6 * (k1_phiA + 2.0f * k2_phiA + 2.0f * k3_phiA + k4_phiA);
        }
    }
}
