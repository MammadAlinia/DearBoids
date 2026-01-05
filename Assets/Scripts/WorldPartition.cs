using Unity.Burst;
using Unity.Mathematics;

namespace SpatialPartition
{
    [BurstCompile]
    public struct WorldPartition
    {
        public float CellSize;

        
        public float3 ToWorldPosition(int3 gridPosition)
        {
            return new float3()
            {
                x = gridPosition.x * CellSize,
                y = gridPosition.y * CellSize,
                z = 0
            };
            
        }

        public int3 ToPartition(float3 worldPosition)
        {
            return new int3()
            {
                x = (int)math.round(worldPosition.x / CellSize),
                y = (int)math.round(worldPosition.y / CellSize),
                z = 0
            };
        }

        public float3 ToWordPartition(float3 worldPosition)
        {
            return ToWorldPosition(ToPartition(worldPosition));
        }
    }
}