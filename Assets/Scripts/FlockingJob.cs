using DearBoids;
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
    [WriteOnly] public NativeArray<Boid> BoidsDataOut;
    [ReadOnly] public NativeParallelMultiHashMap<int3, int> spatialHash;
    [ReadOnly] public WorldPartition Partition;
    [ReadOnly] public NativeArray<InstanceData> InstancesIn;
    [WriteOnly] public NativeArray<InstanceData> InstancesOut;
    public float CellSize;
    public float AvoidanceRange;
    public float AvoidanceStrength;
    public float AlignmentRange;
    public float AlignmentStrength;
    public float CohesionStrength;
    public float DeltaTime;
    public float Speed;


    public float MaxSpeed;
    public float MinSpeed;


    public void Execute(int i)
    {
        var currentBoid = BoidsDataIn[i];
        var currentInstance = InstancesIn[i];
        var position = currentInstance.Matrix.c3.xyz;
        var velocity = currentBoid.Velocity;

        // Flocking parameters
        var separationCount = 0;
        var neighborCount = 0;
        var separation = float3.zero;
        var alignment = float3.zero;
        var cohesion = float3.zero;

        var gridPos = Partition.ToPartition(position);
        //     int maxCheck = 1000;
        int maxCheck = int.MaxValue;
        int checkedBoids = 0;

        var neighborCellsToCheck = math.clamp((int)math.round((AlignmentRange / 2) / CellSize), 1, int.MaxValue);

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
                        var otherP = InstancesIn[otherIndex].Matrix.c3.xyz;
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
                        if (dist <= AlignmentRange)
                        {
                            // if (currentBoid.GroupID == otherBoid.GroupID)
                            {
                                alignment += otherBoid.Velocity;
                                cohesion += otherP;
                            }
                            // else
                            {
                            }


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
            velocity += separation * AvoidanceStrength;
        }

        if (neighborCount > 0)
        {
            // Apply alignment

            alignment /= neighborCount; // Average position
            velocity += (alignment - velocity) * AlignmentStrength;


            // Apply Cohesion
            cohesion /= neighborCount;
            velocity += (cohesion - position) * CohesionStrength;
        }


        float speed = math.length(velocity);

        if (speed > 0.001f)
        {
            velocity = math.normalize(velocity) * math.clamp(speed, MinSpeed, MaxSpeed);
        }

        var direction = math.length(velocity) > 0.001f
            ? math.normalize(velocity)
            : new float3(0, 1, 0);

        position += velocity * DeltaTime;
        currentInstance.Matrix = float4x4.TRS(position,
            quaternion.LookRotation(new float3(0, 0, 1), direction), new float3(1, 1, 1));
        currentInstance.MatrixInverse = math.inverse(currentInstance.Matrix);
        currentBoid.Velocity = velocity;

        InstancesOut[i] = currentInstance;
        BoidsDataOut[i] = currentBoid;
    }
}