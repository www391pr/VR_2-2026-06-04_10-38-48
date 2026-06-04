// ============================================================================
// SPHFluidSystem.cs — GPU-based SPH simulation for InBucket particles
// Swinging Paint Bucket Simulation
// ============================================================================
// Orchestrates compute shaders for spatial hashing and fluid dynamics.
// Synchronizes data between CPU ParticleManager and GPU ComputeBuffers.
// ============================================================================

using UnityEngine;
using SwingingPaintBucket.Core;
using SwingingPaintBucket.Physics;

namespace SwingingPaintBucket.Physics.Fluid
{
    public class SPHFluidSystem : System.IDisposable
    {
        private ComputeShader _sphCompute;
        private ComputeShader _spatialHashCompute;
        
        private ComputeBuffer _positionsBuffer;
        private ComputeBuffer _velocitiesBuffer;
        private ComputeBuffer _densitiesBuffer;
        private ComputeBuffer _pressuresBuffer;
        private ComputeBuffer _forcesBuffer;
        private ComputeBuffer _massesBuffer;
        private ComputeBuffer _statesBuffer;
        
        private ComputeBuffer _cellStartBuffer;
        private ComputeBuffer _cellEndBuffer;
        private ComputeBuffer _sortedIndicesBuffer;
        
        private int _maxBucketParticles;
        private int _gridSize;
        private const int THREAD_GROUP_SIZE = 256;
        private const int MAX_PARTICLES_PER_CELL = 64;
        
        // Temp arrays for packing/unpacking CPU data
        private Vector3[] _tempPositions;
        private Vector3[] _tempVelocities;
        private float[] _tempMasses;
        private int[] _tempStates;
        
        public SPHFluidSystem(ComputeShader sphCompute, ComputeShader spatialHashCompute, int maxBucketParticles)
        {
            _sphCompute = sphCompute;
            _spatialHashCompute = spatialHashCompute;
            _maxBucketParticles = maxBucketParticles;
            
            // Allocate buffers
            _positionsBuffer = new ComputeBuffer(maxBucketParticles, sizeof(float) * 3);
            _velocitiesBuffer = new ComputeBuffer(maxBucketParticles, sizeof(float) * 3);
            _densitiesBuffer = new ComputeBuffer(maxBucketParticles, sizeof(float));
            _pressuresBuffer = new ComputeBuffer(maxBucketParticles, sizeof(float));
            _forcesBuffer = new ComputeBuffer(maxBucketParticles, sizeof(float) * 3);
            _massesBuffer = new ComputeBuffer(maxBucketParticles, sizeof(float));
            _statesBuffer = new ComputeBuffer(maxBucketParticles, sizeof(int));
            
            // Grid size based on max particles (prime number helps reduce collisions)
            _gridSize = 100003; 
            
            _cellStartBuffer = new ComputeBuffer(_gridSize, sizeof(int));
            _cellEndBuffer = new ComputeBuffer(_gridSize, sizeof(int));
            _sortedIndicesBuffer = new ComputeBuffer(_gridSize * MAX_PARTICLES_PER_CELL, sizeof(int));
            
            _tempPositions = new Vector3[maxBucketParticles];
            _tempVelocities = new Vector3[maxBucketParticles];
            _tempMasses = new float[maxBucketParticles];
            _tempStates = new int[maxBucketParticles];
            
            BindBuffers();
        }
        
        private void BindBuffers()
        {
            if (_spatialHashCompute != null)
            {
                int clearKernel = _spatialHashCompute.FindKernel("ClearGrid");
                int hashKernel = _spatialHashCompute.FindKernel("HashParticles");
                
                _spatialHashCompute.SetBuffer(clearKernel, "_CellStart", _cellStartBuffer);
                _spatialHashCompute.SetBuffer(clearKernel, "_CellEnd", _cellEndBuffer);
                
                _spatialHashCompute.SetBuffer(hashKernel, "_CellEnd", _cellEndBuffer);
                _spatialHashCompute.SetBuffer(hashKernel, "_SortedIndices", _sortedIndicesBuffer);
                _spatialHashCompute.SetBuffer(hashKernel, "_Positions", _positionsBuffer);
                _spatialHashCompute.SetBuffer(hashKernel, "_ParticleStates", _statesBuffer);
            }
            
            if (_sphCompute != null)
            {
                int densityKernel = _sphCompute.FindKernel("ComputeDensityPressure");
                int forceKernel = _sphCompute.FindKernel("ComputeForces");
                int integrateKernel = _sphCompute.FindKernel("Integrate");
                
                int[] kernels = { densityKernel, forceKernel, integrateKernel };
                foreach (int k in kernels)
                {
                    _sphCompute.SetBuffer(k, "_Positions", _positionsBuffer);
                    _sphCompute.SetBuffer(k, "_Velocities", _velocitiesBuffer);
                    _sphCompute.SetBuffer(k, "_Densities", _densitiesBuffer);
                    _sphCompute.SetBuffer(k, "_Pressures", _pressuresBuffer);
                    _sphCompute.SetBuffer(k, "_Forces", _forcesBuffer);
                    _sphCompute.SetBuffer(k, "_Masses", _massesBuffer);
                    _sphCompute.SetBuffer(k, "_ParticleStates", _statesBuffer);
                    
                    _sphCompute.SetBuffer(k, "_CellStart", _cellStartBuffer);
                    _sphCompute.SetBuffer(k, "_CellEnd", _cellEndBuffer);
                    _sphCompute.SetBuffer(k, "_SortedIndices", _sortedIndicesBuffer);
                }
            }
        }
        
