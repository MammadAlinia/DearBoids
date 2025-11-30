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

    [Header("Grid")] public float cellSize = 2f;


    private void Start()
    {
        UpdateSpatialHash();
        visualData = new NativeArray<Matrix4x4>(birdCount, Allocator.Persistent);
        boids = new NativeArray<Boid>(birdCount, Allocator.Persistent);


        for (int i = 0; i < boids.Length; i++)
        {
            var wordP = new float3(Random.value, Random.value, 0);
            boids[i] = new Boid()
            {
                Position = wordP,
                Velocity = new float3(Random.value, Random.value, 0),
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
        var job = new FlockingJob()
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


        var handle = job.Schedule(boids.Length, 550);
        handle.Complete();
        (boids, boidsOut) = (boidsOut, boids);
        Graphics.DrawMeshInstanced(birdMesh, 0, birdMaterial, visualData.ToList());
    }

    private void OnDrawGizmos()
    {
    
    }
}