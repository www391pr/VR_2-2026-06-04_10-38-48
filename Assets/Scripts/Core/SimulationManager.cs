// ============================================================================
// SimulationManager.cs — Main orchestrator MonoBehaviour
// Swinging Paint Bucket Simulation
// ============================================================================
// This is the central MonoBehaviour that coordinates all physics subsystems,
// manages the simulation lifecycle, and drives the update cycle.
// ============================================================================

using UnityEngine;
using System;
using SwingingPaintBucket.Physics;
using SwingingPaintBucket.Physics.Fluid;
using SwingingPaintBucket.Physics.Canvas;
using SwingingPaintBucket.Physics.Environment;
using SwingingPaintBucket.Rendering;
using SwingingPaintBucket.Data;

namespace SwingingPaintBucket.Core
{
    /// <summary>
    /// Central simulation orchestrator. Manages the complete update cycle:
    /// 1. Pendulum → 2. Rope → 3. Bucket → 4. SPH Fluid → 5. Orifice Flow →
    /// 6. Free-Fall → 7. Canvas Impact → 8. Color Mixing → 9. Render → 10. Record
    /// </summary>
    public class SimulationManager : MonoBehaviour
    {
        // ── Inspector References ─────────────────────────────────────────
        [Header("Configuration")]
        [SerializeField] private SimulationConfig _config;

        [Header("Compute Shaders")]
        [SerializeField] private ComputeShader _sphComputeShader;
        [SerializeField] private ComputeShader _spatialHashComputeShader;
        [SerializeField] private ComputeShader _particleUpdateComputeShader;
        [SerializeField] private ComputeShader _canvasBlendComputeShader;

        [Header("Rendering")]
        [SerializeField] private ParticleRenderer _particleRenderer;
        [SerializeField] private BucketRenderer _bucketRenderer;
        [SerializeField] private RopeRenderer _ropeRenderer;
        [SerializeField] private CanvasPaintRenderer _canvasPaintRenderer;

        // ── Runtime State ────────────────────────────────────────────────
        private SimulationState _state;
        private TimeManager _timeManager;

        // ── Physics Systems ──────────────────────────────────────────────
        private PendulumSystem _pendulumSystem;
        private RopeSystem _ropeSystem;
        private BucketSystem _bucketSystem;
        private OrificeFlowCalculator _orificeFlowCalculator;
        private FreeFallSystem _freeFallSystem;
        private CanvasImpactSystem _canvasImpactSystem;
        private ColorMixingSystem _colorMixingSystem;
        private EnvironmentSystem _environmentSystem;
        private SPHFluidSystem _sphFluidSystem;
        private ParticleManager _particleManager;

        // ── Data ─────────────────────────────────────────────────────────
        private DataRecorder _dataRecorder;

        // ── Events ───────────────────────────────────────────────────────
        /// <summary>Fired when simulation state changes.</summary>
        public event Action<SimulationStatus> OnStatusChanged;
        /// <summary>Fired every physics step with current state.</summary>
        public event Action<SimulationState> OnPhysicsStep;
        /// <summary>Fired when simulation completes.</summary>
        public event Action<SimulationState> OnSimulationComplete;

        // ── Properties ───────────────────────────────────────────────────
        public SimulationConfig Config => _config;
        public SimulationState State => _state;
        public TimeManager Time => _timeManager;
        public DataRecorder DataRecorder => _dataRecorder;
        public ParticleManager ParticleManager => _particleManager;

        // ====================================================================
        // UNITY LIFECYCLE
        // ====================================================================

        private void Awake()
        {
            _state = new SimulationState();
            ValidateReferences();
        }

        private void OnDestroy()
        {
            CleanupSystems();
        }

