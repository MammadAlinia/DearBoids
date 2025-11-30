using System;
using Unity.Burst;
using Unity.Mathematics;

namespace Flocking
{
    [BurstCompile]
    [Serializable]
    public struct Boid
    {
        public float3 Position;
        public float3 Velocity;
        public int2 GridCell;

        public Boid(float3 position, float3 velocity, int2 gridCell)
        {
            Position = position;
            Velocity = velocity;
            GridCell = gridCell;
        }
    }
}