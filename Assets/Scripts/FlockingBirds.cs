using System;
using System.Collections.Generic;
using System.Linq;
using Flocking;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;


public class FlockingBirds : MonoBehaviour
{
    public Mesh birdMesh;
    [SerializeField] private Material birdMaterial;
    [Range(1, 100000)] public int birdCount;
    [Range(0.0001f, 50f)] public float speed;
    [Range(5, 100f)] public float scale = 15f;

    [Header("Strength")] [Range(0.0f, 10f)]
    public float separationStrength = 0.5f;

    [Range(0.0f, 1f)] public float alignmentStrength = 0.5f;
    [Range(0.0f, 1f)] public float cohesionStrength = 0.5f;

    [Header("Range")] [Range(0.01f, 10f)] public float separationRange = 2f;
    [Range(0.01f, 10f)] public float visibleRange = 5f;
    [Range(0.01f, 10f)] public float turnFactor = 5f;


    public NativeArray<Boid> boids;
    private NativeArray<Boid> boidsOut;
    public NativeArray<Matrix4x4> visualData;
    public NativeArray<HashAndIndex> hashAndIndex;
    [Header("Grid")] public float cellSize = 2f;


    private void Start()
    {
        UpdateSpatialHash();
        visualData = new NativeArray<Matrix4x4>(birdCount, Allocator.Persistent);
        boids = new NativeArray<Boid>(birdCount, Allocator.Persistent);
        boidsOut = new NativeArray<Boid>(birdCount, Allocator.Persistent);


        for (int i = 0; i < boids.Length; i++)
        {
            var wordP = new float3(Random.value, Random.value, 0);
            boids[i] = new Boid()
            {
                Position = wordP,
                Velocity = new float3(Random.value, Random.value, 0),
            };
            boidsOut[i] = new Boid()
            {
                Position = boids[i].Position,
                Velocity = boids[i].Velocity,
            };
        }


        Camera.main.orthographicSize = scale;
        cameraS = Camera.main.OrthographicBounds().size * 0.35f;
        xRange = cameraS.x;
        yRange = cameraS.y;
    }

    float xRange;
    float yRange;
    private Vector3 cameraS;

    private void OnDisable()
    {
        boids.Dispose();
        boidsOut.Dispose();
        visualData.Dispose();
        // hashAndIndex.Dispose();
    }

    private void Update()
    {
        //  FlockingSingleThread();
        FlockingJobs();
    }

    private void UpdateSpatialHash()
    {
    }


    private void FlockingJobs()
    {
        hashAndIndex = new NativeArray<HashAndIndex>(boids.Length, Allocator.Persistent);

        var hashJob = new HashCellJob()
        {
            boids = boids,
            cellSize = cellSize,
            hashAndIndices = hashAndIndex
        };
        var hashHandle = hashJob.Schedule(boids.Length, 64);

        var sortJob = new SortHashJob()
        {
            hashAndIndices = hashAndIndex
        };
        var sortHandle = sortJob.Schedule(hashHandle);
        var neighborMap = new NativeParallelMultiHashMap<int, int>(boids.Length * 5, Allocator.Persistent);
        var findNeighborsJob = new FindNeighborsJob()
        {
            boids = boids,
            sortedHashList = hashAndIndex,
            neighbors = neighborMap.AsParallelWriter(),
            cellSize = cellSize,
            searchRadius = math.max(separationRange, visibleRange)
        };
        var findNeighborsHandle = findNeighborsJob.Schedule(boids.Length, 64, sortHandle);
        var flockingJob = new FlockingJob()
        {
            AvoidanceRange = separationRange,
            AvoidanceForce = separationStrength,
            AlignmentForce = alignmentStrength,
            AlignmentRange = visibleRange,
            TurnFactor = turnFactor,
            CohesionForce = cohesionStrength,
            BoidsDataIn = boids,
            BoidsDataOut = boidsOut,
            DeltaTime = Time.deltaTime,
            Speed = speed,
            VisualData = visualData,
            XRange = xRange,
            YRange = yRange
        };
        var flockingJobHandle = flockingJob.Schedule(boids.Length, 64, findNeighborsHandle);
        flockingJobHandle.Complete();
        neighborMap.Dispose();
        hashAndIndex.Dispose();
        (boids, boidsOut) = (boidsOut, boids);
        Graphics.DrawMeshInstanced(birdMesh, 0, birdMaterial, visualData.ToList());
    }

