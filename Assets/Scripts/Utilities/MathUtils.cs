// ============================================================================
// MathUtils.cs — Mathematical utility functions
// Swinging Paint Bucket Simulation
// ============================================================================

using UnityEngine;

namespace SwingingPaintBucket.Utilities
{
    /// <summary>
    /// Provides mathematical utility functions for physics calculations
    /// including SPH kernels, clamping, and interpolation.
    /// </summary>
    public static class MathUtils
    {
        // ====================================================================
        // SPH KERNEL FUNCTIONS
        // All kernels follow the study reference exactly.
        // ====================================================================

        /// <summary>
        /// Poly6 kernel for SPH density computation.
        /// W(r, h) = (315 / 64πh⁹) · (h² - |r|²)³  for |r| ≤ h
        /// </summary>
        /// <param name="r">Distance vector between particles</param>
        /// <param name="h">Smoothing radius</param>
        /// <returns>Kernel value</returns>
        public static float Poly6Kernel(Vector3 r, float h)
        {
            float rSqr = r.sqrMagnitude;
            float hSqr = h * h;
            if (rSqr >= hSqr) return 0.0f;

            float h9 = hSqr * hSqr * hSqr * hSqr * h; // h^9
            float diff = hSqr - rSqr;
            float diff3 = diff * diff * diff;

            return (PhysicsConstants.POLY6_CONSTANT_3D / h9) * diff3;
        }

        /// <summary>
        /// Poly6 kernel using squared distance (avoids sqrt).
        /// </summary>
        public static float Poly6KernelSqr(float rSqr, float h)
        {
            float hSqr = h * h;
            if (rSqr >= hSqr) return 0.0f;

            float h9 = hSqr * hSqr * hSqr * hSqr * h;
            float diff = hSqr - rSqr;
            float diff3 = diff * diff * diff;

            return (PhysicsConstants.POLY6_CONSTANT_3D / h9) * diff3;
        }

        /// <summary>
        /// Spiky kernel gradient for SPH pressure force.
        /// ∇W(r, h) = (-45 / πh⁶) · (h - |r|)² · r̂
        /// Used in: F_pressure,i = -Σⱼ mⱼ (Pᵢ + Pⱼ)/(2ρⱼ) ∇W(rᵢ - rⱼ)
        /// </summary>
        /// <param name="r">Distance vector from j to i (rᵢ - rⱼ)</param>
        /// <param name="h">Smoothing radius</param>
        /// <returns>Gradient vector</returns>
        public static Vector3 SpikyKernelGradient(Vector3 r, float h)
        {
            float rMag = r.magnitude;
            if (rMag < PhysicsConstants.EPSILON || rMag >= h) return Vector3.zero;

            float h6 = h * h * h * h * h * h; // h^6
            float diff = h - rMag;
            float coeff = (PhysicsConstants.SPIKY_GRAD_CONSTANT_3D / h6) * diff * diff / rMag;

            return coeff * r;
        }

        /// <summary>
        /// Viscosity kernel Laplacian for SPH viscosity force.
        /// ∇²W(r, h) = (45 / πh⁶) · (h - |r|)
        /// Used in: F_viscosity,i = μ Σⱼ mⱼ (vⱼ - vᵢ)/ρⱼ ∇²W
        /// </summary>
        /// <param name="r">Distance vector</param>
        /// <param name="h">Smoothing radius</param>
        /// <returns>Laplacian scalar value</returns>
        public static float ViscosityKernelLaplacian(Vector3 r, float h)
        {
            float rMag = r.magnitude;
            if (rMag >= h) return 0.0f;

            float h6 = h * h * h * h * h * h;
            return (PhysicsConstants.VISCOSITY_LAP_CONSTANT_3D / h6) * (h - rMag);
        }

        // ====================================================================
        // GENERAL MATH
        // ====================================================================

        /// <summary>
        /// Clamps a float to [min, max].
        /// </summary>
        public static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        /// <summary>
        /// Smooth step interpolation [0,1].
        /// </summary>
        public static float SmoothStep(float edge0, float edge1, float x)
        {
            float t = Clamp((x - edge0) / (edge1 - edge0), 0.0f, 1.0f);
            return t * t * (3.0f - 2.0f * t);
        }

        /// <summary>
        /// Linearly interpolates between two floats.
        /// </summary>
        public static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * Clamp(t, 0.0f, 1.0f);
        }

