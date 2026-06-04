// ============================================================================
// TimeManager.cs — Fixed timestep management for physics simulation
// Swinging Paint Bucket Simulation
// ============================================================================
// Provides a deterministic fixed timestep for physics integration,
// decoupled from Unity's variable render frame rate.
// ============================================================================

using UnityEngine;

namespace SwingingPaintBucket.Core
{
    /// <summary>
    /// Manages fixed-timestep physics updates independent of Unity's frame rate.
    /// Implements an accumulator-based approach to prevent spiral-of-death.
    /// </summary>
    public class TimeManager
    {
        // ── Configuration ────────────────────────────────────────────────
        private float _fixedDeltaTime;
        private float _accumulator;
        private float _timeScale;
        private int _maxStepsPerFrame;

        // ── State ────────────────────────────────────────────────────────
        private float _simulationTime;
        private int _stepCount;

        // ── Properties ───────────────────────────────────────────────────
        /// <summary>Fixed physics timestep Δt (seconds).</summary>
        public float FixedDeltaTime => _fixedDeltaTime;

        /// <summary>Current simulation time (seconds).</summary>
        public float SimulationTime => _simulationTime;

        /// <summary>Total number of physics steps executed.</summary>
        public int StepCount => _stepCount;

        /// <summary>Time scale multiplier (0.1x - 10x). 1 = real-time.</summary>
        public float TimeScale
        {
            get => _timeScale;
            set => _timeScale = Mathf.Clamp(value, 0.1f, 10.0f);
        }

        /// <summary>
        /// How many physics steps need to be executed this frame.
        /// Capped at maxStepsPerFrame to prevent spiral of death.
        /// </summary>
        public int StepsThisFrame { get; private set; }

        /// <summary>
        /// Interpolation alpha for rendering between physics steps.
        /// Used to smooth visual updates between fixed-rate physics.
        /// </summary>
        public float InterpolationAlpha => _accumulator / _fixedDeltaTime;

        // ── Constructor ──────────────────────────────────────────────────

        /// <summary>
        /// Creates a new TimeManager with specified fixed timestep.
        /// </summary>
        /// <param name="fixedDeltaTime">Physics timestep (seconds)</param>
        /// <param name="maxStepsPerFrame">Maximum physics steps per render frame</param>
        public TimeManager(float fixedDeltaTime, int maxStepsPerFrame = 4)
        {
            _fixedDeltaTime = Mathf.Clamp(fixedDeltaTime,
                Utilities.SimulationLimits.MIN_DELTA_TIME,
                Utilities.SimulationLimits.MAX_DELTA_TIME);
            _maxStepsPerFrame = maxStepsPerFrame;
            _timeScale = 1.0f;
            Reset();
        }

        // ── Methods ──────────────────────────────────────────────────────

        /// <summary>
        /// Accumulates frame time and determines how many physics steps to run.
        /// Call this once per Unity Update().
        /// </summary>
        /// <param name="unscaledDeltaTime">Time.unscaledDeltaTime from Unity</param>
        public void Accumulate(float unscaledDeltaTime)
        {
            // Cap incoming delta to prevent spiral of death after long pauses
            float cappedDelta = Mathf.Min(unscaledDeltaTime, Utilities.SimulationLimits.MAX_DELTA_TIME);
            float scaledDelta = cappedDelta * _timeScale;

            _accumulator += scaledDelta;

            // Calculate number of physics steps (capped)
            StepsThisFrame = 0;
            while (_accumulator >= _fixedDeltaTime && StepsThisFrame < _maxStepsPerFrame)
            {
                StepsThisFrame++;
                _accumulator -= _fixedDeltaTime;
            }

            // If we capped steps, discard excess accumulator to prevent buildup
            if (StepsThisFrame >= _maxStepsPerFrame)
            {
                _accumulator = 0.0f;
            }
        }

        /// <summary>
        /// Advances simulation time by one fixed step.
        /// Call this inside the physics step loop.
        /// </summary>
        public void AdvanceStep()
        {
            _simulationTime += _fixedDeltaTime;
            _stepCount++;
        }

        /// <summary>
        /// Resets the time manager to initial state.
        /// </summary>
        public void Reset()
        {
            _accumulator = 0.0f;
            _simulationTime = 0.0f;
            _stepCount = 0;
            StepsThisFrame = 0;
        }

        /// <summary>
        /// Updates the fixed timestep (e.g., if user changes quality settings).
        /// </summary>
        public void SetFixedDeltaTime(float dt)
        {
            _fixedDeltaTime = Mathf.Clamp(dt,
                Utilities.SimulationLimits.MIN_DELTA_TIME,
                Utilities.SimulationLimits.MAX_DELTA_TIME);
        }
    }
}
