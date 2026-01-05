using System;
using System.Collections.Generic;
using System.Linq;
using SpatialPartition;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DearBoids.Grid
{
    public class SpatialPartition : MonoBehaviour
    {
        public bool drawGizmo = false;
        public Material material;
        public Mesh mesh;
        public float CellSize = 1f;
        public int2 GridSize = new int2(8, 12);


        public WorldPartition Partition;

        public float3 pPosition;


        Camera _camera;
        public NativeArray<InstanceData> Instances;
        public InstancedRenderingSystem InstanceRenderer;

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


            Instances = new NativeArray<InstanceData>(GridSize.x * GridSize.y, Allocator.Persistent);

            InstanceRenderer = InstancedRenderingSystem.Default(mesh, material);
            GridUpdateJob gridUpdateJob = new GridUpdateJob()
            {
                Instances = Instances,
                GridSize = GridSize,
                CellSize = CellSize,
                Partition = Partition
            };
            gridUpdateJob.Run();
        }

        private void Dispose()
        {
            if (Instances.IsCreated)
                Instances.Dispose();
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

            GridColorJob gridColorJob = new GridColorJob()
            {
                Instances = Instances,
                cellSize = CellSize,
                Partition = Partition,
                mousePosition = pPosition,
                GridSize = GridSize
            };


            gridColorJob.Run();
            InstanceRenderer.UpdateData(ref Instances);
            InstanceRenderer.Draw();
        }

        private void OnDisable()
        {
            Dispose();
        }
    }
}