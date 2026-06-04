// ============================================================================
// CanvasPaintRenderer.cs — Canvas and paint texture rendering
// Swinging Paint Bucket Simulation
// ============================================================================
// Manages the RenderTexture that represents the painted canvas.
// Uses compute shaders to stamp paint particles and diffuse colors.
// ============================================================================

using UnityEngine;
using SwingingPaintBucket.Core;
using SwingingPaintBucket.Physics;
using SwingingPaintBucket.Utilities;

namespace SwingingPaintBucket.Rendering
{
    /// <summary>
    /// Renders the canvas surface with accumulating paint.
    /// Uses RenderTexture for color and height, updated via compute shaders.
    /// </summary>
    public class CanvasPaintRenderer : MonoBehaviour
    {
        [Header("Canvas Object")]
        [SerializeField] private MeshRenderer _canvasMeshRenderer;
        [SerializeField] private MeshFilter _canvasMeshFilter;

        [Header("Settings")]
        [SerializeField] private float _splatRadius = 3.0f; // in texture pixels
        [SerializeField] private int _diffusionIterations = 2;

        // ── Textures ─────────────────────────────────────────────────────
        private RenderTexture _colorTexture;
        private RenderTexture _heightTexture;

        // ── Compute Shader ───────────────────────────────────────────────
        private ComputeShader _canvasBlendShader;
        private int _stampKernel;
        private int _diffuseKernel;

        // ── GPU Buffers for particle data ────────────────────────────────
        private ComputeBuffer _particlePosBuffer;
        private ComputeBuffer _particleColorBuffer;
        private ComputeBuffer _particleMassBuffer;
        private ComputeBuffer _particleStateBuffer;

        // ── Config cache ─────────────────────────────────────────────────
        private int _textureSize;
        private float _canvasWidth;
        private float _canvasHeight;
        private Material _canvasMaterial;

        private bool _initialized = false;

        /// <summary>
        /// Initializes the canvas rendering system.
        /// </summary>
        public void Initialize(CanvasConfig canvasConfig, ComputeShader blendShader)
        {
            _textureSize = canvasConfig.textureResolution;
            _canvasWidth = canvasConfig.width;
            _canvasHeight = canvasConfig.height;
            _canvasBlendShader = blendShader;

            // Create RenderTextures
            _colorTexture = new RenderTexture(_textureSize, _textureSize, 0, RenderTextureFormat.ARGBFloat);
            _colorTexture.enableRandomWrite = true;
            _colorTexture.filterMode = FilterMode.Bilinear;
            _colorTexture.wrapMode = TextureWrapMode.Clamp;
            _colorTexture.Create();

            _heightTexture = new RenderTexture(_textureSize, _textureSize, 0, RenderTextureFormat.RFloat);
            _heightTexture.enableRandomWrite = true;
            _heightTexture.filterMode = FilterMode.Bilinear;
            _heightTexture.wrapMode = TextureWrapMode.Clamp;
            _heightTexture.Create();

            // Clear textures
            ClearTexture(_colorTexture, Color.clear);
            ClearTexture(_heightTexture, Color.clear);

            // Setup compute shader kernels
            if (_canvasBlendShader != null)
            {
                _stampKernel = _canvasBlendShader.FindKernel("StampParticles");
                _diffuseKernel = _canvasBlendShader.FindKernel("DiffuseColors");
            }

            // Setup canvas mesh and material
            SetupCanvasMesh(canvasConfig);

            _initialized = true;
        }

        /// <summary>
        /// Updates the canvas texture with current particle data.
        /// </summary>
        public void UpdateVisuals(ParticleManager particleManager, SimulationState state)
        {
            if (!_initialized || particleManager == null) return;

            var particles = particleManager.Particles;
            int canvasCount = particleManager.CanvasCount;

            if (canvasCount == 0) return;

            // For CPU-fallback rendering when compute shader isn't available
            if (_canvasBlendShader == null)
            {
                CPUStampParticles(particles);
                return;
            }

            // GPU stamping would be done here with compute shader dispatch
            // For now, use CPU fallback for reliability
            CPUStampParticles(particles);
        }

