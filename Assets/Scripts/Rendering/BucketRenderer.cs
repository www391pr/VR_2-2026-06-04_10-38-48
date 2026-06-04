// ============================================================================
// BucketRenderer.cs — 3D bucket visualization
// Swinging Paint Bucket Simulation
// ============================================================================

using UnityEngine;
using SwingingPaintBucket.Core;

namespace SwingingPaintBucket.Rendering
{
    /// <summary>
    /// Renders the 3D bucket, updating position and rotation from simulation state.
    /// Also shows paint level indicator inside the bucket.
    /// </summary>
    public class BucketRenderer : MonoBehaviour
    {
        [Header("Bucket Visual")]
        [SerializeField] private Transform _bucketTransform;
        [SerializeField] private Transform _paintLevelIndicator;
        [SerializeField] private MeshRenderer _paintRenderer;

        [Header("Settings")]
        [SerializeField] private float _visualScale = 1.0f;

        private float _initialPaintHeight;
        private bool _initialized = false;

        /// <summary>
        /// Updates the bucket visual to match the simulation state.
        /// </summary>
        public void UpdateVisuals(SimulationState state, SimulationConfig config)
        {
            if (_bucketTransform == null) return;

            // Initialize on first frame
            if (!_initialized)
            {
                _initialPaintHeight = state.paintHeight;
                if (_initialPaintHeight < Utilities.PhysicsConstants.EPSILON)
                    _initialPaintHeight = config.paint.GetInitialHeight(config.bucket.radius);

                // Scale bucket to match config dimensions
                float diameter = config.bucket.radius * 2.0f * _visualScale;
                float height = config.bucket.height * _visualScale;
                _bucketTransform.localScale = new Vector3(diameter, height, diameter);

                _initialized = true;
            }

            // Position and rotation from simulation state
            _bucketTransform.position = state.bucketPosition;
            _bucketTransform.rotation = state.bucketRotation;

            // Hide static paint level indicator since we now use SPH particles
            if (_paintLevelIndicator != null && _paintLevelIndicator.gameObject.activeSelf)
            {
                _paintLevelIndicator.gameObject.SetActive(false);
            }

            // Update paint color
            if (_paintRenderer != null && config.paint.colors != null && config.paint.colors.Length > 0)
            {
                _paintRenderer.material.color = config.paint.colors[0];
            }
        }
    }
}
