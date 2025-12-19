using Unity.Mathematics;
using UnityEngine;

public static class MathExtensions
{
    public static Matrix4x4 ToMatrix4x4(this float4x4 m)
    {
        return new Matrix4x4(
            new Vector4(m.c0.x, m.c0.y, m.c0.z, m.c0.w),
            new Vector4(m.c1.x, m.c1.y, m.c1.z, m.c1.w),
            new Vector4(m.c2.x, m.c2.y, m.c2.z, m.c2.w),
            new Vector4(m.c3.x, m.c3.y, m.c3.z, m.c3.w)
        );
    }
}