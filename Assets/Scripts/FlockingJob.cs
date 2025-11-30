using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Flocking
{
    [BurstCompile]
    public struct FlockingJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Boid> BoidsDataIn;
        [WriteOnly] public NativeArray<Boid> BoidsDataOut;
        public NativeArray<Matrix4x4> VisualData;
        public float TurnFactor;
        public float AvoidanceRange;
        public float AvoidanceForce;

        public float AlignmentRange;
        public float AlignmentForce;

        public float CohesionForce;
        public float DeltaTime;
        public float Speed;
        public float XRange;
        public float YRange;


        public void Execute(int i)
        {
            var closeBoidsCount = 0;
            var inRangeBoidsCount = 0;
            var avoidanceVector = float3.zero;
            var avgVelocityVector = float3.zero;
            var avgPosition = float3.zero;

            var currentBoid = BoidsDataIn[i];
            var position = currentBoid.Position;
            var velocity = currentBoid.Velocity;


            for (int j = 0; j < BoidsDataIn.Length; j++)
            {
                if (j == i) continue;
                var otherBoid = BoidsDataIn[j];
                var distance = math.distance(otherBoid.Position, position);

                if (distance <= AvoidanceRange) // avoidance
                {
                    closeBoidsCount++;
                    avoidanceVector += (position - otherBoid.Position);
                }

                if (distance <= AlignmentRange)
                {
                    avgVelocityVector += otherBoid.Velocity;
                    avgPosition += otherBoid.Position;
                    inRangeBoidsCount++;
                }
            }

            avoidanceVector = closeBoidsCount > 0 ? avoidanceVector * AvoidanceForce : 0;
            avgVelocityVector = inRangeBoidsCount > 0
                ? ((avgVelocityVector / inRangeBoidsCount) - velocity) * AlignmentForce
                : 0;
            avgPosition = inRangeBoidsCount > 0
                ? ((avgPosition / inRangeBoidsCount) - position) * CohesionForce
                : 0;
            // edges
            if (position.y < -YRange && velocity.y <= 0) velocity.y += TurnFactor;
            if (position.y > YRange && velocity.y >= 0) velocity.y -= TurnFactor;
            if (position.x < -XRange && velocity.x <= 0) velocity.x += TurnFactor;
            if (position.x > XRange && velocity.x >= 0) velocity.x -= TurnFactor;

            var finalVel = velocity + avoidanceVector + avgVelocityVector + avgPosition;
            finalVel = math.normalize(finalVel) * Speed;

            currentBoid.Velocity = finalVel;
            currentBoid.Position += finalVel * DeltaTime;


            BoidsDataOut[i] = currentBoid;

            // visual
            VisualData[i] = Matrix4x4.TRS(currentBoid.Position,
                quaternion.LookRotation(new float3(0f, 0f, 1f), math.normalize(currentBoid.Velocity)),
                new float3(.5f, 1f, 1f));
        }
    }
}