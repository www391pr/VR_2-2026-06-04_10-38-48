// ============================================================================
// UIManager.cs — Main UI controller
// Swinging Paint Bucket Simulation
// ============================================================================

using UnityEngine;
using UnityEngine.UI;
using SwingingPaintBucket.Core;
using SwingingPaintBucket.Data;
using System.IO;

namespace SwingingPaintBucket.UI
{
    /// <summary>
    /// Manages all UI panels and coordinates between UI and simulation.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SimulationManager _simulationManager;

        [Header("Panels")]
        [SerializeField] private GameObject _mainMenuPanel;
        [SerializeField] private GameObject _simulationPanel;
        [SerializeField] private GameObject _parameterPanel;
        [SerializeField] private GameObject _physicsDisplayPanel;
        [SerializeField] private GameObject _resultsPanel;
        [SerializeField] private GameObject _comparisonPanel;

        [Header("Simulation Controls")]
        [SerializeField] private Button _startButton;
        [SerializeField] private Button _pauseButton;
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _resetButton;
        [SerializeField] private Slider _speedSlider;
        [SerializeField] private Text _speedLabel;

        [Header("Physics Display")]
        [SerializeField] private Text _thetaDisplay;
        [SerializeField] private Text _phiDisplay;
        [SerializeField] private Text _velocityDisplay;
        [SerializeField] private Text _paintRemainingDisplay;
        [SerializeField] private Text _flowRateDisplay;
        [SerializeField] private Text _particleCountDisplay;
        [SerializeField] private Text _fpsDisplay;
        [SerializeField] private Text _swingCountDisplay;
        [SerializeField] private Text _elapsedTimeDisplay;

        [Header("Parameter Inputs — Bucket")]
        [SerializeField] private Slider _bucketMassSlider;
        [SerializeField] private Slider _bucketRadiusSlider;
        [SerializeField] private Slider _orificeRadiusSlider;
        [SerializeField] private Slider _paintVolumeSlider;
        [SerializeField] private Text _bucketMassLabel;
        [SerializeField] private Text _bucketRadiusLabel;
        [SerializeField] private Text _orificeRadiusLabel;
        [SerializeField] private Text _paintVolumeLabel;

        [Header("Parameter Inputs — Rope")]
        [SerializeField] private Slider _ropeLengthSlider;
        [SerializeField] private Toggle _ropeElasticToggle;
        [SerializeField] private Slider _ropeDampingSlider;
        [SerializeField] private Text _ropeLengthLabel;

        [Header("Parameter Inputs — Motion")]
        [SerializeField] private Slider _thetaInitSlider;
        [SerializeField] private Slider _phiInitSlider;
        [SerializeField] private Slider _initialVelocitySlider;
        [SerializeField] private Text _thetaInitLabel;
        [SerializeField] private Text _phiInitLabel;

        [Header("Parameter Inputs — Environment")]
        [SerializeField] private Slider _gravitySlider;
        [SerializeField] private Slider _humiditySlider;
        [SerializeField] private Toggle _windToggle;
        [SerializeField] private Text _gravityLabel;
        [SerializeField] private Text _humidityLabel;

        [Header("Parameter Inputs — Paint")]
        [SerializeField] private Slider _viscositySlider;
        [SerializeField] private Text _viscosityLabel;

        [Header("Parameter Inputs — Canvas")]
        [SerializeField] private Slider _canvasWidthSlider;
        [SerializeField] private Slider _canvasHeightSlider;
        [SerializeField] private Dropdown _surfaceTypeDropdown;
        [SerializeField] private Slider _inclinationSlider;

        [Header("Results")]
        [SerializeField] private Text _resultsText;
        [SerializeField] private RawImage _canvasPreview;
        [SerializeField] private Button _saveImageButton;
        [SerializeField] private Button _exportCSVButton;
        [SerializeField] private Button _exportJSONButton;
        [SerializeField] private Button _newExperimentButton;

        [Header("Comparison")]
        [SerializeField] private Dropdown _experimentADropdown;
        [SerializeField] private Dropdown _experimentBDropdown;
        [SerializeField] private Button _compareButton;
        [SerializeField] private Text _comparisonText;

        [Header("Camera Control UI")]
        [SerializeField] private Button _topViewButton;
        [SerializeField] private Button _sideViewButton;
        [SerializeField] private Button _frontViewButton;
        [SerializeField] private Button _freeViewButton;
        [SerializeField] private Button _focusButton;

