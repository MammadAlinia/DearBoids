using Flocking;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

[BurstCompile]
public struct BoidsColorJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<Boid> BoidsData;
    public NativeArray<InstanceData> InstanceData;

    public void Execute(int i)
    {
        var instanceData = InstanceData[i];


        InstanceData[i] = instanceData;
    }
}