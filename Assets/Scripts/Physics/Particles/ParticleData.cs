// ============================================================================
// ParticleData.cs — Particle data structures (Structure-of-Arrays)
// Swinging Paint Bucket Simulation
// ============================================================================
// Uses Structure-of-Arrays (SOA) layout for cache efficiency and GPU compat.
// All particle data is stored in flat arrays for compute shader access.
// ============================================================================

using UnityEngine;
using System;
using SwingingPaintBucket.Core;

namespace SwingingPaintBucket.Physics
{
    /// <summary>
    /// Particle lifecycle states.
    /// </summary>
    public enum ParticleState : byte
    {
        Dead = 0,       // Inactive, can be recycled
        InBucket = 1,   // Inside the bucket (SPH simulation)
        FreeFall = 2,   // Airborne between bucket and canvas
        OnCanvas = 3    // Landed on canvas surface
    }

    /// <summary>
    /// Structure-of-Arrays particle storage for maximum performance.
    /// This layout allows:
    /// - Cache-friendly iteration over single properties
    /// - Direct upload to ComputeBuffers for GPU processing
    /// - Zero GC allocation during simulation
    /// </summary>
    public class ParticleSOA : IDisposable
    {
        // ── Particle Properties ──────────────────────────────────────────
        public Vector3[] positions;
        public Vector3[] velocities;
        public float[] masses;
        public Color[] colors;
        public ParticleState[] states;
        public float[] lifetimes;
        public float[] radii;        // Droplet radius for Weber number
        public int[] colorIndices;   // Which color from the paint palette

        // ── Counts ───────────────────────────────────────────────────────
        public int capacity;
        public int activeCount;

        // ── Free List (pool) ─────────────────────────────────────────────
        private int[] _freeList;
        private int _freeCount;

        // ── Constructor ──────────────────────────────────────────────────

        public ParticleSOA(int capacity)
        {
            this.capacity = capacity;
            this.activeCount = 0;

            positions = new Vector3[capacity];
            velocities = new Vector3[capacity];
            masses = new float[capacity];
            colors = new Color[capacity];
            states = new ParticleState[capacity];
            lifetimes = new float[capacity];
            radii = new float[capacity];
            colorIndices = new int[capacity];

            // Initialize free list (all slots free)
            _freeList = new int[capacity];
            _freeCount = capacity;
            for (int i = 0; i < capacity; i++)
            {
                _freeList[i] = capacity - 1 - i; // Stack order: last-in-first-out
                states[i] = ParticleState.Dead;
            }
        }

        // ── Pool Operations ──────────────────────────────────────────────

        /// <summary>
        /// Acquires a free particle slot. Returns -1 if pool is exhausted.
        /// O(1) operation using free list stack.
        /// </summary>
        public int Acquire()
        {
            if (_freeCount <= 0) return -1;

            _freeCount--;
            int index = _freeList[_freeCount];
            activeCount++;
            return index;
        }

        /// <summary>
        /// Releases a particle back to the pool.
        /// O(1) operation.
        /// </summary>
        public void Release(int index)
        {
            if (index < 0 || index >= capacity) return;
            if (states[index] == ParticleState.Dead) return; // Already released

            states[index] = ParticleState.Dead;
            positions[index] = Vector3.zero;
            velocities[index] = Vector3.zero;
            masses[index] = 0.0f;
            lifetimes[index] = 0.0f;

            _freeList[_freeCount] = index;
            _freeCount++;
            activeCount--;
        }

        /// <summary>
        /// Spawns a new particle with given properties.
        /// Returns the index, or -1 if pool is full.
        /// </summary>
        public int Spawn(Vector3 position, Vector3 velocity, float mass,
            Color color, ParticleState state, float radius, int colorIndex = 0)
        {
            int index = Acquire();
            if (index < 0) return -1;

            positions[index] = position;
            velocities[index] = velocity;
            masses[index] = mass;
            colors[index] = color;
            states[index] = state;
            lifetimes[index] = 0.0f;
            radii[index] = radius;
            colorIndices[index] = colorIndex;

            return index;
        }