        // ── Private ──────────────────────────────────────────────────────
        private SimulationConfig _config;
        private ExperimentManager _experimentManager;
        private bool _simulationActive = false;

        private void Awake()
        {
            _experimentManager = new ExperimentManager();
        }

        private void Start()
        {
            // Create default config if none assigned
            if (_simulationManager != null && _simulationManager.Config != null)
                _config = _simulationManager.Config;
            else
                _config = ScriptableObject.CreateInstance<SimulationConfig>();

            SetupEventListeners();
            SetupSliderRanges();
            ShowMainMenu();
            UpdateParameterLabels();
        }

        private void Update()
        {
            if (_simulationActive && _simulationManager != null && _simulationManager.State != null)
            {
                UpdatePhysicsDisplay();
            }
        }

        // ====================================================================
        // EVENT LISTENERS
        // ====================================================================

        private void SetupEventListeners()
        {
            // Simulation controls
            if (_startButton != null) _startButton.onClick.AddListener(OnStartClicked);
            if (_pauseButton != null) _pauseButton.onClick.AddListener(OnPauseClicked);
            if (_resumeButton != null) _resumeButton.onClick.AddListener(OnResumeClicked);
            if (_resetButton != null) _resetButton.onClick.AddListener(OnResetClicked);
            if (_speedSlider != null) _speedSlider.onValueChanged.AddListener(OnSpeedChanged);

            // Parameter sliders
            if (_bucketMassSlider != null) _bucketMassSlider.onValueChanged.AddListener(v => { _config.bucket.mass = v; UpdateParameterLabels(); });
            if (_bucketRadiusSlider != null) _bucketRadiusSlider.onValueChanged.AddListener(v => { _config.bucket.radius = v; UpdateParameterLabels(); });
            if (_orificeRadiusSlider != null) _orificeRadiusSlider.onValueChanged.AddListener(v => { _config.bucket.orificeRadius = v; UpdateParameterLabels(); });
            if (_paintVolumeSlider != null) _paintVolumeSlider.onValueChanged.AddListener(v => { _config.paint.initialVolume = v; UpdateParameterLabels(); });
            if (_ropeLengthSlider != null) _ropeLengthSlider.onValueChanged.AddListener(v => { _config.rope.length = v; UpdateParameterLabels(); });
            if (_ropeElasticToggle != null) _ropeElasticToggle.onValueChanged.AddListener(v => _config.rope.isElastic = v);
            if (_thetaInitSlider != null) _thetaInitSlider.onValueChanged.AddListener(v => { _config.motion.initialThetaDegrees = v; UpdateParameterLabels(); });
            if (_phiInitSlider != null) _phiInitSlider.onValueChanged.AddListener(v => { _config.motion.initialPhiDegrees = v; UpdateParameterLabels(); });
            if (_gravitySlider != null) _gravitySlider.onValueChanged.AddListener(v => { _config.environment.gravity = v; UpdateParameterLabels(); });
            if (_humiditySlider != null) _humiditySlider.onValueChanged.AddListener(v => { _config.environment.humidity = v; UpdateParameterLabels(); });
            if (_viscositySlider != null) _viscositySlider.onValueChanged.AddListener(v => { _config.paint.viscosity = v; UpdateParameterLabels(); });
            if (_windToggle != null) _windToggle.onValueChanged.AddListener(v => _config.environment.windEnabled = v);

            // Export buttons
            if (_saveImageButton != null) _saveImageButton.onClick.AddListener(OnSaveImage);
            if (_exportCSVButton != null) _exportCSVButton.onClick.AddListener(OnExportCSV);
            if (_exportJSONButton != null) _exportJSONButton.onClick.AddListener(OnExportJSON);
            if (_newExperimentButton != null) _newExperimentButton.onClick.AddListener(OnNewExperiment);
            if (_compareButton != null) _compareButton.onClick.AddListener(OnCompare);

            // Camera preset buttons
            if (_topViewButton != null) _topViewButton.onClick.AddListener(() => FindObjectOfType<CameraController>()?.SetTopView());
            if (_sideViewButton != null) _sideViewButton.onClick.AddListener(() => FindObjectOfType<CameraController>()?.SetSideView());
            if (_frontViewButton != null) _frontViewButton.onClick.AddListener(() => FindObjectOfType<CameraController>()?.SetFrontView());
            if (_freeViewButton != null) _freeViewButton.onClick.AddListener(() => FindObjectOfType<CameraController>()?.SetFreeView());
            if (_focusButton != null) _focusButton.onClick.AddListener(() => FindObjectOfType<CameraController>()?.FocusOnTarget());

            // Simulation events
            if (_simulationManager != null)
            {
                _simulationManager.OnStatusChanged += OnSimulationStatusChanged;
                _simulationManager.OnSimulationComplete += OnSimulationComplete;
            }
        }

