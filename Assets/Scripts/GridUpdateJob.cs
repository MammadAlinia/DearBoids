using SpatialPartition;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Flocking.Grid
{
    [BurstCompile]
    public struct GridUpdateJob : IJob
    {
        public float CellSize;
        public int2 GridSize;
        public WorldPartition Partition;
        public NativeArray<InstanceData> Instances;

        public void Execute()
        {
            for (int x = 0; x < GridSize.x; x++)
            {
                for (int y = 0; y < GridSize.y; y++)
                {
                    var offset = math.round((new float3(GridSize.x, GridSize.y, 0f) / 2f)) * CellSize;
                    var wp = Partition.ToWorldPosition(new int3(x, y, 0)) - offset;

                    var matrix = float4x4.TRS(
                        translation: wp,
                        quaternion.identity,
                        new float3(1f, 1f, 1f) * CellSize);
                    Instances[y * GridSize.x + x] = new InstanceData()
                    {
                        Matrix = matrix,
                        MatrixInverse = math.inverse(matrix),
                        Color = Color.white
                    };
                }
            }
        }
    }
}