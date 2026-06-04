// ============================================================================
// ExperimentData.cs — Experiment data model and recording
// Swinging Paint Bucket Simulation
// ============================================================================

using UnityEngine;
using System;
using System.Collections.Generic;
using SwingingPaintBucket.Core;

namespace SwingingPaintBucket.Data
{
    /// <summary>
    /// Complete data record of a single simulation experiment.
    /// Stores inputs, outputs, metrics, and trajectory data.
    /// </summary>
    [Serializable]
    public class ExperimentData
    {
        // ── Identification ───────────────────────────────────────────────
        public string experimentId;
        public string timestamp;
        public string experimentName;

        // ── Input Parameters (snapshot of config at start) ───────────────
        // Bucket
        public float bucketMass;
        public float bucketRadius;
        public float bucketHeight;
        public float orificeRadius;
        public string orificeShape;
        public float paintVolume;
        public float paintDensity;
        public float paintViscosity;

        // Rope
        public float ropeLength;
        public bool ropeElastic;
        public float ropeStiffness;

        // Motion
        public float initialThetaDeg;
        public float initialPhiDeg;
        public float initialVelocity;

        // Environment
        public float gravity;
        public float airDamping;
        public float humidity;
        public float temperature;
        public bool windEnabled;
        public Vector3 windVelocity;

        // Canvas
        public float canvasWidth;
        public float canvasHeight;
        public string canvasSurface;
        public float canvasInclination;

        // Paint colors
        public float[] paintColorR;
        public float[] paintColorG;
        public float[] paintColorB;

        // ── Output Metrics ───────────────────────────────────────────────
        public float simulationDuration;
        public int totalParticlesSpawned;
        public int swingCount;
        public float paintUsedPercent;
        public float canvasCoveragePercent;
        public float totalPaintArea;
        public int splashEventCount;

        // ── Performance Metrics ──────────────────────────────────────────
        public float averageFPS;
        public float minFPS;
        public float peakMemoryMB;
        public int totalPhysicsSteps;

        // ── Trajectory Data (sampled) ────────────────────────────────────
        public List<float> trajectoryTimestamps;
        public List<float> trajectoryX;
        public List<float> trajectoryY;
        public List<float> trajectoryZ;
        public List<float> trajectoryTheta;
        public List<float> trajectoryPhi;
        public List<float> trajectoryPaintMass;
        public List<float> trajectoryFlowRate;

        // ── Canvas Image Path ────────────────────────────────────────────
        public string canvasImagePath;

        public ExperimentData()
        {
            experimentId = Guid.NewGuid().ToString("N").Substring(0, 8);
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            trajectoryTimestamps = new List<float>();
            trajectoryX = new List<float>();
            trajectoryY = new List<float>();
            trajectoryZ = new List<float>();
            trajectoryTheta = new List<float>();
            trajectoryPhi = new List<float>();
            trajectoryPaintMass = new List<float>();
            trajectoryFlowRate = new List<float>();
        }

        /// <summary>
        /// Populates input fields from a SimulationConfig.
        /// </summary>
        public void CaptureInputs(SimulationConfig config)
        {
            bucketMass = config.bucket.mass;
            bucketRadius = config.bucket.radius;
            bucketHeight = config.bucket.height;
            orificeRadius = config.bucket.orificeRadius;
            orificeShape = config.bucket.orificeShape.ToString();
            paintVolume = config.paint.initialVolume;
            paintDensity = config.paint.density;
            paintViscosity = config.paint.viscosity;

            ropeLength = config.rope.length;
            ropeElastic = config.rope.isElastic;
            ropeStiffness = config.rope.stiffness;

            initialThetaDeg = config.motion.initialThetaDegrees;
            initialPhiDeg = config.motion.initialPhiDegrees;
            initialVelocity = config.motion.initialVelocity;

            gravity = config.environment.gravity;
            airDamping = config.environment.airDampingCoefficient;
            humidity = config.environment.humidity;
            temperature = config.environment.temperature;
            windEnabled = config.environment.windEnabled;
            windVelocity = config.environment.windVelocity;

            canvasWidth = config.canvas.width;
            canvasHeight = config.canvas.height;
            canvasSurface = config.canvas.surfaceType.ToString();
            canvasInclination = config.canvas.inclinationDegrees;

            if (config.paint.colors != null)
            {
                paintColorR = new float[config.paint.colors.Length];
                paintColorG = new float[config.paint.colors.Length];
                paintColorB = new float[config.paint.colors.Length];
                for (int i = 0; i < config.paint.colors.Length; i++)
                {
                    paintColorR[i] = config.paint.colors[i].r;
                    paintColorG[i] = config.paint.colors[i].g;
                    paintColorB[i] = config.paint.colors[i].b;
                }
            }
        }