        private void SetupSliderRanges()
        {
            SetSlider(_bucketMassSlider, 0.05f, 10.0f, _config.bucket.mass);
            SetSlider(_bucketRadiusSlider, 0.02f, 0.5f, _config.bucket.radius);
            SetSlider(_orificeRadiusSlider, 0.001f, 0.05f, _config.bucket.orificeRadius);
            SetSlider(_paintVolumeSlider, 0.0001f, 0.01f, _config.paint.initialVolume);
            SetSlider(_ropeLengthSlider, 0.1f, 10.0f, _config.rope.length);
            SetSlider(_thetaInitSlider, 0.0f, 90.0f, _config.motion.initialThetaDegrees);
            SetSlider(_phiInitSlider, 0.0f, 360.0f, _config.motion.initialPhiDegrees);
            SetSlider(_gravitySlider, 0.1f, 30.0f, _config.environment.gravity);
            SetSlider(_humiditySlider, 0.0f, 1.0f, _config.environment.humidity);
            SetSlider(_viscositySlider, 0.01f, 10.0f, _config.paint.viscosity);
            SetSlider(_speedSlider, 0.1f, 10.0f, 1.0f);
        }

        private void SetSlider(Slider slider, float min, float max, float value)
        {
            if (slider == null) return;
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;
        }

        // ====================================================================
        // PANEL MANAGEMENT
        // ====================================================================

        private void ShowMainMenu()
        {
            SetPanel(_mainMenuPanel, true);
            SetPanel(_simulationPanel, false);
            SetPanel(_resultsPanel, false);
            SetPanel(_comparisonPanel, false);
            _simulationActive = false;
        }

        private void ShowSimulation()
        {
            SetPanel(_mainMenuPanel, false);
            SetPanel(_simulationPanel, true);
            SetPanel(_parameterPanel, true);
            SetPanel(_physicsDisplayPanel, true);
            SetPanel(_resultsPanel, false);
            SetPanel(_comparisonPanel, false);
            _simulationActive = true;
        }

        private void ShowResults()
        {
            SetPanel(_resultsPanel, true);
        }

        private void SetPanel(GameObject panel, bool active)
        {
            if (panel != null) panel.SetActive(active);
        }

        // ====================================================================
        // BUTTON HANDLERS
        // ====================================================================

        private void OnStartClicked()
        {
            if (_simulationManager == null) return;
            _simulationManager.ApplyConfig(_config);
            _simulationManager.StartSimulation();
            ShowSimulation();
        }

        private void OnPauseClicked()
        {
            _simulationManager?.PauseSimulation();
        }

        private void OnResumeClicked()
        {
            _simulationManager?.ResumeSimulation();
        }

        private void OnResetClicked()
        {
            _simulationManager?.RestartSimulation();
        }

        private void OnSpeedChanged(float value)
        {
            _simulationManager?.SetTimeScale(value);
            if (_speedLabel != null)
                _speedLabel.text = $"Speed: {value:F1}x";
        }

        private void OnSaveImage()
        {
            // Save canvas image as PNG
            string path = Path.Combine(Application.persistentDataPath,
                $"canvas_{System.DateTime.Now:yyyyMMdd_HHmmss}.png");

            var canvasRenderer = FindObjectOfType<Rendering.CanvasPaintRenderer>();
            if (canvasRenderer != null)
            {
                Texture2D tex = canvasRenderer.CaptureCanvasImage();
                if (tex != null)
                {
                    PNGExporter.ExportTexture(tex, path);
                    Destroy(tex);
                }
            }
        }

        private void OnExportCSV()
        {
            if (_simulationManager?.DataRecorder?.CurrentExperiment == null) return;
            string path = Path.Combine(Application.persistentDataPath,
                $"experiment_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv");
            CSVExporter.ExportExperiment(_simulationManager.DataRecorder.CurrentExperiment, path);
        }

