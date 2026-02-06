using System;
using DearBoids;
using SpatialPartition;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using Random = UnityEngine.Random;

[Serializable]
public class FlockingSystem : IDisposable
{
    public float cellSize = 1f;

    public WorldPartition WorldPartition;

    public float avoidanceRange;
    public float boidMaxSpeed ;

    public float boidMinSpeed ;

    public float cohesionStrength ;

    public float alignmentRange ;

    public float alignmentStrength ;

    public float avoidanceStrength ;

    public static FlockingSystem New(FlockingSettingData setting, WorldPartition worldPartition)
    {
        var flockingBoids = new FlockingSystem();

        flockingBoids.avoidanceRange = setting.avoidanceRange;
        flockingBoids.avoidanceStrength = setting.avoidanceStrength;
        flockingBoids.alignmentStrength = setting.alignmentStrength;
        flockingBoids.alignmentRange = setting.alignmentRange;
        flockingBoids.cohesionStrength = setting.cohesionStrength;
        flockingBoids.boidMinSpeed = setting.boidMinSpeed;
        flockingBoids.boidMaxSpeed = setting.boidMaxSpeed;
        flockingBoids.WorldPartition = worldPartition;
        return flockingBoids;
    }


    public void Dispose()
    {
    }

    public void UpdateFlocking(ref NativeArray<Boid> boidsData,
        ref NativeArray<InstanceData> instanceData,
        ref NativeParallelMultiHashMap<int3, int> spatialHash)
    {
        var boidsTemp = new NativeArray<Boid>(boidsData.Length, Allocator.TempJob);
        var instancesTemp = new NativeArray<InstanceData>(instanceData.Length, Allocator.TempJob);
        var batchCount =   12;
        spatialHash.Clear();
        var hashJob = new HashPositionsJob()
        {
            instances = instanceData,
            spatialHash = spatialHash.AsParallelWriter(),
            Partition = WorldPartition
        };
        var hashHandle = hashJob.Schedule(boidsData.Length, batchCount);


        var flockingJob = new FlockingJob()
        {
            Partition = WorldPartition,
            InstancesIn = instanceData,
            InstancesOut = instancesTemp,
            AvoidanceRange = avoidanceRange,
            AlignmentRange = alignmentRange,
            AvoidanceStrength = avoidanceStrength,
            AlignmentStrength = alignmentStrength,
            CohesionStrength = cohesionStrength,
            BoidsDataIn = boidsData,
            BoidsDataOut = boidsTemp,
            DeltaTime = Time.deltaTime,
            Speed = 1,
            spatialHash = spatialHash,
            CellSize = cellSize,
            MinSpeed = boidMinSpeed,
            MaxSpeed = boidMaxSpeed,
        };
        var flockingJobHandle = flockingJob.Schedule(boidsData.Length, batchCount, hashHandle);
        flockingJobHandle.Complete();

        NativeArray<Boid>.Copy(boidsTemp, boidsData);
        NativeArray<InstanceData>.Copy(instancesTemp, instanceData);
        boidsTemp.Dispose();
        instancesTemp.Dispose();
    }
}