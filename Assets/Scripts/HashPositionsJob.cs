using Flocking;
using SpatialPartition;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct HashPositionsJob : IJobParallelFor
{
    [ReadOnly] public WorldPartition Partition;
    [ReadOnly] public NativeArray<Boid> boids;
    [WriteOnly] public NativeParallelMultiHashMap<int3, int>.ParallelWriter spatialHash;

    public void Execute(int index)
    {
        var wp = boids[index].objectToWorld.c3.xyz;
        var gridPos = Partition.ToPartition(wp);
        spatialHash.Add(gridPos, index);
    }
}