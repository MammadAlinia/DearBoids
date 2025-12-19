using Flocking;
using SpatialPartition;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile]
public struct FlockingJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<Boid> BoidsDataIn;
    [ReadOnly] public NativeParallelMultiHashMap<int3, int> spatialHash;
    [ReadOnly] public WorldPartition Partition;
    [WriteOnly] public NativeArray<Boid> BoidsDataOut;

    public float CellSize;
    public float boundryWeigth;
    public float AvoidanceRange;
    public float seperationForce;
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
        var position = currentBoid.objectToWorld.c3.xyz;
        var velocity = currentBoid.Velocity;

        // Flocking parameters
        var separationCount = 0;
        var neighborCount = 0;
        var separation = float3.zero;
        var alignment = float3.zero;
        var cohesion = float3.zero;

        var gridPos = Partition.ToPartition(position);
        //     int maxCheck = 1000;
        int maxCheck = 1000;
        int checkedBoids = 0;

        var neighborCellsToCheck = (int)math.round((AlignmentRange / 2) / CellSize);

        // Check 8 neighboring cells (3x3)
        for (int x = -neighborCellsToCheck; x <= neighborCellsToCheck; x++)
        {
            for (int y = -neighborCellsToCheck; y <= neighborCellsToCheck; y++)
            {
                var neighborCell = gridPos + new int3(x, y, 0);

                // Query all boids in this cell
                if (spatialHash.TryGetFirstValue(neighborCell, out int otherIndex, out var iterator))
                {
                    do
                    {
                        if (otherIndex == i) continue;
                        var otherBoid = BoidsDataIn[otherIndex];
                        var otherP = otherBoid.objectToWorld.c3.xyz;
                        var offset = position - otherP;
                        var dist = math.length(offset);

                        if (math.dot(velocity, math.normalize(otherP - position)) < -0.33f) continue;

                        // Avoidance
                        if (dist <= AvoidanceRange && dist > 0.0001f)
                        {
                            separation += offset;
                            separationCount++;
                        }

                        // Alignment & Cohesion
                        if (currentBoid.GroupID == otherBoid.GroupID && dist <= AlignmentRange)
                        {
                            alignment += otherBoid.Velocity;
                            cohesion += otherP;
                            neighborCount++;
                        }

                        checkedBoids++;

                        if (checkedBoids > maxCheck)
                        {
                            break;
                        }
                    } while (spatialHash.TryGetNextValue(out otherIndex, ref iterator));
                }
            }
        }


        // Apply separation
        if (separationCount > 0)
        {
            separation /= separationCount; // Average separation
            velocity += separation * seperationForce;
        }

        if (neighborCount > 0)
        {
            // Apply alignment

            alignment /= neighborCount; // Average position
            velocity += (alignment - velocity) * AlignmentForce;


            // Apply Cohesion
            cohesion /= neighborCount;
            velocity += (cohesion - position) * CohesionForce;
        }


        if (position.x < -XRange)
            velocity.x += boundryWeigth;

        if (position.x > XRange)
            velocity.x -= boundryWeigth;

        if (position.y < -YRange)
            velocity.y += boundryWeigth;

        if (position.y > YRange)
            velocity.y -= boundryWeigth;


        float speed = math.length(velocity);

        if (speed > 0.001f)
        {
            velocity = math.normalize(velocity) * math.clamp(speed, minSpeed, maxSpeed);
        }

        var direction = math.length(velocity) > 0.001f
            ? math.normalize(velocity)
            : new float3(0, 1, 0);

        position += velocity * DeltaTime;
        currentBoid.objectToWorld = float4x4.TRS(position,
            quaternion.LookRotation(new float3(0, 0, 1), direction), new float3(.5f, 1, 1));
        currentBoid.Velocity = velocity;

        BoidsDataOut[i] = currentBoid;
    }

    public float maxSpeed;
    public float minSpeed;
}