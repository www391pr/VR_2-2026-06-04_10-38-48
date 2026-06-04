// ============================================================================
// RopeRenderer.cs — Rope visual rendering
// Swinging Paint Bucket Simulation
// ============================================================================

using UnityEngine;
using SwingingPaintBucket.Core;

namespace SwingingPaintBucket.Rendering
{
    /// <summary>
    /// Renders the rope as a LineRenderer between the suspension point and bucket.
    /// Shows tension through color/thickness variations.
    /// </summary>
    public class RopeRenderer : MonoBehaviour
    {
        [Header("Rendering")]
        [SerializeField] private LineRenderer _lineRenderer;

        [Header("Settings")]
        [SerializeField] private float _ropeWidth = 0.01f;
        [SerializeField] private Color _normalColor = new Color(0.6f, 0.4f, 0.2f);
        [SerializeField] private Color _stretchedColor = new Color(0.9f, 0.2f, 0.2f);
        [SerializeField] private int _ropeSegments = 20;

        private Vector3[] _ropePoints;

        private void Awake()
        {
            if (_lineRenderer == null)
                _lineRenderer = GetComponent<LineRenderer>();

            if (_lineRenderer == null)
                _lineRenderer = gameObject.AddComponent<LineRenderer>();

            _lineRenderer.positionCount = _ropeSegments + 1;
            _lineRenderer.startWidth = _ropeWidth;
            _lineRenderer.endWidth = _ropeWidth;
            _lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            _lineRenderer.startColor = _normalColor;
            _lineRenderer.endColor = _normalColor;

            _ropePoints = new Vector3[_ropeSegments + 1];
        }

        /// <summary>
        /// Updates the rope visual to match the simulation state.
        /// </summary>
        public void UpdateVisuals(SimulationState state, SimulationConfig config)
        {
            if (_lineRenderer == null) return;

            Vector3 suspensionPoint = config.suspension.position;
            // Attach the rope to the top of the handle/hanger (offset from the center)
            Vector3 bucketPoint = state.bucketPosition + state.bucketRotation * new Vector3(0.0f, 1.55f * config.bucket.height, 0.0f);

            // Generate rope points with slight catenary/sag
            for (int i = 0; i <= _ropeSegments; i++)
            {
                float t = (float)i / _ropeSegments;
                Vector3 point = Vector3.Lerp(suspensionPoint, bucketPoint, t);

                // Add slight sag for visual realism (parabolic approximation)
                // Sag is proportional to tension inversely
                float sag = 0.0f;
                if (state.ropeTension > 0.0f && state.ropeTension < 1000.0f)
                {
                    float sagFactor = 0.02f / Mathf.Max(state.ropeTension * 0.01f, 0.1f);
                    sag = sagFactor * Mathf.Sin(t * Mathf.PI);
                }
                point.y -= sag;

                _ropePoints[i] = point;
            }

            _lineRenderer.SetPositions(_ropePoints);

            // Color based on tension/stretch
            if (config.rope.isElastic)
            {
                float stretchRatio = state.currentRopeLength / config.rope.length;
                float t = Mathf.Clamp01((stretchRatio - 1.0f) * 5.0f); // 20% stretch = full red
                Color ropeColor = Color.Lerp(_normalColor, _stretchedColor, t);
                _lineRenderer.startColor = ropeColor;
                _lineRenderer.endColor = ropeColor;

                // Width increases with stretch
                float width = _ropeWidth * (1.0f + t * 0.5f);
                _lineRenderer.startWidth = width;
                _lineRenderer.endWidth = width;
            }
        }
    }
}
