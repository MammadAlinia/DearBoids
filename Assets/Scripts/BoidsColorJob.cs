using Flocking;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

[BurstCompile]
public struct BoidsColorJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<Boid> BoidsData;
    public NativeArray<Color> boidsColor;

    public void Execute(int i)
    {
        if (BoidsData[i].GroupID == 0)
            boidsColor[i] = Color.teal;
        if (BoidsData[i].GroupID == 1)
            boidsColor[i] = Color.orange;
        if (BoidsData[i].GroupID == 2)
            boidsColor[i] = Color.paleVioletRed;
        if (BoidsData[i].GroupID == 3)
            boidsColor[i] = Color.red;
    }
}