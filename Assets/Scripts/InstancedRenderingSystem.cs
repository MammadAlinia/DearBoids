using System;
using DearBoids.Grid;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;


[BurstCompile]
public struct InstanceData
{
    public float4x4 Matrix;
    public float4x4 MatrixInverse;
    public Color Color;

    public static int Size()
    {
        return sizeof(float) * 4 * 4
               + sizeof(float) * 4 * 4
               + sizeof(float) * 4;
    }
}

/// <summary>
/// A high-performance helper for Indirect Instanced Rendering.
/// Optimized for Unity.Mathematics and NativeArrays (Burst/Jobs compatible).
/// </summary>
public class InstancedRenderingSystem : IDisposable
{
    public static InstancedRenderingSystem Default(Mesh mesh, Material material) =>
        new InstancedRenderingSystem(mesh, material, "_PerInstanceItemData");

    private const int ARGS_COUNT = 5;

    public Mesh Mesh;
    public Material Material;
    public ShadowCastingMode ShadowCasting = ShadowCastingMode.Off;
    public bool ReceiveShadows = true;

    private ComputeBuffer _argsBuffer;
    private ComputeBuffer _dataBuffer;
    private MaterialPropertyBlock _mpb;
    private Bounds _bounds;

    // We use a NativeArray for args to allow easy access if we ever want to write args from a Job
    private readonly uint[] _args = new uint[ARGS_COUNT];
    private readonly string _bufferShaderName;

    public InstancedRenderingSystem(Mesh mesh, Material material, string bufferShaderName = "_InstanceData")
    {
        Mesh = mesh;
        Material = material;
        _bufferShaderName = bufferShaderName;
        _mpb = new MaterialPropertyBlock();
        _bounds = new Bounds(Vector3.zero, Vector3.one * 100000); // Large bounds to avoid culling
    }

    /// <summary>
    /// Uploads data from a NativeArray directly to the GPU.
    /// Fast and generates zero garbage.
    /// </summary>
    public void UpdateData(ref NativeArray<InstanceData> data)
    {
        int count = data.Length;

        // 1. Resize Buffer if needed
        // Note: In a real production scenario, you might want to grow the buffer in chunks (powers of 2)
        // to avoid resizing every single frame if the count fluctuates slightly.
        if (_dataBuffer == null || _dataBuffer.count != count)
        {
            ReleaseBuffer(ref _dataBuffer);

            if (count > 0)
            {
                // UnsafeUtility.SizeOf<T>() is essentially sizeof(T) for unmanaged types
                _dataBuffer = new ComputeBuffer(count,
                    Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SizeOf<InstanceData>());
            }
        }

        // 2. Set Data
        if (count > 0)
        {
            _dataBuffer.SetData(data);
            _mpb.SetBuffer(_bufferShaderName, _dataBuffer);
        }

        // 3. Update Args
        UpdateArgsBuffer(count);
    }

    private void UpdateArgsBuffer(int count)
    {
        if (_argsBuffer == null)
        {
            _argsBuffer = new ComputeBuffer(1, _args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        }

        _args[0] = (uint)Mesh.GetIndexCount(0);
        _args[1] = (uint)count;
        _args[2] = (uint)Mesh.GetIndexStart(0);
        _args[3] = (uint)Mesh.GetBaseVertex(0);
        _args[4] = 0;

        _argsBuffer.SetData(_args);
    }

    public void Draw()
    {
        if (_dataBuffer == null || _argsBuffer == null || Mesh == null || Material == null ||
            _dataBuffer.count == 0) return;

        Graphics.DrawMeshInstancedIndirect(
            Mesh,
            0,
            Material,
            _bounds,
            _argsBuffer,
            0,
            _mpb,
            ShadowCasting,
            ReceiveShadows
        );
    }

    public void Dispose()
    {
        ReleaseBuffer(ref _dataBuffer);
        ReleaseBuffer(ref _argsBuffer);
    }

    private void ReleaseBuffer(ref ComputeBuffer buffer)
    {
        if (buffer != null)
        {
            buffer.Release();
            buffer = null;
        }
    }
}