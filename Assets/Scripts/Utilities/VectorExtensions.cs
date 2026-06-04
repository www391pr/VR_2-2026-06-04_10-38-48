// ============================================================================
// VectorExtensions.cs — Extension methods for Vector3/Vector4
// Swinging Paint Bucket Simulation
// ============================================================================

using UnityEngine;

namespace SwingingPaintBucket.Utilities
{
    /// <summary>
    /// Extension methods for Unity vector types to support physics calculations.
    /// </summary>
    public static class VectorExtensions
    {
        /// <summary>
        /// Returns the component of this vector along a given direction.
        /// v_normal = (v · n̂) n̂
        /// </summary>
        public static Vector3 ComponentAlong(this Vector3 v, Vector3 direction)
        {
            Vector3 n = direction.normalized;
            return Vector3.Dot(v, n) * n;
        }

        /// <summary>
        /// Returns the component of this vector perpendicular to a given direction.
        /// v_tangent = v - (v · n̂) n̂
        /// </summary>
        public static Vector3 ComponentPerpendicular(this Vector3 v, Vector3 direction)
        {
            return v - v.ComponentAlong(direction);
        }

        /// <summary>
        /// Reflects the normal component with restitution coefficient.
        /// v_after = -e(v · n̂)n̂ + (v - (v · n̂)n̂)
        /// From the study: collision response equation.
        /// </summary>
        /// <param name="v">Incoming velocity</param>
        /// <param name="normal">Surface normal</param>
        /// <param name="restitution">Coefficient of restitution e (0=stick, 1=full bounce)</param>
        /// <returns>Velocity after collision</returns>
        public static Vector3 ReflectWithRestitution(this Vector3 v, Vector3 normal, float restitution)
        {
            float vn = Vector3.Dot(v, normal);
            Vector3 vNormal = vn * normal;
            Vector3 vTangent = v - vNormal;
            return -restitution * vNormal + vTangent;
        }

        /// <summary>
        /// Weighted color blend for paint mixing.
        /// C_new = (m₁C₁ + m₂C₂) / (m₁ + m₂)
        /// Applied to RGBA Color stored as Vector4.
        /// </summary>
        public static Color WeightedBlend(this Color c1, float m1, Color c2, float m2)
        {
            float totalMass = m1 + m2;
            if (totalMass < PhysicsConstants.EPSILON) return c1;
            float inv = 1.0f / totalMass;
            return new Color(
                (m1 * c1.r + m2 * c2.r) * inv,
                (m1 * c1.g + m2 * c2.g) * inv,
                (m1 * c1.b + m2 * c2.b) * inv,
                (m1 * c1.a + m2 * c2.a) * inv
            );
        }

        /// <summary>
        /// Velocity-dependent color mixing from the study.
        /// C_new = (1 - β)Cₛ + β · (mᵢCᵢ + mₛCₛ)/(mᵢ + mₛ)
        /// </summary>
        /// <param name="surfaceColor">Existing surface color Cₛ</param>
        /// <param name="incomingColor">Incoming particle color Cᵢ</param>
        /// <param name="surfaceMass">Surface paint mass mₛ</param>
        /// <param name="incomingMass">Incoming particle mass mᵢ</param>
        /// <param name="beta">Mixing factor β = 1 - e^(-k|v|)</param>
        /// <returns>Mixed color</returns>
        public static Color MixWithBeta(this Color surfaceColor, Color incomingColor,
            float surfaceMass, float incomingMass, float beta)
        {
            Color massWeighted = surfaceColor.WeightedBlend(surfaceMass, incomingColor, incomingMass);
            return new Color(
                (1.0f - beta) * surfaceColor.r + beta * massWeighted.r,
                (1.0f - beta) * surfaceColor.g + beta * massWeighted.g,
                (1.0f - beta) * surfaceColor.b + beta * massWeighted.b,
                (1.0f - beta) * surfaceColor.a + beta * massWeighted.a
            );
        }

        /// <summary>
        /// Converts a Color to Vector4 for compute shader compatibility.
        /// </summary>
        public static Vector4 ToVector4(this Color c)
        {
            return new Vector4(c.r, c.g, c.b, c.a);
        }

        /// <summary>
        /// Converts a Vector4 to Color.
        /// </summary>
        public static Color ToColor(this Vector4 v)
        {
            return new Color(v.x, v.y, v.z, v.w);
        }

        /// <summary>
        /// Returns the XZ components as a Vector2 (for canvas plane operations).
        /// </summary>
        public static Vector2 XZ(this Vector3 v)
        {
            return new Vector2(v.x, v.z);
        }

        /// <summary>
        /// Clamps all components of a Vector3.
        /// </summary>
        public static Vector3 ClampMagnitude(this Vector3 v, float maxMagnitude)
        {
            if (v.sqrMagnitude > maxMagnitude * maxMagnitude)
                return v.normalized * maxMagnitude;
            return v;
        }
    }
}