        /// <summary>
        /// Populates output fields from final simulation state.
        /// </summary>
        public void CaptureOutputs(SimulationState state)
        {
            simulationDuration = state.elapsedTime;
            totalParticlesSpawned = state.totalParticlesSpawned;
            swingCount = state.swingCount;
            canvasCoveragePercent = state.canvasCoveragePercent;
            totalPaintArea = state.totalPaintArea;
            splashEventCount = state.splashEventCount;

            averageFPS = state.averageFPS;
            minFPS = state.minFPS;
            peakMemoryMB = state.peakMemoryMB;
            totalPhysicsSteps = state.physicsStepCount;

            // Compute paint usage
            float initialMass = paintDensity * paintVolume;
            if (initialMass > 0)
                paintUsedPercent = (1.0f - state.paintMassRemaining / initialMass) * 100.0f;
        }
    }

    /// <summary>
    /// Records simulation data at regular intervals during execution.
    /// </summary>
    public class DataRecorder
    {
        private ExperimentData _currentExperiment;
        private float _lastSampleTime;
        private float _sampleInterval;
        private int _maxSamples;

        public ExperimentData CurrentExperiment => _currentExperiment;

        public void Initialize(SimulationConfig config)
        {
            _currentExperiment = new ExperimentData();
            _currentExperiment.CaptureInputs(config);
            _lastSampleTime = 0.0f;
            _sampleInterval = Utilities.SimulationLimits.TRAJECTORY_SAMPLE_INTERVAL;
            _maxSamples = Utilities.SimulationLimits.MAX_TRAJECTORY_SAMPLES;
        }

        /// <summary>
        /// Records a data point from the current simulation state.
        /// Samples at fixed intervals to control data size.
        /// </summary>
        public void RecordStep(SimulationState state)
        {
            if (_currentExperiment == null) return;

            // Sample trajectory at fixed intervals
            if (state.elapsedTime - _lastSampleTime >= _sampleInterval &&
                _currentExperiment.trajectoryTimestamps.Count < _maxSamples)
            {
                _lastSampleTime = state.elapsedTime;

                _currentExperiment.trajectoryTimestamps.Add(state.elapsedTime);
                _currentExperiment.trajectoryX.Add(state.bucketPosition.x);
                _currentExperiment.trajectoryY.Add(state.bucketPosition.y);
                _currentExperiment.trajectoryZ.Add(state.bucketPosition.z);
                _currentExperiment.trajectoryTheta.Add(state.theta);
                _currentExperiment.trajectoryPhi.Add(state.phi);
                _currentExperiment.trajectoryPaintMass.Add(state.paintMassRemaining);
                _currentExperiment.trajectoryFlowRate.Add(state.flowRate);
            }
        }

        /// <summary>
        /// Finalizes recording with output metrics.
        /// </summary>
        public void FinalizeRecording(SimulationState state)
        {
            if (_currentExperiment == null) return;
            _currentExperiment.CaptureOutputs(state);
        }
    }
}