        /// <summary>
        /// Returns number of free slots available.
        /// </summary>
        public int FreeCount => _freeCount;

        /// <summary>
        /// Checks if pool has available slots.
        /// </summary>
        public bool HasFreeSlots => _freeCount > 0;

        /// <summary>
        /// Resets all particles to dead state.
        /// </summary>
        public void Reset()
        {
            for (int i = 0; i < capacity; i++)
            {
                states[i] = ParticleState.Dead;
                positions[i] = Vector3.zero;
                velocities[i] = Vector3.zero;
                masses[i] = 0.0f;
                lifetimes[i] = 0.0f;
            }

            _freeCount = capacity;
            for (int i = 0; i < capacity; i++)
            {
                _freeList[i] = capacity - 1 - i;
            }
            activeCount = 0;
        }

        /// <summary>
        /// Disposes all arrays.
        /// </summary>
        public void Dispose()
        {
            positions = null;
            velocities = null;
            masses = null;
            colors = null;
            states = null;
            lifetimes = null;
            radii = null;
            colorIndices = null;
            _freeList = null;
        }
    }

    /// <summary>
    /// Manages all particle storage and provides high-level operations.
    /// Separates particles by state for efficient batch processing.
    /// </summary>
    public class ParticleManager : IDisposable
    {
        private ParticleSOA _particles;

        // ── State-separated indices for batch processing ─────────────────
        // These arrays hold indices of particles in each state.
        // Rebuilt each frame for efficient iteration.
        private int[] _freeFallIndices;
        private int[] _canvasIndices;
        private int[] _bucketIndices;

        public int FreeFallCount { get; private set; }
        public int CanvasCount { get; private set; }
        public int BucketCount { get; private set; }

        public ParticleSOA Particles => _particles;

        public ParticleManager(int maxParticles)
        {
            _particles = new ParticleSOA(maxParticles);
            _freeFallIndices = new int[maxParticles];
            _canvasIndices = new int[maxParticles];
            _bucketIndices = new int[maxParticles];
        }

        /// <summary>
        /// Spawns a free-fall particle (exiting the bucket orifice).
        /// </summary>
        public int SpawnFreeFallParticle(Vector3 position, Vector3 velocity,
            float mass, Color color, float radius, int colorIndex = 0)
        {
            return _particles.Spawn(position, velocity, mass, color,
                ParticleState.FreeFall, radius, colorIndex);
        }

        /// <summary>
        /// Transitions a particle from FreeFall to OnCanvas state.
        /// </summary>
        public void TransitionToCanvas(int index)
        {
            if (index >= 0 && index < _particles.capacity)
                _particles.states[index] = ParticleState.OnCanvas;
        }

        /// <summary>
        /// Transitions an InBucket particle to FreeFall state, updating its properties.
        /// If no InBucket particles exist, spawns a new FreeFall particle.
        /// </summary>
        public int TransitionBucketToFreeFall(Vector3 position, Vector3 velocity, float mass, Color color, float radius, int colorIndex = 0)
        {
            if (BucketCount <= 0)
            {
                return SpawnFreeFallParticle(position, velocity, mass, color, radius, colorIndex);
            }

            // Grab the last one to be fast O(1)
            BucketCount--;
            int idx = _bucketIndices[BucketCount];

            _particles.positions[idx] = position;
            _particles.velocities[idx] = velocity;
            _particles.masses[idx] = mass;
            _particles.colors[idx] = color;
            _particles.radii[idx] = radius;
            _particles.states[idx] = ParticleState.FreeFall;
            _particles.colorIndices[idx] = colorIndex;

            return idx;
        }

        /// <summary>
        /// Kills a particle (returns to pool).
        /// </summary>
        public void KillParticle(int index)
        {
            _particles.Release(index);
        }