        private void OnExportJSON()
        {
            if (_simulationManager?.DataRecorder?.CurrentExperiment == null) return;
            string path = Path.Combine(Application.persistentDataPath,
                $"experiment_{System.DateTime.Now:yyyyMMdd_HHmmss}.json");
            JSONExporter.ExportExperiment(_simulationManager.DataRecorder.CurrentExperiment, path);
        }

        private void OnNewExperiment()
        {
            ShowMainMenu();
            _simulationManager?.StopSimulation();
        }

        private void OnCompare()
        {
            // Comparison logic would go here
            // Uses ExperimentManager to compare selected experiments
        }

        // ====================================================================
        // SIMULATION EVENTS
        // ====================================================================

        private void OnSimulationStatusChanged(SimulationStatus status)
        {
            if (_pauseButton != null) _pauseButton.gameObject.SetActive(status == SimulationStatus.Running);
            if (_resumeButton != null) _resumeButton.gameObject.SetActive(status == SimulationStatus.Paused);
        }

        private void OnSimulationComplete(SimulationState finalState)
        {
            // Show results
            ShowResults();

            // Generate report text
            if (_resultsText != null && _simulationManager?.DataRecorder?.CurrentExperiment != null)
            {
                string report = ReportGenerator.GenerateReport(
                    _simulationManager.DataRecorder.CurrentExperiment);
                _resultsText.text = report;
            }

            // Save experiment
            if (_simulationManager?.DataRecorder?.CurrentExperiment != null)
            {
                _experimentManager.AddExperiment(_simulationManager.DataRecorder.CurrentExperiment);
            }

            // Display canvas preview
            if (_canvasPreview != null)
            {
                var canvasRenderer = FindObjectOfType<Rendering.CanvasPaintRenderer>();
                if (canvasRenderer != null)
                {
                    Texture2D tex = canvasRenderer.CaptureCanvasImage();
                    _canvasPreview.texture = tex;
                }
            }
        }

        // ====================================================================
        // DISPLAY UPDATES
        // ====================================================================

        private void UpdatePhysicsDisplay()
        {
            var state = _simulationManager.State;
            if (state == null) return;

            float radToDeg = Utilities.PhysicsConstants.RAD_TO_DEG;

            SetText(_thetaDisplay, $"θ: {state.theta * radToDeg:F2}°");
            SetText(_phiDisplay, $"ϕ: {state.phi * radToDeg:F2}°");
            SetText(_velocityDisplay, $"v: {state.bucketVelocity.magnitude:F3} m/s");
            SetText(_paintRemainingDisplay, $"Paint: {state.paintMassRemaining:F4} kg");
            SetText(_flowRateDisplay, $"Flow: {state.flowRate * 1e6f:F2} mL/s");
            SetText(_particleCountDisplay, $"Particles: {state.TotalActiveParticles}");
            SetText(_fpsDisplay, $"FPS: {state.currentFPS:F0}");
            SetText(_swingCountDisplay, $"Swings: {state.swingCount}");
            SetText(_elapsedTimeDisplay, $"Time: {state.elapsedTime:F2}s");
        }

        private void UpdateParameterLabels()
        {
            SetText(_bucketMassLabel, $"{_config.bucket.mass:F2} kg");
            SetText(_bucketRadiusLabel, $"{_config.bucket.radius:F3} m");
            SetText(_orificeRadiusLabel, $"{_config.bucket.orificeRadius:F4} m");
            SetText(_paintVolumeLabel, $"{_config.paint.initialVolume * 1000:F1} L");
            SetText(_ropeLengthLabel, $"{_config.rope.length:F2} m");
            SetText(_thetaInitLabel, $"{_config.motion.initialThetaDegrees:F1}°");
            SetText(_phiInitLabel, $"{_config.motion.initialPhiDegrees:F1}°");
            SetText(_gravityLabel, $"{_config.environment.gravity:F2} m/s²");
            SetText(_humidityLabel, $"{_config.environment.humidity * 100:F0}%");
            SetText(_viscosityLabel, $"{_config.paint.viscosity:F2} Pa·s");
        }

        private void SetText(Text text, string value)
        {
            if (text != null) text.text = value;
        }
    }
}
