using System;
using System.Collections.Generic;
using System.Linq;
using Flocking;
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
    [Range(1, 100000)] public int boidCount;
    [Range(0.0001f, 50f)] public float speed;
    [Range(5, 100f)] public float scale = 15f;

    [Header("Strength")] [Range(0.0f, 10f)]
    public float separationStrength = 0.5f;

    [Range(0.0f, 1f)] public float alignmentStrength = 0.5f;
    [Range(0.0f, 1f)] public float cohesionStrength = 0.5f;

    [Header("Range")] [Range(0.01f, 10f)] public float avoidanceRange = 2f;
    [Range(0.01f, 10f)] public float alignmentRange = 5f;
    [Range(0.01f, 10f)] public float turnFactor = 5f;


    public NativeArray<Boid> boids;
    private NativeArray<Boid> boidsOut;
    public NativeArray<Matrix4x4> visualData;
    private NativeParallelMultiHashMap<int, int> spatialHash;
    [Header("Grid")] public float cellSize = 2f;


    private void Start()
    {
        UpdateSpatialHash();
        visualData = new NativeArray<Matrix4x4>(boidCount, Allocator.Persistent);
        boids = new NativeArray<Boid>(boidCount, Allocator.Persistent);
        boidsOut = new NativeArray<Boid>(boidCount, Allocator.Persistent);
        spatialHash = new NativeParallelMultiHashMap<int, int>(boidCount * 2, Allocator.Persistent);


        for (int i = 0; i < boids.Length; i++)
        {
            var wordP = new float3(Random.value*scale, Random.value*scale, 0);
            boids[i] = new Boid()
            {
                Position = wordP,
                Velocity = new float3(Random.value*scale, Random.value*scale, 0),
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
        if (boids.IsCreated) boids.Dispose();
        if (boidsOut.IsCreated) boidsOut.Dispose();
        if (spatialHash.IsCreated) spatialHash.Dispose();
        if (visualData.IsCreated) visualData.Dispose();
    }

    private void Update()
    {
        //  FlockingSingleThread();
        FlockingJobs();
        Graphics.RenderMeshInstanced(new RenderParams(birdMaterial) { }, birdMesh, 0, visualData);
    }

    private void UpdateSpatialHash()
    {
    }


    private void FlockingJobs()
    {
        float cellSize = math.min(avoidanceRange, alignmentRange);
        spatialHash.Clear();
        var hashJob = new HashPositionsJob()
        {
            boids = boids,
            spatialHash = spatialHash.AsParallelWriter(),
            cellSize = cellSize
        };
        var hashHandle = hashJob.Schedule(boids.Length, 550);
        var job = new FlockingJob()
        {
            AvoidanceRange = avoidanceRange,
            AvoidanceForce = separationStrength,
            AlignmentForce = alignmentStrength,
            AlignmentRange = alignmentRange,
            TurnFactor = turnFactor,
            CohesionForce = cohesionStrength,
            BoidsDataIn = boids,
            BoidsDataOut = boidsOut,
            DeltaTime = Time.deltaTime,
            Speed = speed,
            VisualData = visualData,
            XRange = xRange,
            YRange = yRange,
            spatialHash = spatialHash,
            CellSize = cellSize
        };


        var handle = job.Schedule(boids.Length, 550, hashHandle);
        handle.Complete();
        (boids, boidsOut) = (boidsOut, boids);
    }


    private void OnDrawGizmos()
    {
    }
}