        /// <summary>
        /// Rebuilds state-separated index arrays for batch processing.
        /// Call once per physics step before processing each state.
        /// </summary>
        public void RebuildStateIndices()
        {
            FreeFallCount = 0;
            CanvasCount = 0;
            BucketCount = 0;

            for (int i = 0; i < _particles.capacity; i++)
            {
                switch (_particles.states[i])
                {
                    case ParticleState.FreeFall:
                        _freeFallIndices[FreeFallCount++] = i;
                        break;
                    case ParticleState.OnCanvas:
                        _canvasIndices[CanvasCount++] = i;
                        break;
                    case ParticleState.InBucket:
                        _bucketIndices[BucketCount++] = i;
                        break;
                }
            }
        }

        /// <summary>Returns the free-fall particle index array.</summary>
        public int[] GetFreeFallIndices() => _freeFallIndices;

        /// <summary>Returns the canvas particle index array.</summary>
        public int[] GetCanvasIndices() => _canvasIndices;

        /// <summary>Returns the bucket particle index array.</summary>
        public int[] GetBucketIndices() => _bucketIndices;

        /// <summary>
        /// Resets all particle data.
        /// </summary>
        public void Reset()
        {
            _particles.Reset();
            FreeFallCount = 0;
            CanvasCount = 0;
            BucketCount = 0;
        }

        /// <summary>
        /// Initializes the SPH particles inside the bucket based on paint volume.
        /// Particles are placed in a simple grid pattern to match the required volume.
        /// </summary>
        public void InitializeBucketParticles(SimulationConfig config)
        {
            float targetVolume = config.paint.initialVolume;
            if (targetVolume <= SwingingPaintBucket.Utilities.PhysicsConstants.EPSILON) return;

            float bucketRadius = config.bucket.radius;
            float targetHeight = config.paint.GetInitialHeight(bucketRadius);
            
            // Calculate particle mass and spacing
            float particleMass = config.particleMass;
            if (particleMass <= SwingingPaintBucket.Utilities.PhysicsConstants.EPSILON)
            {
                particleMass = config.paint.InitialMass / Mathf.Max(config.maxParticles * 0.1f, 1.0f);
            }
            
            float dropletRadius = Mathf.Pow((3.0f * particleMass) / (4.0f * Mathf.PI * config.paint.density), 1.0f / 3.0f);
            dropletRadius *= 1.5f; // Increase radius slightly for better visual overlap
            
            float spacing = config.paint.smoothingRadius * 0.5f; // Initial spacing based on smoothing radius
            
            int colorIndex = 0;
            // Force red color and full opacity as requested
            Color paintColor = new Color(1.0f, 0.0f, 0.0f, 1.0f);

            // Simple grid fill
            float startY = -config.bucket.height * 0.5f + spacing;
            float endY = startY + targetHeight;

            int spawnCount = 0;
            for (float y = startY; y <= endY; y += spacing)
            {
                for (float x = -bucketRadius; x <= bucketRadius; x += spacing)
                {
                    for (float z = -bucketRadius; z <= bucketRadius; z += spacing)
                    {
                        if (x * x + z * z <= bucketRadius * bucketRadius * 0.9f) // Keep slightly away from walls
                        {
                            if (!Particles.HasFreeSlots) return;

                            Vector3 pos = new Vector3(x, y, z);
                            // Add slight jitter to avoid grid artifacts
                            pos += new Vector3(UnityEngine.Random.Range(-0.1f, 0.1f), UnityEngine.Random.Range(-0.1f, 0.1f), UnityEngine.Random.Range(-0.1f, 0.1f)) * spacing;

                            _particles.Spawn(pos, Vector3.zero, particleMass, paintColor, ParticleState.InBucket, dropletRadius, colorIndex);
                            spawnCount++;
                        }
                    }
                }
            }
            Debug.Log($"[ParticleManager] Initialized {spawnCount} particles in the bucket.");
        }

        public void Dispose()
        {
            _particles?.Dispose();
        }
    }
}
