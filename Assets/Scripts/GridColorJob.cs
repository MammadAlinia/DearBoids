using SpatialPartition;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace DearBoids.Grid
{
    [BurstCompile]
    public struct GridColorJob : IJob
    {
        [ReadOnly] public WorldPartition Partition;
        public float3 mousePosition;
        public float cellSize;
        public int2 GridSize;
        public NativeArray<InstanceData> Instances;

        public void Execute()
        {
            var offset = math.round((new float3(GridSize.x, GridSize.y, 0f) / 2f)) * cellSize;
            var gridSize = Instances.Length;

            for (int x = 0; x < GridSize.x; x++)
            {
                for (int y = 0; y < GridSize.y; y++)
                {
                    var index = y * GridSize.x + x;
                    var data = Instances[index];
                    var lerpedI = (float)index / (gridSize);
                    var color = new Color(lerpedI, lerpedI, 0, 1);

                    var wp = Partition.ToWorldPosition(new int3(x, y, 0)) - offset;

                    var gPos = Partition.ToPartition(wp);

                    if (gPos.Equals(Partition.ToPartition(mousePosition)))
                    {
                        data.Color = Color.blue;
                    }
                    else
                    {
                        data.Color = Color.wheat;
                    }

                    Instances[index] = data;
                }
            }
        }
    }
}