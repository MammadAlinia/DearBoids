using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using SpatialPartition;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Flocking.Grid
{
    public struct ItemInstanceData
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

    public class GridController : MonoBehaviour
    {
        public bool drawGizmo = false;
        public Material material;
        public Mesh mesh;
        public float CellSize = 1f;
        public int2 GridSize = new int2(8, 12);


        public WorldPartition Partition;

        public float3 pPosition;


        Camera _camera;
        public NativeArray<ItemInstanceData> Instances;
        public InstancedRenderBatch<ItemInstanceData> InstanceRenderer;

        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                //  Initialize();
            }
        }

        private void Initialize()
        {
            _camera = Camera.main;
            if (_camera == null) return;
            var cameraS = _camera.OrthographicBounds().size / CellSize;
            GridSize = (int2)math.round(new float2(cameraS.x, cameraS.y));
            Partition = new WorldPartition()
            {
                CellSize = CellSize
            };


            Instances = new NativeArray<ItemInstanceData>(GridSize.x * GridSize.y, Allocator.Persistent);

            InstanceRenderer = new InstancedRenderBatch<ItemInstanceData>(mesh, material, "_PerInstanceItemData");
        }

        private void Dispose()
        {
            InstanceRenderer?.Dispose();
        }

        private void Start()
        {
            Initialize();
        }

        private void Update()
        {
            if (!_camera) return;
            var vector2 = Mouse.current.position.ReadValue();

            var mousePosition = _camera.ScreenToWorldPoint(vector2);
            mousePosition.z = 0;
            pPosition = Partition.ToWordPartition(mousePosition);
            GridUpdateJob gridUpdateJob = new GridUpdateJob()
            {
                Instances = Instances,
                GridSize = GridSize,
                CellSize = CellSize,
                Partition = Partition
            };
            gridUpdateJob.Run();

            GridColorJob gridColorJob = new GridColorJob()
            {
                Instances = Instances,
                cellSize = CellSize,
                Partition = Partition,
                mousePosition = pPosition,
                GridSize = GridSize
            };


            gridColorJob.Run();
            InstanceRenderer.UpdateData(Instances);
            InstanceRenderer.Draw();
        }

        private void OnDrawGizmos()
        {
            if (!drawGizmo)
                return;


            var offset = math.round((new float3(GridSize.x, GridSize.y, 0f) / 2f)) * CellSize;
            var cellOffset = float3.zero;
            var gridSize = Instances.Length;

            for (int x = 0; x < GridSize.x; x++)
            {
                for (int y = 0; y < GridSize.y; y++)
                {
                    var index = y * GridSize.x + x;
                    var lerpedI = (float)index / (gridSize);
                    Gizmos.color = new Color(lerpedI, lerpedI, 0, 1);

                    var wp = Partition.ToWorldPosition(new int3(x, y, 0)) - offset;

                    var gPos = Partition.ToPartition(wp);

                    if (gPos.Equals(Partition.ToPartition(pPosition)))
                    {
                        Gizmos.color = Color.teal;
                        Gizmos.DrawCube(wp + cellOffset, Vector3.one * CellSize);
                    }
                    else
                    {
                        Gizmos.DrawCube(wp + cellOffset, Vector3.one * CellSize);
                    }

                    // UnityEditor.Handles.Label(wp, $"({gPos.x},{gPos.y})");
                }
            }
        }

        private void OnDisable()
        {
            Dispose();
        }

        private void OnApplicationQuit()
        {
            Dispose();
        }
    }
}