        /// <summary>
        /// Computes Weber number: We = ρ D v_n² / σ
        /// Used to determine splash behavior on canvas impact.
        /// </summary>
        /// <param name="density">Paint density ρ (kg/m³)</param>
        /// <param name="diameter">Droplet diameter D (m)</param>
        /// <param name="normalVelocity">Normal impact velocity v_n (m/s)</param>
        /// <param name="surfaceTension">Surface tension σ (N/m)</param>
        /// <returns>Weber number (dimensionless)</returns>
        public static float WeberNumber(float density, float diameter, float normalVelocity, float surfaceTension)
        {
            if (surfaceTension < PhysicsConstants.EPSILON) return float.MaxValue;
            return (density * diameter * normalVelocity * normalVelocity) / surfaceTension;
        }

        /// <summary>
        /// Computes mixing factor β = 1 - e^(-k|v|)
        /// From the study: determines how much color blending occurs on impact.
        /// Higher speed → more mixing.
        /// </summary>
        /// <param name="speed">Impact speed |v| (m/s)</param>
        /// <param name="k">Experimental constant k</param>
        /// <returns>Mixing factor β ∈ [0, 1]</returns>
        public static float MixingFactor(float speed, float k)
        {
            return 1.0f - Mathf.Exp(-k * speed);
        }

        /// <summary>
        /// Random unit vector in a plane defined by a normal.
        /// Used for splash secondary droplet directions d̂ᵢ.
        /// </summary>
        /// <param name="normal">Plane normal</param>
        /// <returns>Random unit vector perpendicular to normal</returns>
        public static Vector3 RandomDirectionInPlane(Vector3 normal)
        {
            // Find a vector not parallel to normal
            Vector3 arbitrary = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) < 0.99f
                ? Vector3.up
                : Vector3.right;

            Vector3 tangent1 = Vector3.Cross(normal, arbitrary).normalized;
            Vector3 tangent2 = Vector3.Cross(normal, tangent1).normalized;

            float angle = Random.Range(0.0f, PhysicsConstants.TWO_PI);
            return Mathf.Cos(angle) * tangent1 + Mathf.Sin(angle) * tangent2;
        }

        /// <summary>
        /// Compute rotation matrix from angle-axis (simplified for small rotations).
        /// For bucket rotation tracking.
        /// </summary>
        public static Quaternion AngularVelocityToQuaternion(Vector3 angularVelocity, float deltaTime)
        {
            float angle = angularVelocity.magnitude * deltaTime;
            if (angle < PhysicsConstants.EPSILON) return Quaternion.identity;
            Vector3 axis = angularVelocity.normalized;
            return Quaternion.AngleAxis(angle * PhysicsConstants.RAD_TO_DEG, axis);
        }

        /// <summary>
        /// Cross product as an explicit function (for clarity in physics equations).
        /// ω × v
        /// </summary>
        public static Vector3 Cross(Vector3 a, Vector3 b)
        {
            return Vector3.Cross(a, b);
        }

        /// <summary>
        /// Projects a vector onto a plane defined by its normal.
        /// Used for: g_tangent = g - g_normal
        /// </summary>
        public static Vector3 ProjectOntoPlane(Vector3 v, Vector3 planeNormal)
        {
            return v - Vector3.Dot(v, planeNormal) * planeNormal;
        }

        /// <summary>
        /// Computes the normal component of a vector.
        /// Used for: g_normal = (g · n̂) n̂
        /// </summary>
        public static Vector3 ProjectOntoNormal(Vector3 v, Vector3 normal)
        {
            return Vector3.Dot(v, normal) * normal;
        }

        /// <summary>
        /// Computes area of circle given radius: A = πr²
        /// Used for orifice area calculations.
        /// </summary>
        public static float CircleArea(float radius)
        {
            return PhysicsConstants.PI * radius * radius;
        }

        /// <summary>
        /// Computes orifice flow rate: Q = Cd · A · √(2gh)
        /// From the study (Torricelli's law with discharge coefficient).
        /// </summary>
        /// <param name="cd">Discharge coefficient</param>
        /// <param name="area">Orifice area (m²)</param>
        /// <param name="g">Gravity (m/s²)</param>
        /// <param name="h">Effective fluid height above orifice (m)</param>
        /// <returns>Volumetric flow rate Q (m³/s)</returns>
        public static float OrificeFlowRate(float cd, float area, float g, float h)
        {
            if (h <= 0.0f) return 0.0f;
            return cd * area * Mathf.Sqrt(2.0f * g * h);
        }

        /// <summary>
        /// Hydrostatic pressure: P = ρgh
        /// From the study.
        /// </summary>
        public static float HydrostaticPressure(float density, float g, float h)
        {
            return density * g * Mathf.Max(0.0f, h);
        }
    }
}
