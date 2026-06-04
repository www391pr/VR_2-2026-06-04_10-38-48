// ============================================================================
// CameraController.cs — Orbital camera controller
// Swinging Paint Bucket Simulation
// ============================================================================

using UnityEngine;
using SwingingPaintBucket.Core;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SwingingPaintBucket.UI
{
    /// <summary>
    /// Provides orbital camera control for viewing the simulation.
    /// Supports orbit, zoom, pan, and preset views (top, side, free).
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform _target;

        [Header("Orbit Settings")]
        [SerializeField] private float _orbitSpeed = 5.0f;
        [SerializeField] private float _zoomSpeed = 5.0f;
        [SerializeField] private float _panSpeed = 0.5f;
        [SerializeField] private float _minDistance = 0.5f;
        [SerializeField] private float _maxDistance = 20.0f;
        [SerializeField] private float _minPitch = -89.0f;
        [SerializeField] private float _maxPitch = 89.0f;

        [Header("Initial")]
        [SerializeField] private float _initialDistance = 5.0f;
        [SerializeField] private float _initialYaw = 45.0f;
        [SerializeField] private float _initialPitch = 30.0f;

        [Header("Smoothing")]
        [SerializeField] private float _smoothTime = 0.1f;

        private float _yaw;
        private float _pitch;
        private float _distance;
        private Vector3 _targetPosition;

        private float _currentYaw;
        private float _currentPitch;
        private float _currentDistance;
        private Vector3 _currentTarget;

        private float _yawVelocity;
        private float _pitchVelocity;
        private float _distanceVelocity;
        private Vector3 _targetVelocity;

        private void Start()
        {
            _yaw = _initialYaw;
            _pitch = _initialPitch;
            _distance = _initialDistance;
            _targetPosition = _target != null ? _target.position : Vector3.zero;

            _currentYaw = _yaw;
            _currentPitch = _pitch;
            _currentDistance = _distance;
            _currentTarget = _targetPosition;

            UpdateCameraPosition();
        }

        private void LateUpdate()
        {
            HandleInput();

            // Smooth interpolation
            _currentYaw = Mathf.SmoothDamp(_currentYaw, _yaw, ref _yawVelocity, _smoothTime);
            _currentPitch = Mathf.SmoothDamp(_currentPitch, _pitch, ref _pitchVelocity, _smoothTime);
            _currentDistance = Mathf.SmoothDamp(_currentDistance, _distance, ref _distanceVelocity, _smoothTime);
            _currentTarget = Vector3.SmoothDamp(_currentTarget, _targetPosition, ref _targetVelocity, _smoothTime);

            UpdateCameraPosition();
        }

        private void HandleInput()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                // Right-click drag: orbit
                if (Mouse.current.rightButton.isPressed)
                {
                    Vector2 mouseDelta = Mouse.current.delta.ReadValue() * 0.1f;
                    _yaw += mouseDelta.x * _orbitSpeed * 0.1f;
                    _pitch -= mouseDelta.y * _orbitSpeed * 0.1f;
                    _pitch = Mathf.Clamp(_pitch, _minPitch, _maxPitch);
                }

                // Middle-click drag: pan
                if (Mouse.current.middleButton.isPressed)
                {
                    Vector2 mouseDelta = Mouse.current.delta.ReadValue() * 0.01f;
                    Vector3 right = transform.right;
                    Vector3 up = transform.up;
                    _targetPosition -= (right * mouseDelta.x + up * mouseDelta.y) * _panSpeed;
                }

                // Scroll: zoom
                float scroll = Mouse.current.scroll.ReadValue().y * 0.01f;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    _distance -= scroll * _zoomSpeed * 0.1f;
                    _distance = Mathf.Clamp(_distance, _minDistance, _maxDistance);
                }
            }

            if (Keyboard.current != null)
            {
                if (Keyboard.current.digit1Key.wasPressedThisFrame) SetTopView();
                if (Keyboard.current.digit2Key.wasPressedThisFrame) SetSideView();
                if (Keyboard.current.digit3Key.wasPressedThisFrame) SetFrontView();
                if (Keyboard.current.digit4Key.wasPressedThisFrame) SetFreeView();
                if (Keyboard.current.fKey.wasPressedThisFrame) FocusOnTarget();
            }
#else
            // Legacy Input fallback
            // Right-click drag: orbit
            if (Input.GetMouseButton(1))
            {
                _yaw += Input.GetAxis("Mouse X") * _orbitSpeed;
                _pitch -= Input.GetAxis("Mouse Y") * _orbitSpeed;
                _pitch = Mathf.Clamp(_pitch, _minPitch, _maxPitch);
            }

            // Middle-click drag: pan
            if (Input.GetMouseButton(2))
            {
                Vector3 right = transform.right;
                Vector3 up = transform.up;
                _targetPosition -= (right * Input.GetAxis("Mouse X") + up * Input.GetAxis("Mouse Y")) * _panSpeed;
            }

            // Scroll: zoom
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                _distance -= scroll * _zoomSpeed;
                _distance = Mathf.Clamp(_distance, _minDistance, _maxDistance);
            }

            // Keyboard shortcuts for preset views
            if (Input.GetKeyDown(KeyCode.Alpha1)) SetTopView();
            if (Input.GetKeyDown(KeyCode.Alpha2)) SetSideView();
            if (Input.GetKeyDown(KeyCode.Alpha3)) SetFrontView();
            if (Input.GetKeyDown(KeyCode.Alpha4)) SetFreeView();
            if (Input.GetKeyDown(KeyCode.F)) FocusOnTarget();
#endif
        }

        private void UpdateCameraPosition()
        {
            float yawRad = _currentYaw * Mathf.Deg2Rad;
            float pitchRad = _currentPitch * Mathf.Deg2Rad;

            Vector3 offset = new Vector3(
                _currentDistance * Mathf.Cos(pitchRad) * Mathf.Sin(yawRad),
                _currentDistance * Mathf.Sin(pitchRad),
                _currentDistance * Mathf.Cos(pitchRad) * Mathf.Cos(yawRad)
            );

            transform.position = _currentTarget + offset;
            transform.LookAt(_currentTarget);
        }

        // ── Preset Views ─────────────────────────────────────────────────

        /// <summary>Top-down view (looking at canvas).</summary>
        public void SetTopView()
        {
            _yaw = 0;
            _pitch = 89;
            _distance = 5;
            _targetPosition = new Vector3(0, 0, 0);
        }

        /// <summary>Side view.</summary>
        public void SetSideView()
        {
            _yaw = 0;
            _pitch = 10;
            _distance = 6;
            _targetPosition = new Vector3(0, 1.5f, 0);
        }

        /// <summary>Front view.</summary>
        public void SetFrontView()
        {
            _yaw = 90;
            _pitch = 10;
            _distance = 6;
            _targetPosition = new Vector3(0, 1.5f, 0);
        }

        /// <summary>Default 3/4 view.</summary>
        public void SetFreeView()
        {
            _yaw = _initialYaw;
            _pitch = _initialPitch;
            _distance = _initialDistance;
            _targetPosition = new Vector3(0, 1.5f, 0);
        }

        /// <summary>Focus on current bucket position.</summary>
        public void FocusOnTarget()
        {
            if (_target != null)
                _targetPosition = _target.position;
        }

        /// <summary>
        /// Sets the focus target dynamically (e.g., to follow bucket).
        /// </summary>
        public void SetTarget(Vector3 worldPosition)
        {
            _targetPosition = worldPosition;
        }
    }
}