    [BurstCompile]
    public struct HashAndIndex : IComparable<HashAndIndex>
    {
        public int Hash;
        public int Index;

        public int CompareTo(HashAndIndex other)
        {
            return Hash.CompareTo(other.Hash);
        }
    }

    static int Hash(int3 gridPos)
    {
        unchecked
        {
            return gridPos.x * 73856093 ^ gridPos.y * 19349663 ^ gridPos.z * 83492791;
        }
    }

    [BurstCompile]
    public struct HashCellJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Boid> boids;
        public NativeArray<HashAndIndex> hashAndIndices;

        public float cellSize;

        public void Execute(int index)
        {
            var boid = boids[index];
            var hash = Hash(GridPos(boid.Position, cellSize));
            hashAndIndices[index] = new HashAndIndex()
            {
                Hash = hash,
                Index = index
            };
        }

        private static int3 GridPos(float3 position, float size)
        {
            return new int3(
                (int)math.floor(position.x / size),
                (int)math.floor(position.y / size),
                (int)math.floor(position.z / size)
            );
        }
    }

    [BurstCompile]
    public struct SortHashJob : IJob
    {
        public NativeArray<HashAndIndex> hashAndIndices;

        public void Execute()
        {
            hashAndIndices.Sort();
        }
    }

    [BurstCompile]
    public struct FindNeighborsJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Boid> boids;
        [ReadOnly] public NativeArray<HashAndIndex> sortedHashList;


        public NativeParallelMultiHashMap<int, int>.ParallelWriter neighbors;

        public float cellSize;
        public float searchRadius;

        public void Execute(int index)
        {
            float3 pos = boids[index].Position;
            int3 baseCell = GridPos(pos, cellSize);

            // Check all 9 surrounding grid cells
            for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
            {
                int3 neighCell = baseCell + new int3(x, y, 0);
                int hash = Hash(neighCell);

                // find hash in sorted list with binary search
                int left = 0;
                int right = sortedHashList.Length - 1;

                while (left < right)
                {
                    var mid = (left + right) / 2;
                    if (sortedHashList[mid].Hash < hash)
                        left = mid + 1;
                    else
                        right = mid;

                    if (sortedHashList[mid].Hash == hash) // found a match
                    {
                        // check for multiple items with the same hash from start to end of the sorted array
                        var startIndex = mid;

                        while (startIndex > 0 && sortedHashList[startIndex - 1].Hash == hash)
                        {
                            startIndex--;
                        }

                        var endIndex = mid;

                        while (endIndex < sortedHashList.Length - 1 && sortedHashList[endIndex + 1].Hash == hash)
                        {
                            endIndex++;
                        }


                        // add all boids in this cell to neighbors if within search radius
                        for (int i = startIndex; i <= endIndex; i++)
                        {
                            int boidIndex = sortedHashList[i].Index;

                            if (boidIndex != index)
                            {

                                neighbors.Add(index, boidIndex);
                            }
                        }

                        break;
                    }
                }
            }
        }

        private static int3 GridPos(float3 position, float size)
        {
            return new int3(
                (int)math.floor(position.x / size),
                (int)math.floor(position.y / size),
                (int)math.floor(position.z / size)
            );
        }

        private static int Hash(int3 gridPos)
        {
            unchecked
            {
                return gridPos.x * 73856093
                       ^ gridPos.y * 19349663
                       ^ gridPos.z * 83492791;
            }
        }
    }

    private void OnDrawGizmos()
    {
        var sci = Mathf.FloorToInt(scale);

        for (int x = -sci; x < sci; x++)
        {
            for (int y = -sci; y < sci; y++)
            {
                var mousePos = Mouse.current.position.ReadValue();
                var worldPos = Camera.main.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, 0));
                var xp = (int)math.floor(worldPos.x / cellSize);
                var yp = (int)math.floor(worldPos.y / cellSize);
                var cx = x * cellSize + cellSize / 2;
                var cy = y * cellSize + cellSize / 2;

                if (xp == x && yp == y)
                {
                    Gizmos.DrawCube(new Vector3(cx, cy, 0), new Vector3(cellSize, cellSize, 0));
                }
                else
                {
                    Gizmos.DrawWireCube(new Vector3(cx, cy, 0), new Vector3(cellSize, cellSize, 0));
                }
            }
        }
    }
}