using System;
using DearBoids;
using SpatialPartition;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DearBoids
{
    public class Gameplay : MonoBehaviour
    {
        public FlockingSettingData flockingSetting;
        public RenderingSetting renderingSetting;

        public int count = 1000;
        public float cellSize = 1f;
        public float scale = 25f;

        private NativeArray<Boid> boidsData;
        private NativeParallelMultiHashMap<int3, int> spatialHash;
        private NativeArray<InstanceData> boidInstances;

        public float xRange;
        public float yRange;
        public Vector3 cameraS;

      [SerializeField]  private FlockingSystem _flockingSystem;
        private InstancedRenderingSystem _instancedRenderingSystem;
        private TurningSystem _turningSystem;

        public void Start()
        {
            boidsData = new NativeArray<Boid>(count, Allocator.Persistent);
            boidInstances = new NativeArray<InstanceData>(count, Allocator.Persistent);
            spatialHash = new NativeParallelMultiHashMap<int3, int>(count * 2, Allocator.Persistent);

            var partition = new WorldPartition()
            {
                CellSize = cellSize,
            };


            _instancedRenderingSystem =
                InstancedRenderingSystem.Default(mesh: renderingSetting.mesh, material: renderingSetting.material);

            Camera.main.orthographicSize = scale;
            cameraS = Camera.main.OrthographicBounds().size;
            xRange = cameraS.x * 0.35f;
            yRange = cameraS.y * 0.35f;

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


            _flockingSystem = FlockingSystem.New(flockingSetting, partition);
            _turningSystem = TurningSystem.TurnCorner();
        }

        void Update()
        {
            _turningSystem.TurnUpdate(ref boidsData, ref boidInstances, new float2(-xRange, xRange),
                new float2(-yRange, yRange), flockingSetting.turnFactor);
            _flockingSystem.UpdateFlocking(ref boidsData, ref boidInstances, ref spatialHash);

            _instancedRenderingSystem.UpdateData(ref boidInstances);
            _instancedRenderingSystem.Draw();
        }

        void OnDisable()
        {
            _flockingSystem?.Dispose();
            _instancedRenderingSystem?.Dispose();
            if (boidsData.IsCreated) boidsData.Dispose();
            if (boidInstances.IsCreated) boidInstances.Dispose();
            if (spatialHash.IsCreated) spatialHash.Dispose();
        }
    }
}