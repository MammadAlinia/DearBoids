using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Mathematics;

namespace DearBoids
{
    [BurstCompile]
    [StructLayout(LayoutKind.Sequential)]
    public struct Boid
    {
        public float3 Velocity;
        public uint GroupID;

    }
}