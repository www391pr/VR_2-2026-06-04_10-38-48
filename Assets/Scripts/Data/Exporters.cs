// ============================================================================
// Exporters — CSV, JSON, PNG export
// Swinging Paint Bucket Simulation
// ============================================================================

using UnityEngine;
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace SwingingPaintBucket.Data
{
    /// <summary>
    /// Exports experiment data to CSV format.
    /// </summary>
    public static class CSVExporter
    {
        /// <summary>
        /// Exports experiment parameters and results to CSV.
        /// </summary>
        public static void ExportExperiment(ExperimentData data, string filePath)
        {
            StringBuilder sb = new StringBuilder();

            // Header
            sb.AppendLine("=== EXPERIMENT REPORT ===");
            sb.AppendLine($"Experiment ID,{data.experimentId}");
            sb.AppendLine($"Date,{data.timestamp}");
            sb.AppendLine();

            // Input Parameters
            sb.AppendLine("=== INPUT PARAMETERS ===");
            sb.AppendLine("Parameter,Value,Unit");
            sb.AppendLine($"Bucket Mass,{data.bucketMass:F4},kg");
            sb.AppendLine($"Bucket Radius,{data.bucketRadius:F4},m");
            sb.AppendLine($"Bucket Height,{data.bucketHeight:F4},m");
            sb.AppendLine($"Orifice Radius,{data.orificeRadius:F4},m");
            sb.AppendLine($"Orifice Shape,{data.orificeShape},");
            sb.AppendLine($"Paint Volume,{data.paintVolume:F6},m³");
            sb.AppendLine($"Paint Density,{data.paintDensity:F1},kg/m³");
            sb.AppendLine($"Paint Viscosity,{data.paintViscosity:F4},Pa·s");
            sb.AppendLine($"Rope Length,{data.ropeLength:F4},m");
            sb.AppendLine($"Rope Elastic,{data.ropeElastic},");
            sb.AppendLine($"Initial θ,{data.initialThetaDeg:F2},degrees");
            sb.AppendLine($"Initial ϕ,{data.initialPhiDeg:F2},degrees");
            sb.AppendLine($"Initial Velocity,{data.initialVelocity:F4},m/s");
            sb.AppendLine($"Gravity,{data.gravity:F4},m/s²");
            sb.AppendLine($"Air Damping,{data.airDamping:F4},1/s");
            sb.AppendLine($"Humidity,{data.humidity:F2},");
            sb.AppendLine($"Temperature,{data.temperature:F1},K");
            sb.AppendLine($"Wind Enabled,{data.windEnabled},");
            sb.AppendLine($"Canvas Width,{data.canvasWidth:F2},m");
            sb.AppendLine($"Canvas Height,{data.canvasHeight:F2},m");
            sb.AppendLine($"Canvas Surface,{data.canvasSurface},");
            sb.AppendLine($"Canvas Inclination,{data.canvasInclination:F1},degrees");
            sb.AppendLine();

            // Results
            sb.AppendLine("=== RESULTS ===");
            sb.AppendLine("Metric,Value,Unit");
            sb.AppendLine($"Duration,{data.simulationDuration:F2},seconds");
            sb.AppendLine($"Particles Spawned,{data.totalParticlesSpawned},");
            sb.AppendLine($"Swing Count,{data.swingCount},");
            sb.AppendLine($"Paint Used,{data.paintUsedPercent:F1},%");
            sb.AppendLine($"Canvas Coverage,{data.canvasCoveragePercent:F2},%");
            sb.AppendLine($"Paint Area,{data.totalPaintArea:F4},m²");
            sb.AppendLine($"Splash Events,{data.splashEventCount},");
            sb.AppendLine();

            // Performance
            sb.AppendLine("=== PERFORMANCE ===");
            sb.AppendLine($"Average FPS,{data.averageFPS:F1},");
            sb.AppendLine($"Min FPS,{data.minFPS:F1},");
            sb.AppendLine($"Peak Memory,{data.peakMemoryMB:F1},MB");
            sb.AppendLine($"Physics Steps,{data.totalPhysicsSteps},");
            sb.AppendLine();

            // Trajectory data
            if (data.trajectoryTimestamps != null && data.trajectoryTimestamps.Count > 0)
            {
                sb.AppendLine("=== TRAJECTORY DATA ===");
                sb.AppendLine("Time,X,Y,Z,Theta,Phi,PaintMass,FlowRate");

                for (int i = 0; i < data.trajectoryTimestamps.Count; i++)
                {
                    sb.AppendLine(
                        $"{data.trajectoryTimestamps[i]:F4}," +
                        $"{data.trajectoryX[i]:F6}," +
                        $"{data.trajectoryY[i]:F6}," +
                        $"{data.trajectoryZ[i]:F6}," +
                        $"{data.trajectoryTheta[i]:F6}," +
                        $"{data.trajectoryPhi[i]:F6}," +
                        $"{data.trajectoryPaintMass[i]:F6}," +
                        $"{data.trajectoryFlowRate[i]:F8}"
                    );
                }
            }

            File.WriteAllText(filePath, sb.ToString());
            Debug.Log($"[CSVExporter] Exported to {filePath}");
        }
    }

    /// <summary>
    /// Exports experiment data to JSON format.
    /// </summary>
    public static class JSONExporter
    {
        /// <summary>
        /// Exports experiment data to JSON.
        /// </summary>
        public static void ExportExperiment(ExperimentData data, string filePath)
        {
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(filePath, json);
            Debug.Log($"[JSONExporter] Exported to {filePath}");
        }
    }

    /// <summary>
    /// Exports canvas image as PNG.
    /// </summary>
    public static class PNGExporter
    {
        /// <summary>
        /// Saves a Texture2D to a PNG file.
        /// </summary>
        public static void ExportTexture(Texture2D texture, string filePath)
        {
            if (texture == null)
            {
                Debug.LogError("[PNGExporter] Texture is null!");
                return;
            }

            byte[] pngData = texture.EncodeToPNG();
            File.WriteAllBytes(filePath, pngData);
            Debug.Log($"[PNGExporter] Exported {texture.width}x{texture.height} to {filePath}");
        }

        /// <summary>
        /// Captures a RenderTexture and saves as PNG.
        /// </summary>
        public static void ExportRenderTexture(RenderTexture rt, string filePath)
        {
            Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            ExportTexture(tex, filePath);
            UnityEngine.Object.Destroy(tex);
        }
    }

    /// <summary>
    /// Generates formatted text reports.
    /// </summary>
    public static class ReportGenerator
    {
        /// <summary>
        /// Generates a complete text report for an experiment.
        /// </summary>
        public static string GenerateReport(ExperimentData data)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("╔══════════════════════════════════════════════════════╗");
            sb.AppendLine("║         SWINGING PAINT BUCKET — EXPERIMENT REPORT   ║");
            sb.AppendLine("╠══════════════════════════════════════════════════════╣");
            sb.AppendLine($"║ Experiment ID: {data.experimentId,-38}║");
            sb.AppendLine($"║ Date: {data.timestamp,-45}║");
            sb.AppendLine("╠══════════════════════════════════════════════════════╣");
            sb.AppendLine("║ INPUT PARAMETERS                                    ║");
            sb.AppendLine($"║ ├─ Bucket: {data.bucketMass:F2}kg, R={data.bucketRadius:F3}m         ║");
            sb.AppendLine($"║ ├─ Orifice: {data.orificeShape}, r={data.orificeRadius:F4}m   ║");
            sb.AppendLine($"║ ├─ Paint: V={data.paintVolume * 1000:F1}L, μ={data.paintViscosity:F2}Pa·s  ║");
            sb.AppendLine($"║ ├─ Rope: L={data.ropeLength:F2}m, elastic={data.ropeElastic}     ║");
            sb.AppendLine($"║ ├─ Motion: θ₀={data.initialThetaDeg:F1}°, ϕ₀={data.initialPhiDeg:F1}°       ║");
            sb.AppendLine($"║ ├─ Gravity: g={data.gravity:F2} m/s²                    ║");
            sb.AppendLine($"║ └─ Canvas: {data.canvasWidth:F1}×{data.canvasHeight:F1}m, {data.canvasSurface,-10}      ║");
            sb.AppendLine("╠══════════════════════════════════════════════════════╣");
            sb.AppendLine("║ RESULTS                                             ║");
            sb.AppendLine($"║ ├─ Duration: {data.simulationDuration:F2} seconds                ║");
            sb.AppendLine($"║ ├─ Particles: {data.totalParticlesSpawned,-10}                    ║");
            sb.AppendLine($"║ ├─ Swings: {data.swingCount,-10}                          ║");
            sb.AppendLine($"║ ├─ Paint used: {data.paintUsedPercent:F1}%                       ║");
            sb.AppendLine($"║ ├─ Coverage: {data.canvasCoveragePercent:F2}%                     ║");
            sb.AppendLine($"║ └─ Splashes: {data.splashEventCount,-10}                    ║");
            sb.AppendLine("╠══════════════════════════════════════════════════════╣");
            sb.AppendLine("║ PERFORMANCE                                         ║");
            sb.AppendLine($"║ ├─ Avg FPS: {data.averageFPS:F1}                           ║");
            sb.AppendLine($"║ ├─ Min FPS: {data.minFPS:F1}                           ║");
            sb.AppendLine($"║ └─ Peak Memory: {data.peakMemoryMB:F1} MB                   ║");
            sb.AppendLine("╚══════════════════════════════════════════════════════╝");

            return sb.ToString();
        }

        /// <summary>
        /// Generates a comparison report between two experiments.
        /// </summary>
        public static string GenerateComparisonReport(ExperimentComparison comparison)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("═══ EXPERIMENT COMPARISON REPORT ═══");
            sb.AppendLine($"Experiment A: {comparison.experimentA.experimentId} ({comparison.experimentA.timestamp})");
            sb.AppendLine($"Experiment B: {comparison.experimentB.experimentId} ({comparison.experimentB.timestamp})");
            sb.AppendLine();

            sb.AppendLine("─── PARAMETER DIFFERENCES ───");
            if (comparison.parameterDifferences.Count == 0)
            {
                sb.AppendLine("No parameter differences found.");
            }
            else
            {
                sb.AppendLine(String.Format("{0,-25} {1,-15} {2,-15}", "Parameter", "Exp A", "Exp B"));
                sb.AppendLine(new string('─', 55));
                foreach (var diff in comparison.parameterDifferences)
                {
                    sb.AppendLine(String.Format("{0,-25} {1,-15} {2,-15}",
                        diff.parameterName, diff.valueA, diff.valueB));
                }
            }

            sb.AppendLine();
            sb.AppendLine("─── RESULT DIFFERENCES ───");
            sb.AppendLine($"Duration diff: {comparison.durationDiff:+0.00;-0.00;0.00} seconds");
            sb.AppendLine($"Coverage diff: {comparison.coverageDiffPercent:+0.00;-0.00;0.00}%");
            sb.AppendLine($"Paint used diff: {comparison.paintUsedDiffPercent:+0.00;-0.00;0.00}%");
            sb.AppendLine($"Particle count diff: {comparison.particleCountDiff:+0;-0;0}");
            sb.AppendLine($"Swing count diff: {comparison.swingCountDiff:+0;-0;0}");

            return sb.ToString();
        }
    }
}