        private void Update()
        {
            if (_state.status != SimulationStatus.Running)
                return;

            // Update performance metrics
            _state.UpdatePerformanceMetrics(UnityEngine.Time.unscaledDeltaTime);

            // Accumulate time and determine physics steps
            _timeManager.Accumulate(UnityEngine.Time.unscaledDeltaTime);

            // Execute fixed-timestep physics
            for (int i = 0; i < _timeManager.StepsThisFrame; i++)
            {
                PhysicsStep(_timeManager.FixedDeltaTime);
                _timeManager.AdvanceStep();
            }

            // Update renderers (at render frame rate)
            UpdateRenderers();

            // Check completion conditions
            CheckCompletionConditions();
        }

        // ====================================================================
        // PUBLIC API
        // ====================================================================

        /// <summary>
        /// Initializes all systems with the current configuration and starts simulation.
        /// </summary>
        public void StartSimulation()
        {
            if (_config == null)
            {
                Debug.LogError("[SimulationManager] No SimulationConfig assigned!");
                return;
            }

            // Reset state
            _state.Reset();

            // Initialize time manager
            _timeManager = new TimeManager(
                _config.fixedTimeStep,
                Utilities.SimulationLimits.MAX_PHYSICS_STEPS_PER_FRAME
            );

            // Initialize all physics systems
            InitializeSystems();

            // Start recording
            _dataRecorder = new DataRecorder();
            _dataRecorder.Initialize(_config);

            // Set initial state
            SetInitialState();

            // Begin
            SetStatus(SimulationStatus.Running);
            Debug.Log("[SimulationManager] Simulation started.");
        }

        /// <summary>
        /// Pauses the simulation.
        /// </summary>
        public void PauseSimulation()
        {
            if (_state.status == SimulationStatus.Running)
            {
                SetStatus(SimulationStatus.Paused);
                Debug.Log("[SimulationManager] Simulation paused.");
            }
        }

        /// <summary>
        /// Resumes a paused simulation.
        /// </summary>
        public void ResumeSimulation()
        {
            if (_state.status == SimulationStatus.Paused)
            {
                SetStatus(SimulationStatus.Running);
                Debug.Log("[SimulationManager] Simulation resumed.");
            }
        }

        /// <summary>
        /// Stops and resets the simulation.
        /// </summary>
        public void StopSimulation()
        {
            SetStatus(SimulationStatus.Idle);
            CleanupSystems();
            _state.Reset();
            Debug.Log("[SimulationManager] Simulation stopped.");
        }

        /// <summary>
        /// Resets and restarts with current configuration.
        /// </summary>
        public void RestartSimulation()
        {
            StopSimulation();
            StartSimulation();
        }

        /// <summary>
        /// Sets the simulation time scale (0.1x to 10x).
        /// </summary>
        public void SetTimeScale(float scale)
        {
            if (_timeManager != null)
                _timeManager.TimeScale = scale;
        }

        /// <summary>
        /// Applies a new configuration. Must restart simulation for changes to take effect.
        /// </summary>
        public void ApplyConfig(SimulationConfig newConfig)
        {
            _config = newConfig;
        }

        // ====================================================================
        // PHYSICS UPDATE CYCLE
        // From the study: ordered update of all subsystems.
        // ====================================================================

