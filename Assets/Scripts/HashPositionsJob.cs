using Flocking;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct HashPositionsJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<Boid> boids;
    [WriteOnly] public NativeParallelMultiHashMap<int, int>.ParallelWriter spatialHash;
    public float cellSize;

    public void Execute(int index)
    {
        var gridPos = GetGridPosition(boids[index].Position, cellSize);
        var hash = Hash(gridPos);
        spatialHash.Add(hash, index);
    }

    private static int Hash(int3 gridPos)
    {
        unchecked
        {
            return gridPos.x * 73856093 ^ gridPos.y * 19349663 ^ gridPos.z * 83492791;
        }
    }

    private static int3 GetGridPosition(float3 position, float size)
    {
        return new int3(
            (int)math.floor(position.x / size),
            (int)math.floor(position.y / size),
            (int)math.floor(position.z / size)
        );
    }
}