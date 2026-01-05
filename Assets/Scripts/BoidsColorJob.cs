using DearBoids;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile]
public struct BoidsColorJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<Boid> BoidsData;
    [ReadOnly] public NativeParallelMultiHashMap<int3, int> SpatialHash;
    public NativeArray<InstanceData> InstanceData;
    public SpatialPartition.WorldPartition Partition;

    public void Execute(int i)
    {
        var instanceData = InstanceData[i];


        // Color based on  neighbors count
        var boid = BoidsData[i];

        var position = instanceData.Matrix.c3.xyz;
        var gridPos = Partition.ToPartition(position);

        int neighborCount = 0;

        if (SpatialHash.TryGetFirstValue(gridPos, out int otherIndex, out var iterator))
        {
            do
            {
                if (otherIndex != i)
                {
                    neighborCount++;
                }
            } while (SpatialHash.TryGetNextValue(out otherIndex, ref iterator));
        }

        var maxNeighbors = 10;
        float lerpedValue = math.clamp(neighborCount, 0f, maxNeighbors);
        instanceData.Color = Color.Lerp(Color.teal, Color.darkRed, lerpedValue / maxNeighbors);

        InstanceData[i] = instanceData;
    }
}