        /// <summary>
        /// Executes one fixed-timestep physics step.
        /// Update order follows the study's simulation pipeline.
        /// </summary>
        private void PhysicsStep(float dt)
        {
            // 0. Rebuild particle state indices for batch processing across all systems
            _particleManager.RebuildStateIndices();

            // 1. Update pendulum angles (θ, ϕ) using RK4
            //    θ̈ = sin(θ)cos(θ)ϕ̇² - (g/L)sin(θ) - c_θ θ̇
            //    ϕ̈ = -(2θ̇ϕ̇)/tan(θ) - c_ϕ ϕ̇
            _pendulumSystem.UpdatePhysics(dt, _state, _config);

            // 2. Update rope (tension, optional elasticity, torsion)
            //    F_rope = -ks(|r - r₀| - L)r̂ - kd v  (if elastic)
            //    τ = -kt θt  (torsion)
            _ropeSystem.UpdatePhysics(dt, _state, _config);

            // 3. Update bucket dynamics (position, velocity, mass, rotation)
            //    ma = Fg + FT + Fd + Fjet + Fwind
            //    I(dω/dt) = Στ
            _bucketSystem.UpdatePhysics(dt, _state, _config);

            // 3.5 Update SPH fluid dynamics in bucket
            //    ρᵢ = Σⱼ mⱼ W(rᵢ - rⱼ), Pᵢ = k(ρᵢ - ρ₀), Fᵢ = ...
            _sphFluidSystem?.UpdatePhysics(dt, _state, _config, _particleManager);

            // 4. Update orifice flow (flow rate, exit velocity, spawn particles)
            //    Q = Cd A √(2g h_eff)
            //    v_out = v_bucket + √(2gh)n̂ + ω × r_hole + v_vortex
            _orificeFlowCalculator.UpdatePhysics(dt, _state, _config, _particleManager);

            // 5. Update free-fall particles (gravity + drag + wind + surface tension)
            //    F = Fg + Fd + Fsurface + Fwind
            _freeFallSystem.UpdatePhysics(dt, _state, _config, _particleManager);

            // 6. Detect canvas impacts and handle splash/spread
            //    Weber number: We = ρDvn²/σ
            //    v_after = -e(v·n̂)n̂ + (v - (v·n̂)n̂)
            _canvasImpactSystem.UpdatePhysics(dt, _state, _config, _particleManager);

            // 7. Update canvas particles (friction, spread, absorption, color mixing)
            //    F = mg_tangent + F_friction + F_viscous + F_spread
            //    dm/dt = -αm  (absorption)
            _colorMixingSystem.UpdatePhysics(dt, _state, _config, _particleManager);

            // 8. Update environment (wind changes, etc.)
            _environmentSystem.UpdatePhysics(dt, _state, _config);

            // 9. Update swing counter
            _state.CheckSwingCompletion(_state.theta);

            // 10. Record data
            _state.elapsedTime = _timeManager.SimulationTime;
            _state.physicsStepCount++;
            _dataRecorder?.RecordStep(_state);

            // Fire event
            OnPhysicsStep?.Invoke(_state);
        }

        // ====================================================================
        // INITIALIZATION
        // ====================================================================

        private void InitializeSystems()
        {
            _pendulumSystem = new PendulumSystem();
            _ropeSystem = new RopeSystem();
            _bucketSystem = new BucketSystem();
            _orificeFlowCalculator = new OrificeFlowCalculator();
            _freeFallSystem = new FreeFallSystem();
            _canvasImpactSystem = new CanvasImpactSystem();
            _colorMixingSystem = new ColorMixingSystem();
            _environmentSystem = new EnvironmentSystem();

            // Initialize particle manager with capacity
            _particleManager = new ParticleManager(_config.maxParticles);
            _particleManager.InitializeBucketParticles(_config);

            // Initialize SPH System
            _sphFluidSystem = new SPHFluidSystem(_sphComputeShader, _spatialHashComputeShader, Mathf.Min(_config.maxParticles, 100000));

            // Initialize rendering
            if (_canvasPaintRenderer != null)
                _canvasPaintRenderer.Initialize(_config.canvas, _canvasBlendComputeShader);

            if (_particleRenderer != null)
                _particleRenderer.Initialize(_config.maxParticles);
        }

