using Flocking;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile]
public struct FlockingJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<Boid> BoidsDataIn;
    [ReadOnly] public NativeParallelMultiHashMap<int, int> spatialHash;

    [WriteOnly] public NativeArray<Boid> BoidsDataOut;
    public NativeArray<Matrix4x4> VisualData;

    public float CellSize;
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
    public float ZRange;

    public void Execute(int i)
    {
        var currentBoid = BoidsDataIn[i];
        var position = currentBoid.Position;
        var velocity = currentBoid.Velocity;

        // Flocking calculations
        var closeBoidsCount = 0;
        var inRangeBoidsCount = 0;
        var avoidanceVector = float3.zero;
        var avgVelocityVector = float3.zero;
        var avgPosition = float3.zero;

        var gridPos = GetGridPosition(position, CellSize);
        int maxCheck = 500;
        int checkedBoids = 0;

        // Check 8 neighboring cells (3x3x3)
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                var neighborCell = gridPos + new int3(x, y, 0);
                var hash = Hash(neighborCell);

                // Query all boids in this cell
                if (spatialHash.TryGetFirstValue(hash, out int otherIndex, out var iterator))
                {
                    do
                    {
                        if (otherIndex == i) continue;

                        var otherBoid = BoidsDataIn[otherIndex];
                        var distance = math.distance(otherBoid.Position, position);
                        checkedBoids++;

                        // Avoidance
                        if (distance <= AvoidanceRange && distance > 0.001f)
                        {
                            closeBoidsCount++;
                            avoidanceVector += (position - otherBoid.Position) / distance;
                        }

                        // Alignment & Cohesion
                        if (distance <= AlignmentRange)
                        {
                            avgVelocityVector += otherBoid.Velocity;
                            avgPosition += otherBoid.Position;
                            inRangeBoidsCount++;
                        }
                    } while (checkedBoids < maxCheck && spatialHash.TryGetNextValue(out otherIndex, ref iterator));
                }
            }
        }

        // Apply flocking forces
        if (closeBoidsCount > 0)
        {
            avoidanceVector = math.normalize(avoidanceVector) * AvoidanceForce;
        }

        if (inRangeBoidsCount > 0)
        {
            avgVelocityVector = ((avgVelocityVector / inRangeBoidsCount) - velocity) * AlignmentForce;
            avgPosition = ((avgPosition / inRangeBoidsCount) - position) * CohesionForce;
        }

        // Boundary constraints
        if (position.y < -YRange && velocity.y <= 0) velocity.y += TurnFactor;
        if (position.y > YRange && velocity.y >= 0) velocity.y -= TurnFactor;
        if (position.x < -XRange && velocity.x <= 0) velocity.x += TurnFactor;
        if (position.x > XRange && velocity.x >= 0) velocity.x -= TurnFactor;
        if (position.z < -ZRange && velocity.z <= 0) velocity.z += TurnFactor;
        if (position.z > ZRange && velocity.z >= 0) velocity.z -= TurnFactor;

        // Calculate final velocity
        var finalVel = velocity + avoidanceVector + avgVelocityVector + avgPosition;
        var speed = math.length(finalVel);

        if (speed > 0.001f)
        {
            finalVel = math.normalize(finalVel) * Speed;
        }
        else
        {
            finalVel = new float3(0, 0, 1) * Speed; // Default direction
        }

        currentBoid.Velocity = finalVel;
        currentBoid.Position += finalVel * DeltaTime;

        BoidsDataOut[i] = currentBoid;

        // Visual transform
        var direction = math.length(currentBoid.Velocity) > 0.001f
            ? math.normalize(currentBoid.Velocity)
            : new float3(0, 1, 0);

        VisualData[i] = Matrix4x4.TRS(
            currentBoid.Position,
            quaternion.LookRotation(new float3(0f, 0f, 1f), direction),
            new float3(0.5f, 1f, 1f)
        );
    }

    private static int Hash(int3 gridPos)
    {
        unchecked
        {
            return gridPos.x * 73856093 ^ gridPos.y * 19349663 ^ gridPos.z * 83492791;
        }
    }

    private static int3 GetGridPosition(float3 position, float size)
    {
        return new int3(
            (int)math.floor(position.x / size),
            (int)math.floor(position.y / size),
            (int)math.floor(position.z / size)
        );
    }
}