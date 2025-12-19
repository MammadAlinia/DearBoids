using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Mathematics;

namespace Flocking
{
    [BurstCompile]
    [StructLayout(LayoutKind.Sequential)]
    public struct Boid
    {
        public float4x4 objectToWorld;
        public float3 Velocity;
        public uint GroupID;

    }
}