using System;
using DearBoids;
using SpatialPartition;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;
using Random = UnityEngine.Random;

namespace DearBoids
{
    public class Gameplay : MonoBehaviour
    {
        public FlockingSettingData flockingSetting;
        public RenderingSetting renderingSetting;

        public int prevCount = 1000;
        public int Count = 1000;

        public float cellSize = 1f;
        public float scale = 25f;

        private NativeArray<Boid> boidsData;
        private NativeParallelMultiHashMap<int3, int> spatialHash;
        private NativeArray<InstanceData> boidInstances;

        public float xRange;
        public float yRange;
        public Vector3 cameraS;

        public FlockingSystem flockingSystem;
        private InstancedRenderingSystem _instancedRenderingSystem;
        private TurningSystem _turningSystem;

        public UIDocument document;
        public int fps;

        public void Start()
        {
            document.rootVisualElement.dataSource = this;
            InitSize();
            InitSystems();
        }

        public void InitSystems()
        {
            boidsData = new NativeArray<Boid>(Count, Allocator.Persistent);
            boidInstances = new NativeArray<InstanceData>(Count, Allocator.Persistent);
            spatialHash = new NativeParallelMultiHashMap<int3, int>(Count * 2, Allocator.Persistent);

            var partition = new WorldPartition()
            {
                CellSize = cellSize,
            };


            _instancedRenderingSystem =
                InstancedRenderingSystem.Default(mesh: renderingSetting.mesh, material: renderingSetting.material);

            for (int i = 0; i < boidsData.Length; i++)
            {
                var randP = new float3(Random.Range(-1f, 1f) * scale, Random.Range(-1f, 1f) * scale, 0);
                var randV = new float3(-Random.Range(-1f, 1f) * scale, Random.Range(-1f, 1f) * scale, 0);
                var objToWord = float4x4.TRS(randP, quaternion.LookRotation(new float3(0f, 0f, 1f), randV),
                    new float3(1, 1, 1));
                boidInstances[i] = new InstanceData()
                {
                    Matrix = objToWord,
                    MatrixInverse = math.inverse(objToWord),
                    Color = i % 2 == 0 ? Color.cyan : Color.magenta
                };

                boidsData[i] = new Boid()
                {
                    Velocity = randV,
                    GroupID = (uint)(i % 2 == 0 ? 0 : 1)
                };
            }


            flockingSystem = FlockingSystem.New(flockingSetting, partition);
            _turningSystem = TurningSystem.TurnCorner();
        }

        void InitSize()
        {
            Camera.main.orthographicSize = scale;
            cameraS = Camera.main.OrthographicBounds().size;
            xRange = cameraS.x * 0.50f;
            yRange = cameraS.y * 0.50f;
        }

        void Update()
        {
            fps = (int)(1f / Time.unscaledDeltaTime);

            if (prevCount != Count)
            {
                Dispose();
                InitSize();
                InitSystems();
                prevCount = Count;
                return;
            }

            _turningSystem.TurnUpdate(ref boidsData, ref boidInstances, new float2(-xRange, xRange),
                new float2(-yRange, yRange), flockingSetting.turnFactor);
            flockingSystem.UpdateFlocking(ref boidsData, ref boidInstances, ref spatialHash);

            _instancedRenderingSystem.UpdateData(ref boidInstances);
            _instancedRenderingSystem.Draw();
        }

        void OnDisable()
        {
            Dispose();
        }

        void Dispose()
        {
            flockingSystem?.Dispose();
            _instancedRenderingSystem?.Dispose();
            if (boidsData.IsCreated) boidsData.Dispose();
            if (boidInstances.IsCreated) boidInstances.Dispose();
            if (spatialHash.IsCreated) spatialHash.Dispose();
        }
    }
}