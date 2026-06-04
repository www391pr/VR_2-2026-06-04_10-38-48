// ============================================================================
// ExperimentManager.cs — Experiment storage and comparison
// Swinging Paint Bucket Simulation
// ============================================================================

using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;

namespace SwingingPaintBucket.Data
{
    /// <summary>
    /// Manages storage, retrieval, and comparison of experiments.
    /// </summary>
    public class ExperimentManager
    {
        private List<ExperimentData> _experiments;
        private string _savePath;

        public List<ExperimentData> Experiments => _experiments;
        public int ExperimentCount => _experiments.Count;

        public ExperimentManager()
        {
            _experiments = new List<ExperimentData>();
            _savePath = Path.Combine(Application.persistentDataPath, "Experiments");

            if (!Directory.Exists(_savePath))
                Directory.CreateDirectory(_savePath);

            LoadAllExperiments();
        }

        /// <summary>
        /// Adds a completed experiment to storage.
        /// </summary>
        public void AddExperiment(ExperimentData experiment)
        {
            _experiments.Add(experiment);
            SaveExperiment(experiment);
        }

        /// <summary>
        /// Saves an experiment to disk as JSON.
        /// </summary>
        public void SaveExperiment(ExperimentData experiment)
        {
            string json = JsonUtility.ToJson(experiment, true);
            string filePath = Path.Combine(_savePath, $"experiment_{experiment.experimentId}.json");
            File.WriteAllText(filePath, json);
            Debug.Log($"[ExperimentManager] Saved experiment {experiment.experimentId} to {filePath}");
        }

        /// <summary>
        /// Loads all saved experiments from disk.
        /// </summary>
        public void LoadAllExperiments()
        {
            _experiments.Clear();

            if (!Directory.Exists(_savePath)) return;

            string[] files = Directory.GetFiles(_savePath, "experiment_*.json");
            foreach (string file in files)
            {
                try
                {
                    string json = File.ReadAllText(file);
                    ExperimentData data = JsonUtility.FromJson<ExperimentData>(json);
                    if (data != null)
                        _experiments.Add(data);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[ExperimentManager] Failed to load {file}: {e.Message}");
                }
            }

            Debug.Log($"[ExperimentManager] Loaded {_experiments.Count} experiments.");
        }

        /// <summary>
        /// Gets an experiment by ID.
        /// </summary>
        public ExperimentData GetExperiment(string experimentId)
        {
            return _experiments.Find(e => e.experimentId == experimentId);
        }

        /// <summary>
        /// Deletes an experiment.
        /// </summary>
        public void DeleteExperiment(string experimentId)
        {
            ExperimentData exp = GetExperiment(experimentId);
            if (exp != null)
            {
                _experiments.Remove(exp);
                string filePath = Path.Combine(_savePath, $"experiment_{experimentId}.json");
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
        }

        /// <summary>
        /// Compares two experiments and returns a comparison result.
        /// </summary>
        public ExperimentComparison Compare(string idA, string idB)
        {
            ExperimentData a = GetExperiment(idA);
            ExperimentData b = GetExperiment(idB);

            if (a == null || b == null) return null;

            return new ExperimentComparison(a, b);
        }
    }

    /// <summary>
    /// Result of comparing two experiments.
    /// </summary>
    [Serializable]
    public class ExperimentComparison
    {
        public ExperimentData experimentA;
        public ExperimentData experimentB;

        // ── Differences ──────────────────────────────────────────────────
        public List<ParameterDifference> parameterDifferences;

        // ── Statistical Comparison ───────────────────────────────────────
        public float durationDiff;
        public float coverageDiffPercent;
        public float paintUsedDiffPercent;
        public int particleCountDiff;
        public int swingCountDiff;

        public ExperimentComparison(ExperimentData a, ExperimentData b)
        {
            experimentA = a;
            experimentB = b;
            parameterDifferences = new List<ParameterDifference>();

            ComputeDifferences();
            ComputeStatistics();
        }

        private void ComputeDifferences()
        {
            // Compare all input parameters
            AddDiff("Bucket Mass (kg)", experimentA.bucketMass, experimentB.bucketMass);
            AddDiff("Bucket Radius (m)", experimentA.bucketRadius, experimentB.bucketRadius);
            AddDiff("Orifice Radius (m)", experimentA.orificeRadius, experimentB.orificeRadius);
            AddDiff("Paint Volume (m³)", experimentA.paintVolume, experimentB.paintVolume);
            AddDiff("Paint Viscosity (Pa·s)", experimentA.paintViscosity, experimentB.paintViscosity);
            AddDiff("Rope Length (m)", experimentA.ropeLength, experimentB.ropeLength);
            AddDiff("Initial θ (°)", experimentA.initialThetaDeg, experimentB.initialThetaDeg);
            AddDiff("Initial ϕ (°)", experimentA.initialPhiDeg, experimentB.initialPhiDeg);
            AddDiff("Gravity (m/s²)", experimentA.gravity, experimentB.gravity);
            AddDiff("Humidity", experimentA.humidity, experimentB.humidity);
            AddDiff("Canvas Width (m)", experimentA.canvasWidth, experimentB.canvasWidth);
            AddDiff("Canvas Height (m)", experimentA.canvasHeight, experimentB.canvasHeight);
            AddDiff("Canvas Inclination (°)", experimentA.canvasInclination, experimentB.canvasInclination);

            if (experimentA.orificeShape != experimentB.orificeShape)
                parameterDifferences.Add(new ParameterDifference("Orifice Shape",
                    experimentA.orificeShape, experimentB.orificeShape));

            if (experimentA.canvasSurface != experimentB.canvasSurface)
                parameterDifferences.Add(new ParameterDifference("Canvas Surface",
                    experimentA.canvasSurface, experimentB.canvasSurface));
        }

        private void AddDiff(string name, float a, float b)
        {
            if (Mathf.Abs(a - b) > Utilities.PhysicsConstants.EPSILON)
            {
                parameterDifferences.Add(new ParameterDifference(name,
                    a.ToString("F4"), b.ToString("F4")));
            }
        }

        private void ComputeStatistics()
        {
            durationDiff = experimentB.simulationDuration - experimentA.simulationDuration;
            coverageDiffPercent = experimentB.canvasCoveragePercent - experimentA.canvasCoveragePercent;
            paintUsedDiffPercent = experimentB.paintUsedPercent - experimentA.paintUsedPercent;
            particleCountDiff = experimentB.totalParticlesSpawned - experimentA.totalParticlesSpawned;
            swingCountDiff = experimentB.swingCount - experimentA.swingCount;
        }
    }

    [Serializable]
    public class ParameterDifference
    {
        public string parameterName;
        public string valueA;
        public string valueB;

        public ParameterDifference(string name, string a, string b)
        {
            parameterName = name;
            valueA = a;
            valueB = b;
        }
    }
}
