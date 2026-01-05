using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace DearBoids
{
    public class TurningSystem
    {
        [BurstCompile]
        public struct BoidTurnCornerJob : IJobParallelFor
        {
            public float2 MinMaxX;
            public float2 MinMaxY;
            public NativeArray<Boid> BoidsDataIn;
            public NativeArray<InstanceData> InstanceDataIn;
            public float TurningSpeed;

            public void Execute(int i)
            {
                var boid = BoidsDataIn[i];
                var instance = InstanceDataIn[i];

                // if boids outside the bounds
                var position = instance.Matrix.c3.xyz;
                var velocity = boid.Velocity;
                if (position.x < MinMaxX.x) // p.x < b.x.min
                    velocity.x += TurningSpeed;
                if (position.x > MinMaxX.y) // p.x > b.x.max
                    velocity.x -= TurningSpeed;
                if (position.y < MinMaxY.x) // p.y < b.y.min
                    velocity.y += TurningSpeed;
                if (position.y > MinMaxY.y) // p.y > b.y.max
                    velocity.y -= TurningSpeed;

                boid.Velocity = velocity;
                BoidsDataIn[i] = boid;
            }
        }

        public void TurnUpdate(ref NativeArray<Boid> boids, ref NativeArray<InstanceData> instances, float2 minMaxX,
            float2 minMaxY, float turingSpeed)
        {
            var turnJob = new BoidTurnCornerJob
            {
                MinMaxX = minMaxX,
                MinMaxY = minMaxY,
                BoidsDataIn = boids,
                InstanceDataIn = instances,
                TurningSpeed = turingSpeed
            };
            var handle = turnJob.Schedule(boids.Length, boids.Length / 4);
            handle.Complete();
            boids = turnJob.BoidsDataIn;
        }

        public static TurningSystem TurnCorner()
        {
            return new TurningSystem();
        }

        public static TurningSystem Teleport()
        {
            throw new NotImplementedException();
        }
    }
}