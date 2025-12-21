using Flocking;
using SpatialPartition;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;


public class FlockingBoids : MonoBehaviour
{
    public Mesh quad;
    [SerializeField] private Material birdMaterial;
    [Range(1, 100000)] public int boidCount;
    [Range(0.0001f, 50f)] public float speed;
    [Range(5, 500f)] public float scale = 15f;

    [Header("Strength")] [Range(0.0f, 10f)]
    public float separationStrength = 0.5f;

    [Range(0.0f, 10f)] public float alignmentStrength = 0.5f;
    [Range(0.0f, 10f)] public float cohesionStrength = 0.5f;

    [Header("Range")] [Range(0.01f, 10f)] public float avoidanceRange = 2f;
    [Range(0.01f, 10f)] public float alignmentRange = 5f;
    [Range(0.01f, 10f)] public float turnFactor = 5f;

    [Range(0.01f, 10f)] public float minSpeed = 0.1f;
    [Range(0.01f, 10f)] public float maxSpeed = 1f;


    public NativeArray<Boid> boidsData;
    private NativeArray<Boid> boidsOut;
    private NativeParallelMultiHashMap<int3, int> spatialHash;

    [Header("Grid")] public float cellSize = 2f;

    public WorldPartition WorldPartition;


    public InstancedRenderBatch Renderer;
    public NativeArray<InstanceData> InstanceData;

    private void Start()
    {
        WorldPartition = new WorldPartition()
        {
            CellSize = cellSize
        };

        boidsData = new NativeArray<Boid>(boidCount, Allocator.Persistent);
        boidsOut = new NativeArray<Boid>(boidCount, Allocator.Persistent);

        spatialHash = new NativeParallelMultiHashMap<int3, int>(boidCount * 2, Allocator.Persistent);

        Camera.main.orthographicSize = scale;
        cameraS = Camera.main.OrthographicBounds().size;
        xRange = cameraS.x * 0.35f;
        yRange = cameraS.y * 0.35f;

        var size = (int2)math.round(new float2(cameraS.x, cameraS.y));
        var width = size.x;
        var height = size.y;
        gridSize = width * height;

        Renderer = InstancedRenderBatch.Default(quad, birdMaterial);
        InstanceData = new NativeArray<InstanceData>(boidCount, Allocator.Persistent);

        for (int i = 0; i < boidsData.Length; i++)
        {
            var randP = new float3(Random.Range(-1f, 1f) * scale, Random.Range(-1f, 1f) * scale, 0);
            var randV = new float3(-Random.Range(-1f, 1f) * scale, Random.Range(-1f, 1f) * scale, 0);
            var objToWord = float4x4.TRS(randP, quaternion.LookRotation(new float3(0f, 0f, 1f), randV),
                new float3(1, 1, 1));
            boidsData[i] = new Boid()
            {
                Velocity = randV,
                objectToWorld = objToWord,
                GroupID = (uint)(i % 2 == 0 ? 0 : 1)
                // GroupID = wordP.x > 0 ? 0u : 1u
            };
            InstanceData[i] = new InstanceData()
            {
                Matrix = objToWord,
                MatrixInverse = math.inverse(objToWord),
                Color = boidsData[i].GroupID == 0 ? Color.darkTurquoise : Color.orangeRed
            };
        }
    }


    public int gridSize;
    public float xRange;
    public float yRange;
    public Vector3 cameraS;

    private void OnDisable()
    {
        if (boidsData.IsCreated) boidsData.Dispose();
        if (boidsOut.IsCreated) boidsOut.Dispose();
        if (spatialHash.IsCreated) spatialHash.Dispose();
        if (InstanceData.IsCreated) InstanceData.Dispose();
        Renderer?.Dispose();
    }


    private void Update()
    {
        var batchCount = (int)boidsData.Length / 24;
        FlockingJobs();
    }


    private bool t = false;

    private void FlockingJobs()
    {
        var batchCount = (int)boidsData.Length / 24;
        spatialHash.Clear();
        var hashJob = new HashPositionsJob()
        {
            boids = boidsData,
            spatialHash = spatialHash.AsParallelWriter(),
            Partition = WorldPartition
        };
        var hashHandle = hashJob.Schedule(boidsData.Length, batchCount);


        var flockingJob = new FlockingJob()
        {
            Partition = WorldPartition,
            AvoidanceRange = avoidanceRange,
            seperationForce = separationStrength,
            AlignmentForce = alignmentStrength,
            AlignmentRange = alignmentRange,
            boundryWeigth = turnFactor,
            CohesionForce = cohesionStrength,
            BoidsDataIn = boidsData,
            BoidsDataOut = boidsOut,
            DeltaTime = Time.deltaTime,
            Speed = speed,
            XRange = xRange,
            YRange = yRange,
            spatialHash = spatialHash,
            CellSize = cellSize, minSpeed = minSpeed, maxSpeed = maxSpeed,

            InstanceData = InstanceData
        };
        var flockingJobHandle = flockingJob.Schedule(boidsData.Length, batchCount, hashHandle);
        var boidsColorJob = new BoidsColorJob()
        {
            BoidsData = boidsOut,
            InstanceData= InstanceData
        };
        var handle = boidsColorJob.Schedule(boidsData.Length, batchCount, flockingJobHandle);
        handle.Complete();
        (boidsData, boidsOut) = (boidsOut, boidsData);

        Renderer.UpdateData(InstanceData);
        Renderer.Draw();
    }
}