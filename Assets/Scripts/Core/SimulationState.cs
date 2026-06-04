// ============================================================================
// SimulationState.cs — Runtime simulation state tracking
// Swinging Paint Bucket Simulation
// ============================================================================

using UnityEngine;
using System;

namespace SwingingPaintBucket.Core
{
    /// <summary>
    /// Enumeration of simulation lifecycle states.
    /// </summary>
    public enum SimulationStatus
    {
        Idle,       // Not started
        Running,    // Active simulation
        Paused,     // Temporarily halted
        Completed,  // Paint depleted or max swings reached
        Error       // Simulation error
    }

    /// <summary>
    /// Tracks all runtime state of the simulation.
    /// Updated every physics step by the SimulationManager.
    /// </summary>
    [Serializable]
    public class SimulationState
    {
        // ── Status ───────────────────────────────────────────────────────
        public SimulationStatus status = SimulationStatus.Idle;
        public float elapsedTime = 0.0f;
        public int physicsStepCount = 0;

        // ── Pendulum State ───────────────────────────────────────────────
        /// <summary>Polar angle θ (rad)</summary>
        public float theta = 0.0f;
        /// <summary>Azimuthal angle ϕ (rad)</summary>
        public float phi = 0.0f;
        /// <summary>Angular velocity θ̇ (rad/s)</summary>
        public float thetaDot = 0.0f;
        /// <summary>Angular velocity ϕ̇ (rad/s)</summary>
        public float phiDot = 0.0f;

        // ── Bucket State ─────────────────────────────────────────────────
        /// <summary>Current bucket world position</summary>
        public Vector3 bucketPosition = Vector3.zero;
        /// <summary>Current bucket velocity</summary>
        public Vector3 bucketVelocity = Vector3.zero;
        /// <summary>Current bucket acceleration</summary>
        public Vector3 bucketAcceleration = Vector3.zero;
        /// <summary>Bucket angular velocity ω</summary>
        public Vector3 bucketAngularVelocity = Vector3.zero;
        /// <summary>Bucket rotation quaternion</summary>
        public Quaternion bucketRotation = Quaternion.identity;
        /// <summary>Current total mass (bucket + paint)</summary>
        public float totalMass = 0.0f;
        /// <summary>Current moment of inertia</summary>
        public float momentOfInertia = 0.0f;
        /// <summary>Twist angle for rope torsion</summary>
        public float twistAngle = 0.0f;

        // ── Paint State ──────────────────────────────────────────────────
        /// <summary>Current paint mass remaining in bucket (kg)</summary>
        public float paintMassRemaining = 0.0f;
        /// <summary>Current paint height in bucket (m)</summary>
        public float paintHeight = 0.0f;
        /// <summary>Current effective height at orifice (m), includes sloshing</summary>
        public float effectiveHeight = 0.0f;
        /// <summary>Current flow rate Q (m³/s)</summary>
        public float flowRate = 0.0f;
        /// <summary>Exit velocity of paint from orifice</summary>
        public Vector3 exitVelocity = Vector3.zero;
        /// <summary>Paint depleted flag</summary>
        public bool paintDepleted = false;

        // ── Rope State ───────────────────────────────────────────────────
        /// <summary>Current rope tension (N)</summary>
        public float ropeTension = 0.0f;
        /// <summary>Current rope length (changes if elastic)</summary>
        public float currentRopeLength = 0.0f;

        // ── Particle Counts ──────────────────────────────────────────────
        /// <summary>Total particles created since start</summary>
        public int totalParticlesSpawned = 0;
        /// <summary>Currently active particles in bucket (SPH)</summary>
        public int activeBucketParticles = 0;
        /// <summary>Currently airborne particles</summary>
        public int activeFreeFallParticles = 0;
        /// <summary>Currently on canvas</summary>
        public int activeCanvasParticles = 0;
        /// <summary>Splash events count</summary>
        public int splashEventCount = 0;

        // ── Swing Counter ────────────────────────────────────────────────
        /// <summary>Number of completed swings (half-periods)</summary>
        public int swingCount = 0;
        /// <summary>Previous sign of θ (for swing counting)</summary>
        public float previousThetaSign = 0.0f;

        // ── Performance ──────────────────────────────────────────────────
        public float currentFPS = 0.0f;
        public float averageFPS = 0.0f;
        public float minFPS = float.MaxValue;
        public float peakMemoryMB = 0.0f;

        // ── Canvas Statistics ────────────────────────────────────────────
        /// <summary>Percentage of canvas covered by paint (0-100)</summary>
        public float canvasCoveragePercent = 0.0f;
        /// <summary>Total paint area on canvas (m²)</summary>
        public float totalPaintArea = 0.0f;

        /// <summary>
        /// Resets all state to initial values.
        /// </summary>
        public void Reset()
        {
            status = SimulationStatus.Idle;
            elapsedTime = 0.0f;
            physicsStepCount = 0;

            theta = 0.0f;
            phi = 0.0f;
            thetaDot = 0.0f;
            phiDot = 0.0f;

            bucketPosition = Vector3.zero;
            bucketVelocity = Vector3.zero;
            bucketAcceleration = Vector3.zero;
            bucketAngularVelocity = Vector3.zero;
            bucketRotation = Quaternion.identity;
            totalMass = 0.0f;
            momentOfInertia = 0.0f;
            twistAngle = 0.0f;

            paintMassRemaining = 0.0f;
            paintHeight = 0.0f;
            effectiveHeight = 0.0f;
            flowRate = 0.0f;
            exitVelocity = Vector3.zero;
            paintDepleted = false;

            ropeTension = 0.0f;
            currentRopeLength = 0.0f;

            totalParticlesSpawned = 0;
            activeBucketParticles = 0;
            activeFreeFallParticles = 0;
            activeCanvasParticles = 0;
            splashEventCount = 0;

            swingCount = 0;
            previousThetaSign = 0.0f;

            currentFPS = 0.0f;
            averageFPS = 0.0f;
            minFPS = float.MaxValue;
            peakMemoryMB = 0.0f;

            canvasCoveragePercent = 0.0f;
            totalPaintArea = 0.0f;
        }

        /// <summary>
        /// Updates FPS tracking.
        /// </summary>
        public void UpdatePerformanceMetrics(float deltaTime)
        {
            currentFPS = 1.0f / Mathf.Max(deltaTime, 0.0001f);
            if (currentFPS < minFPS && physicsStepCount > 10)
                minFPS = currentFPS;

            // Exponential moving average
            if (physicsStepCount <= 1)
                averageFPS = currentFPS;
            else
                averageFPS = averageFPS * 0.95f + currentFPS * 0.05f;

            float memMB = (float)System.GC.GetTotalMemory(false) / (1024f * 1024f);
            if (memMB > peakMemoryMB)
                peakMemoryMB = memMB;
        }

        /// <summary>
        /// Checks for swing completion (θ crosses zero).
        /// </summary>
        public void CheckSwingCompletion(float currentTheta)
        {
            float currentSign = Mathf.Sign(currentTheta);
            if (previousThetaSign != 0.0f && currentSign != previousThetaSign)
            {
                swingCount++;
            }
            previousThetaSign = currentSign;
        }

        /// <summary>
        /// Returns total active particle count.
        /// </summary>
        public int TotalActiveParticles => activeBucketParticles + activeFreeFallParticles + activeCanvasParticles;
    }
}