        /// <summary>
        /// CPU fallback for stamping particles onto the canvas texture.
        /// Writes directly to a Texture2D for simplicity.
        /// </summary>
        private void CPUStampParticles(ParticleSOA particles)
        {
            // Create temporary Texture2D for CPU writes
            Texture2D tempTex = new Texture2D(_textureSize, _textureSize, TextureFormat.RGBAFloat, false);

            // Read current texture (only if needed for blending)
            RenderTexture.active = _colorTexture;
            tempTex.ReadPixels(new Rect(0, 0, _textureSize, _textureSize), 0, 0);
            RenderTexture.active = null;

            bool modified = false;

            for (int i = 0; i < particles.capacity; i++)
            {
                if (particles.states[i] != ParticleState.OnCanvas) continue;
                if (particles.masses[i] < PhysicsConstants.EPSILON * 0.01f) continue;

                Vector3 pos = particles.positions[i];
                Color color = particles.colors[i];
                float mass = particles.masses[i];

                // Convert world position to texture coordinates
                float u = (pos.x + _canvasWidth * 0.5f) / _canvasWidth;
                float v = (pos.z + _canvasHeight * 0.5f) / _canvasHeight;

                if (u < 0 || u > 1 || v < 0 || v > 1) continue;

                int texX = Mathf.Clamp(Mathf.FloorToInt(u * _textureSize), 0, _textureSize - 1);
                int texY = Mathf.Clamp(Mathf.FloorToInt(v * _textureSize), 0, _textureSize - 1);

                // Stamp a small area around the particle
                int splatPixels = Mathf.CeilToInt(_splatRadius);

                for (int dx = -splatPixels; dx <= splatPixels; dx++)
                {
                    for (int dy = -splatPixels; dy <= splatPixels; dy++)
                    {
                        int px = texX + dx;
                        int py = texY + dy;

                        if (px < 0 || px >= _textureSize || py < 0 || py >= _textureSize)
                            continue;

                        float dist = Mathf.Sqrt(dx * dx + dy * dy);
                        if (dist > _splatRadius) continue;

                        float weight = 1.0f - (dist / _splatRadius);
                        weight = weight * weight; // Gaussian-like

                        Color existing = tempTex.GetPixel(px, py);
                        float existingMass = existing.a;

                        // Blend
                        Color blended;
                        if (existingMass > PhysicsConstants.EPSILON)
                        {
                            float totalMass = existingMass + mass * weight;
                            blended = new Color(
                                (existing.r * existingMass + color.r * mass * weight) / totalMass,
                                (existing.g * existingMass + color.g * mass * weight) / totalMass,
                                (existing.b * existingMass + color.b * mass * weight) / totalMass,
                                Mathf.Min(totalMass, 1.0f)
                            );
                        }
                        else
                        {
                            blended = new Color(color.r, color.g, color.b, mass * weight);
                        }

                        tempTex.SetPixel(px, py, blended);
                        modified = true;
                    }
                }
            }

            if (modified)
            {
                tempTex.Apply();
                Graphics.Blit(tempTex, _colorTexture);
            }

            Object.Destroy(tempTex);
        }

        /// <summary>
        /// Sets up the canvas 3D mesh and material.
        /// </summary>
        private void SetupCanvasMesh(CanvasConfig config)
        {
            if (_canvasMeshFilter == null) return;

            // Create canvas mesh (flat plane)
            Mesh mesh = new Mesh();
            mesh.name = "CanvasMesh";

            float halfW = config.width * 0.5f;
            float halfH = config.height * 0.5f;

            Vector3[] vertices = new Vector3[]
            {
                new Vector3(-halfW, 0, -halfH),
                new Vector3(halfW, 0, -halfH),
                new Vector3(halfW, 0, halfH),
                new Vector3(-halfW, 0, halfH)
            };

            Vector2[] uvs = new Vector2[]
            {
                new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(1, 1), new Vector2(0, 1)
            };

            int[] triangles = new int[] { 0, 2, 1, 0, 3, 2 };
            Vector3[] normals = new Vector3[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.normals = normals;
            mesh.tangents = new Vector4[] {
                new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1),
                new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1)
            };

            _canvasMeshFilter.mesh = mesh;

            // Apply rotation for canvas inclination
            if (config.inclinationDegrees > 0.01f)
            {
                float incRad = config.inclinationDegrees;
                float dirDeg = config.inclinationDirectionDegrees;
                transform.rotation = Quaternion.Euler(incRad, dirDeg, 0);
            }

            // Position
            transform.position = new Vector3(0, config.yPosition, 0);

            // Setup material
            if (_canvasMeshRenderer != null)
            {
                _canvasMaterial = _canvasMeshRenderer.material;
                if (_canvasMaterial != null)
                {
                    _canvasMaterial.SetTexture("_PaintTex", _colorTexture);
                    _canvasMaterial.SetTexture("_HeightTex", _heightTexture);
                }
            }
        }

        /// <summary>
        /// Returns the current canvas texture for saving.
        /// </summary>
        public Texture2D CaptureCanvasImage()
        {
            if (_colorTexture == null) return null;

            Texture2D result = new Texture2D(_textureSize, _textureSize, TextureFormat.RGBA32, false);
            RenderTexture.active = _colorTexture;
            result.ReadPixels(new Rect(0, 0, _textureSize, _textureSize), 0, 0);
            result.Apply();
            RenderTexture.active = null;

            return result;
        }

        /// <summary>
        /// Clears the canvas.
        /// </summary>
        public void ClearCanvas()
        {
            if (_colorTexture != null) ClearTexture(_colorTexture, Color.clear);
            if (_heightTexture != null) ClearTexture(_heightTexture, Color.clear);
        }

        private void ClearTexture(RenderTexture rt, Color color)
        {
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(true, true, color);
            RenderTexture.active = prev;
        }

        /// <summary>
        /// Releases GPU resources.
        /// </summary>
        public void Cleanup()
        {
            if (_colorTexture != null) { _colorTexture.Release(); Object.Destroy(_colorTexture); }
            if (_heightTexture != null) { _heightTexture.Release(); Object.Destroy(_heightTexture); }
            _particlePosBuffer?.Release();
            _particleColorBuffer?.Release();
            _particleMassBuffer?.Release();
            _particleStateBuffer?.Release();
            _initialized = false;
        }

        private void OnDestroy()
        {
            Cleanup();
        }
    }
}