        private void SetInitialState()
        {
            // Set initial pendulum angles from config
            _state.theta = _config.motion.InitialThetaRad;
            _state.phi = _config.motion.InitialPhiRad;

            // Set initial angular velocities
            // If initial velocity is specified, convert to angular velocity
            if (_config.motion.initialVelocity > Utilities.PhysicsConstants.EPSILON)
            {
                float dirRad = _config.motion.initialVelocityDirectionDegrees * Utilities.PhysicsConstants.DEG_TO_RAD;
                float v = _config.motion.initialVelocity;
                _state.thetaDot = v * Mathf.Cos(dirRad) / _config.rope.length;
                _state.phiDot = v * Mathf.Sin(dirRad) / (_config.rope.length * Mathf.Sin(Mathf.Max(_state.theta, 0.01f)));
            }

            // Set initial paint mass
            _state.paintMassRemaining = _config.paint.InitialMass;
            _state.paintHeight = _config.paint.GetInitialHeight(_config.bucket.radius);
            _state.effectiveHeight = _state.paintHeight;

            // Set initial total mass
            _state.totalMass = _config.bucket.mass + _state.paintMassRemaining;

            // Set initial rope length
            _state.currentRopeLength = _config.rope.length;

            // Compute initial bucket position from angles
            // x = x₀ + L sin(θ) cos(ϕ)
            // y = y₀ - L cos(θ)
            // z = z₀ + L sin(θ) sin(ϕ)
            Vector3 suspension = _config.suspension.position;
            float L = _config.rope.length;
            float theta = _state.theta;
            float phi = _state.phi;

            _state.bucketPosition = new Vector3(
                suspension.x + L * Mathf.Sin(theta) * Mathf.Cos(phi),
                suspension.y - L * Mathf.Cos(theta),
                suspension.z + L * Mathf.Sin(theta) * Mathf.Sin(phi)
            );

            // Compute initial moment of inertia: I = ½ m_total R²
            _state.momentOfInertia = 0.5f * _state.totalMass * _config.bucket.radius * _config.bucket.radius;

            Debug.Log($"[SimulationManager] Initial state: θ={_state.theta * Utilities.PhysicsConstants.RAD_TO_DEG:F1}°, " +
                      $"ϕ={_state.phi * Utilities.PhysicsConstants.RAD_TO_DEG:F1}°, " +
                      $"paint={_state.paintMassRemaining:F3}kg, " +
                      $"position={_state.bucketPosition}");
        }

        // ====================================================================
        // RENDERING
        // ====================================================================

        private void UpdateRenderers()
        {
            if (_bucketRenderer != null)
                _bucketRenderer.UpdateVisuals(_state, _config);

            if (_ropeRenderer != null)
                _ropeRenderer.UpdateVisuals(_state, _config);

            if (_particleRenderer != null)
                _particleRenderer.UpdateVisuals(_particleManager);

            if (_canvasPaintRenderer != null)
                _canvasPaintRenderer.UpdateVisuals(_particleManager, _state);
        }

        // ====================================================================
        // COMPLETION CHECKS
        // ====================================================================

        private void CheckCompletionConditions()
        {
            bool complete = false;

            // Paint depleted
            if (_state.paintDepleted && _state.activeFreeFallParticles == 0)
            {
                complete = true;
                Debug.Log("[SimulationManager] Simulation complete: paint depleted.");
            }

            // Max swings reached (if configured)
            if (_config.motion.maxSwingCount > 0 && _state.swingCount >= _config.motion.maxSwingCount)
            {
                complete = true;
                Debug.Log($"[SimulationManager] Simulation complete: max swings ({_config.motion.maxSwingCount}) reached.");
            }

            if (complete)
            {
                SetStatus(SimulationStatus.Completed);
                _dataRecorder?.FinalizeRecording(_state);
                OnSimulationComplete?.Invoke(_state);
            }
        }

        // ====================================================================
        // UTILITY
        // ====================================================================

        private void SetStatus(SimulationStatus newStatus)
        {
            if (_state.status != newStatus)
            {
                _state.status = newStatus;
                OnStatusChanged?.Invoke(newStatus);
            }
        }

        private void ValidateReferences()
        {
            if (_config == null)
                Debug.LogWarning("[SimulationManager] No SimulationConfig assigned. Assign one before starting.");
        }

        private void CleanupSystems()
        {
            _sphFluidSystem?.Dispose();
            _sphFluidSystem = null;

            _particleManager?.Dispose();
            _particleManager = null;

            _canvasPaintRenderer?.Cleanup();
            _particleRenderer?.Cleanup();
        }
    }
}