        public void UpdatePhysics(float dt, SimulationState state, SimulationConfig config, ParticleManager pm)
        {
            int bucketCount = pm.BucketCount;
            if (bucketCount <= 0 || _sphCompute == null || _spatialHashCompute == null) return;
            
            // Limit to max buffer size
            int count = Mathf.Min(bucketCount, _maxBucketParticles);
            int[] indices = pm.GetBucketIndices();
            var soa = pm.Particles;
            
            Vector3 bucketPos = state.bucketPosition;
            Quaternion bucketRot = state.bucketRotation;
            Quaternion invRot = Quaternion.Inverse(bucketRot);
            Vector3 bucketVel = state.bucketVelocity;
            
            // Pack data and transform to local space
            for (int i = 0; i < count; i++)
            {
                int idx = indices[i];
                Vector3 worldPos = soa.positions[idx];
                Vector3 worldVel = soa.velocities[idx];
                
                _tempPositions[i] = invRot * (worldPos - bucketPos);
                _tempVelocities[i] = invRot * (worldVel - bucketVel);
                _tempMasses[i] = soa.masses[idx];
                _tempStates[i] = 1; // 1 = InBucket
            }
            
            // Upload to GPU
            _positionsBuffer.SetData(_tempPositions, 0, 0, count);
            _velocitiesBuffer.SetData(_tempVelocities, 0, 0, count);
            _massesBuffer.SetData(_tempMasses, 0, 0, count);
            _statesBuffer.SetData(_tempStates, 0, 0, count);
            
            // Set Uniforms
            SetUniforms(dt, state, config, count);
            
            // Dispatch Grid Clear
            int groupsGrid = Mathf.CeilToInt((float)_gridSize / THREAD_GROUP_SIZE);
            _spatialHashCompute.Dispatch(_spatialHashCompute.FindKernel("ClearGrid"), groupsGrid, 1, 1);
            
            // Dispatch Hash
            int groupsParticles = Mathf.CeilToInt((float)count / THREAD_GROUP_SIZE);
            _spatialHashCompute.Dispatch(_spatialHashCompute.FindKernel("HashParticles"), groupsParticles, 1, 1);
            
            // Dispatch SPH Physics
            _sphCompute.Dispatch(_sphCompute.FindKernel("ComputeDensityPressure"), groupsParticles, 1, 1);
            _sphCompute.Dispatch(_sphCompute.FindKernel("ComputeForces"), groupsParticles, 1, 1);
            _sphCompute.Dispatch(_sphCompute.FindKernel("Integrate"), groupsParticles, 1, 1);
            
            // Download results
            _positionsBuffer.GetData(_tempPositions, 0, 0, count);
            _velocitiesBuffer.GetData(_tempVelocities, 0, 0, count);
            
            // Unpack to CPU arrays and transform back to world space
            for (int i = 0; i < count; i++)
            {
                int idx = indices[i];
                
                Vector3 localPos = _tempPositions[i];
                Vector3 localVel = _tempVelocities[i];
                
                soa.positions[idx] = bucketPos + bucketRot * localPos;
                soa.velocities[idx] = bucketVel + bucketRot * localVel;
            }
        }
        
        private void SetUniforms(float dt, SimulationState state, SimulationConfig config, int count)
        {
            float h = config.paint.smoothingRadius;
            
            // Spatial Hash
            _spatialHashCompute.SetFloat("_CellSize", h);
            _spatialHashCompute.SetInt("_GridSize", _gridSize);
            _spatialHashCompute.SetInt("_ParticleCount", count);
            
            // SPH Parameters
            _sphCompute.SetFloat("_SmoothingRadius", h);
            _sphCompute.SetFloat("_SmoothingRadiusSqr", h * h);
            _sphCompute.SetFloat("_RestDensity", config.paint.restDensity);
            _sphCompute.SetFloat("_Stiffness", config.paint.sphStiffness);
            _sphCompute.SetFloat("_Viscosity", config.GetEffectiveViscosity());
            _sphCompute.SetFloat("_DeltaTime", dt);
            _sphCompute.SetFloat("_Gravity", config.environment.gravity);
            
            // Frame of reference: SPH is simulated in the non-inertial bucket frame
            _sphCompute.SetVector("_BucketAcceleration", state.bucketAcceleration);
            _sphCompute.SetVector("_BucketAngularVelocity", state.bucketAngularVelocity);
            
            _sphCompute.SetInt("_ParticleCount", count);
            _sphCompute.SetInt("_GridSize", _gridSize);
            _sphCompute.SetFloat("_CellSize", h);
        }
        
        public void Dispose()
        {
            _positionsBuffer?.Release();
            _velocitiesBuffer?.Release();
            _densitiesBuffer?.Release();
            _pressuresBuffer?.Release();
            _forcesBuffer?.Release();
            _massesBuffer?.Release();
            _statesBuffer?.Release();
            
            _cellStartBuffer?.Release();
            _cellEndBuffer?.Release();
            _sortedIndicesBuffer?.Release();
        }
    }
}
