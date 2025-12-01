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
        public uint  GroupID;

        public Boid(float3 position, float3 velocity, uint groupID)
        {
            Position = position;
            Velocity = velocity;
            GroupID = groupID;
        }
    }
}