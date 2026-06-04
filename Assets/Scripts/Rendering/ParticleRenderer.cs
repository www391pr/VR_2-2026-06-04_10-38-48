// ============================================================================
// ParticleRenderer.cs — GPU-instanced particle rendering
// Swinging Paint Bucket Simulation
// ============================================================================
// Renders up to 1M particles using DrawMeshInstancedIndirect.
// Uploads particle data to GPU buffers for instanced drawing.
// ============================================================================

using UnityEngine;
using SwingingPaintBucket.Physics;

namespace SwingingPaintBucket.Rendering
{
    /// <summary>
    /// Renders paint particles using GPU instancing (DrawMeshInstancedIndirect).
    /// Supports rendering 1M+ particles with minimal draw calls.
    /// </summary>
    public class ParticleRenderer : MonoBehaviour
    {
        [Header("Rendering")]
        [SerializeField] private Material _particleMaterial;
        [SerializeField] private Mesh _particleMesh;

        [Header("Settings")]
        [SerializeField] private float _baseParticleSize = 0.005f;
        [SerializeField] private float _alphaMultiplier = 1.0f;

        // ── GPU Buffers ──────────────────────────────────────────────────
        private ComputeBuffer _positionBuffer;
        private ComputeBuffer _colorBuffer;
        private ComputeBuffer _radiusBuffer;
        private ComputeBuffer _stateBuffer;
        private ComputeBuffer _argsBuffer;

        private int _maxParticles;
        private bool _initialized = false;

        // Temp arrays for upload
        private Vector4[] _colorArray;
        private float[] _radiusArray;
        private int[] _stateArray;

        /// <summary>
        /// Initializes GPU buffers for particle rendering.
        /// </summary>
        public void Initialize(int maxParticles)
        {
            _maxParticles = maxParticles;

            // Create default quad mesh if none assigned
            if (_particleMesh == null)
                _particleMesh = CreateQuadMesh();

            // Create GPU buffers
            _positionBuffer = new ComputeBuffer(maxParticles, sizeof(float) * 3);
            _colorBuffer = new ComputeBuffer(maxParticles, sizeof(float) * 4);
            _radiusBuffer = new ComputeBuffer(maxParticles, sizeof(float));
            _stateBuffer = new ComputeBuffer(maxParticles, sizeof(int));

            // Args buffer for DrawMeshInstancedIndirect
            // [indexCount, instanceCount, indexStart, baseVertex, startInstance]
            _argsBuffer = new ComputeBuffer(1, sizeof(uint) * 5, ComputeBufferType.IndirectArguments);

            // Allocate temp arrays
            _colorArray = new Vector4[maxParticles];
            _radiusArray = new float[maxParticles];
            _stateArray = new int[maxParticles];

            // Set material properties
            if (_particleMaterial != null)
            {
                _particleMaterial.SetBuffer("_ParticlePositions", _positionBuffer);
                _particleMaterial.SetBuffer("_ParticleColors", _colorBuffer);
                _particleMaterial.SetBuffer("_ParticleRadii", _radiusBuffer);
                _particleMaterial.SetBuffer("_ParticleStates", _stateBuffer);
                _particleMaterial.SetFloat("_BaseSize", _baseParticleSize);
                _particleMaterial.SetFloat("_AlphaMultiplier", _alphaMultiplier);
            }

            _initialized = true;
        }

        /// <summary>
        /// Updates particle data and renders.
        /// </summary>
        public void UpdateVisuals(ParticleManager particleManager)
        {
            if (!_initialized || particleManager == null) return;
            if (_particleMaterial == null) return;

            var particles = particleManager.Particles;
            int count = Mathf.Min(particles.capacity, _maxParticles);

            // Upload position data
            _positionBuffer.SetData(particles.positions, 0, 0, count);

            // Convert Color to Vector4 and upload
            for (int i = 0; i < count; i++)
            {
                Color c = particles.colors[i];
                _colorArray[i] = new Vector4(c.r, c.g, c.b, c.a);
                _radiusArray[i] = particles.radii[i];
                _stateArray[i] = (int)particles.states[i];
            }

            _colorBuffer.SetData(_colorArray, 0, 0, count);
            _radiusBuffer.SetData(_radiusArray, 0, 0, count);
            _stateBuffer.SetData(_stateArray, 0, 0, count);

            // Set args
            uint[] args = new uint[5];
            args[0] = (uint)_particleMesh.GetIndexCount(0);
            args[1] = (uint)particles.capacity; // Use capacity to ensure fragmented active particles are drawn
            args[2] = (uint)_particleMesh.GetIndexStart(0);
            args[3] = (uint)_particleMesh.GetBaseVertex(0);
            args[4] = 0;
            _argsBuffer.SetData(args);

            // Draw
            Bounds bounds = new Bounds(Vector3.zero, Vector3.one * 100.0f);
            Graphics.DrawMeshInstancedIndirect(
                _particleMesh, 0, _particleMaterial,
                bounds, _argsBuffer
            );
        }

        /// <summary>
        /// Releases GPU buffers.
        /// </summary>
        public void Cleanup()
        {
            _positionBuffer?.Release();
            _colorBuffer?.Release();
            _radiusBuffer?.Release();
            _stateBuffer?.Release();
            _argsBuffer?.Release();
            _initialized = false;
        }

        private void OnDestroy()
        {
            Cleanup();
        }

        /// <summary>
        /// Creates a simple quad mesh for particle billboards.
        /// </summary>
        private Mesh CreateQuadMesh()
        {
            Mesh mesh = new Mesh();
            mesh.name = "ParticleQuad";

            Vector3[] vertices = new Vector3[]
            {
                new Vector3(-0.5f, -0.5f, 0),
                new Vector3(0.5f, -0.5f, 0),
                new Vector3(0.5f, 0.5f, 0),
                new Vector3(-0.5f, 0.5f, 0)
            };

            Vector2[] uvs = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(1, 1),
                new Vector2(0, 1)
            };

            int[] triangles = new int[] { 0, 2, 1, 0, 3, 2 };

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }
    }
}
