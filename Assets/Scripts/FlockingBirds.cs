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
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;


public class FlockingBirds : MonoBehaviour
{
    public Mesh birdMesh;
    [SerializeField] private Material birdMaterial;
    [Range(1, 100000)] public int boidCount;
    [Range(0.0001f, 50f)] public float speed;
    [Range(5, 100f)] public float scale = 15f;

    [Header("Strength")] [Range(0.0f, 10f)]
    public float separationStrength = 0.5f;

    [Range(0.0f, 10f)] public float alignmentStrength = 0.5f;
    [Range(0.0f, 10f)] public float cohesionStrength = 0.5f;

    [Header("Range")] [Range(0.01f, 10f)] public float avoidanceRange = 2f;
    [Range(0.01f, 10f)] public float alignmentRange = 5f;
    [Range(0.01f, 10f)] public float turnFactor = 5f;


    public NativeArray<Boid> boidsData;
    private NativeArray<Boid> boidsOut;
    public NativeArray<Matrix4x4> visualData;

    private NativeParallelMultiHashMap<int, int> spatialHash;
    private NativeArray<uint> boidsInRangeCount;

    public NativeArray<Color> boidsColor;
    private ComputeBuffer colorBuffer;
    private MaterialPropertyBlock _propertyBlock;
    private static readonly int colorPropertyID = Shader.PropertyToID("_InstanceColorBuffer");
    [Header("Grid")] public float cellSize = 2f;


    private void Start()
    {
        UpdateSpatialHash();
        visualData = new NativeArray<Matrix4x4>(boidCount, Allocator.Persistent);
        boidsColor = new NativeArray<Color>(boidCount, Allocator.Persistent);
        boidsData = new NativeArray<Boid>(boidCount, Allocator.Persistent);
        boidsOut = new NativeArray<Boid>(boidCount, Allocator.Persistent);

        spatialHash = new NativeParallelMultiHashMap<int, int>(boidCount * 2, Allocator.Persistent);
        boidsInRangeCount = new NativeArray<uint>(boidCount, Allocator.Persistent);

        colorBuffer = new ComputeBuffer(boidsData.Length, sizeof(float) * 4);
        _propertyBlock = new MaterialPropertyBlock();

        for (int i = 0; i < boidsData.Length; i++)
        {
            var wordP = new float3(Random.Range(-1f,1f) * scale, Random.Range(-1f,1f) * scale, 0);
            boidsData[i] = new Boid()
            {
                Position = wordP,
                Velocity = new float3(-Random.Range(-1f,1f) * scale, Random.Range(-1f,1f)* scale, 0),
               // GroupID = (uint)(i % 2 == 0 ? 0 : 1)
               GroupID = (uint)Random.Range(0,3)
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
        if (boidsData.IsCreated) boidsData.Dispose();
        if (boidsOut.IsCreated) boidsOut.Dispose();
        if (spatialHash.IsCreated) spatialHash.Dispose();
        if (visualData.IsCreated) visualData.Dispose();
        if (boidsColor.IsCreated) boidsColor.Dispose();
        if (boidsInRangeCount.IsCreated) boidsInRangeCount.Dispose();
        colorBuffer?.Release();
    }

    private void Update()
    {
        //  FlockingSingleThread();
        FlockingJobs();
    }

    private void UpdateSpatialHash()
    {
    }

    public Color[] boidsColorTmp;
    public uint[] boidsCountTmp;

    private void FlockingJobs()
    {
        float cellSize = math.max(avoidanceRange, alignmentRange);
        spatialHash.Clear();
        var hashJob = new HashPositionsJob()
        {
            boids = boidsData,
            spatialHash = spatialHash.AsParallelWriter(),
            cellSize = cellSize
        };
        var hashHandle = hashJob.Schedule(boidsData.Length, 550);


        var flockingJob = new FlockingJob()
        {
            AvoidanceRange = avoidanceRange,
            AvoidanceForce = separationStrength,
            AlignmentForce = alignmentStrength,
            AlignmentRange = alignmentRange,
            TurnFactor = turnFactor,
            CohesionForce = cohesionStrength,
            BoidsDataIn = boidsData,
            BoidsDataOut = boidsOut,
            DeltaTime = Time.deltaTime,
            Speed = speed,
            VisualData = visualData,
            XRange = xRange,
            YRange = yRange,
            spatialHash = spatialHash,
            CellSize = cellSize,
            boidsInRangeCount = boidsInRangeCount
        };
        var flockingJobHandle = flockingJob.Schedule(boidsData.Length, 550, hashHandle);
        var boidsColorJob = new BoidsColorJob()
        {
            boidsColor = boidsColor,
            BoidsData = boidsData
        };
        var handle = boidsColorJob.Schedule(boidsData.Length, 550, flockingJobHandle);
        handle.Complete();
        (boidsData, boidsOut) = (boidsOut, boidsData);
        colorBuffer.SetData(boidsColor);
        _propertyBlock.SetBuffer(colorPropertyID, colorBuffer);

        Graphics.RenderMeshInstanced(new RenderParams(mat: birdMaterial)
            {
                matProps = _propertyBlock
            }
            , birdMesh, 0, visualData);
    }
}

[BurstCompile]
public struct BoidsColorJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<Boid> BoidsData;
    public NativeArray<Color> boidsColor;

    public void Execute(int i)
    {
        if (BoidsData[i].GroupID == 0)
            boidsColor[i] = Color.teal;
        if (BoidsData[i].GroupID == 1)
            boidsColor[i] = Color.forestGreen;
        if (BoidsData[i].GroupID == 2)
            boidsColor[i] = Color.paleVioletRed;
        if (BoidsData[i].GroupID == 3)
            boidsColor[i] = Color.red;
    }
}