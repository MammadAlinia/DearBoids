using SpatialPartition;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Flocking.Grid
{
    [BurstCompile]
    public struct GridUpdateJob : IJob
    {
        public NativeArray<float4x4> CellPositions;
        public float CellSize;
        public int2 GridSize;
        public WorldPartition Partition;

        public void Execute()
        {
            for (int x = 0; x < GridSize.x; x++)
            {
                for (int y = 0; y < GridSize.y; y++)
                {
                    var offset = math.round((new float3(GridSize.x, GridSize.y, 0f) / 2f)) * CellSize;
                    var wp = Partition.ToWorldPosition(new int3(x, y, 0)) - offset;
                    CellPositions[y * GridSize.x + x] =
                        float4x4.TRS(
                            translation: wp,
                            quaternion.identity,
                            new float3(1f, 1f, 1f) * CellSize);
                }
            }
        }
    }
}