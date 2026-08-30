using System;
using DearBoids;
using SpatialPartition;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
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

        [Header("View (camera only)")] public float cameraZoom = 25f;

        [Header("Simulation bounds (world units, independent of camera)")]
        public float simWidth = 50f;

        public float simHeight = 50f;
        public Color boundsColor = new Color(0.26f, 0.59f, 0.98f, 1f);
        public float boundsThickness = 0.15f;
        public bool showBounds = true;

        [Tooltip("Optional. Falls back to Sprites/Default via Shader.Find.")]
        public Material boundsMaterial;

        [Header("Grid (cell size visualization)")]
        public Color gridColor = new Color(0.26f, 0.59f, 0.98f, 0.35f);

        public float gridThickness = 0.04f;
        public bool showGrid = true;

        [Tooltip(
            "Optional material using the Hidden/SimGrid shader. Assign one to guarantee shader inclusion in builds.")]
        public Material gridMaterial;

        [HideInInspector] public float xRange;
        [HideInInspector] public float yRange;

        public FlockingSystem flockingSystem;
        private InstancedRenderingSystem _instancedRenderingSystem;
        private TurningSystem _turningSystem;
        private LineRenderer _boundsLine;
        private Transform _gridQuad;
        private Material _gridMatInstance;

        public FlockingUI ui;
        public int fps;

        public void Start()
        {
            Time.timeScale = .7f;
            SetSimBounds(simWidth, simHeight);
            InitCamera();
            InitSystems();
            InitBoundsVisual();
            InitGridVisual();

            // ---- UI wiring ----
            ui.BoidCountProvider = () => Count;
            ui.BoidCountRequested = c => Count = Mathf.Clamp(c, 1, 300000);
            ui.CameraZoomProvider = () => cameraZoom;
            ui.CameraZoomRequested = SetZoom;
            ui.SimWidthProvider = () => simWidth;
            ui.SimWidthRequested = w => SetSimBounds(w, simHeight);
            ui.SimHeightProvider = () => simHeight;
            ui.SimHeightRequested = h => SetSimBounds(simWidth, h);
            ui.BoundsColorProvider = () => boundsColor;
            ui.BoundsColorRequested = c =>
            {
                boundsColor = c;
                RefreshBoundsVisual();
            };
            ui.ShowBoundsRequested = s =>
            {
                showBounds = s;
                RefreshBoundsVisual();
            };
            ui.CellSizeRequested = SetCellSize;
            ui.GridColorProvider = () => gridColor;
            ui.GridColorRequested = c =>
            {
                gridColor = c;
                RefreshGridVisual();
            };
            ui.ShowGridRequested = s =>
            {
                showGrid = s;
                RefreshGridVisual();
            };
            ui.Init(flockingSystem);

            Time.timeScale = 1;
        }

        public void InitSystems()
        {
            boidsData = new NativeArray<Boid>(Count, Allocator.Persistent);
            boidInstances = new NativeArray<InstanceData>(Count, Allocator.Persistent);
            spatialHash = new NativeParallelMultiHashMap<int3, int>(Count * 2, Allocator.Persistent);

            var partition = new WorldPartition { CellSize = cellSize };

            _instancedRenderingSystem =
                InstancedRenderingSystem.Default(mesh: renderingSetting.mesh, material: renderingSetting.material);

            float hw = simWidth * 0.5f;
            float hh = simHeight * 0.5f;

            for (int i = 0; i < boidsData.Length; i++)
            {
                var randP = new float3(Random.Range(-hw, hw), Random.Range(-hh, hh), 0);
                var randV = new float3(-Random.Range(-1f, 1f) * cameraZoom, Random.Range(-1f, 1f) * cameraZoom, 0);
                var objToWord = float4x4.TRS(randP, quaternion.LookRotation(new float3(0f, 0f, 1f), randV),
                    new float3(1, 1, 1));
                boidInstances[i] = new InstanceData
                {
                    Matrix = objToWord,
                    MatrixInverse = math.inverse(objToWord),
                    Color = Color.Lerp(Color.white, Color.midnightBlue, Random.value),
                };

                boidsData[i] = new Boid
                {
                    Velocity = randV,
                    GroupID = (uint)(i % 2 == 0 ? 0 : 1)
                };
            }

            flockingSystem = FlockingSystem.New(flockingSetting, partition);
            flockingSystem.cellSize =
                cellSize; // New() doesn't copy cellSize — keeps the UI slider's initial value correct
            _turningSystem = TurningSystem.TurnCorner();
        }

        private void InitCamera()
        {
            var cam = Camera.main;

            if (cam == null)
            {
                Debug.LogWarning("[Gameplay] No main camera found.");
                return;
            }

            cam.orthographicSize = cameraZoom;
        }

        // ------------------------------------------------------------ camera / bounds

        public void SetZoom(float orthoSize)
        {
            cameraZoom = orthoSize; // view only
            var cam = Camera.main;
            if (cam != null) cam.orthographicSize = cameraZoom;
        }

        public void SetSimBounds(float width, float height)
        {
            simWidth = Mathf.Max(1f, width);
            simHeight = Mathf.Max(1f, height);
            xRange = simWidth * 0.5f;
            yRange = simHeight * 0.5f;
            RefreshBoundsVisual();
            RefreshGridVisual(); // the grid quad is scaled to the sim rect too
        }

        public void SetCellSize(float v)
        {
            cellSize = Mathf.Max(0.05f, v);
            if (flockingSystem != null) flockingSystem.cellSize = cellSize;
            // To also resize the LIVE spatial hash partition, see the note on
            // WorldPartition.CellSize sync at the bottom of the reply.
            RefreshGridVisual();
        }

        // ------------------------------------------------------------ bounds rectangle

        private void InitBoundsVisual()
        {
            if (_boundsLine == null)
            {
                var go = new GameObject("SimBoundsRect");
                go.transform.SetParent(transform, false);
                _boundsLine = go.AddComponent<LineRenderer>();
                _boundsLine.loop = true;
                _boundsLine.useWorldSpace = true;
                _boundsLine.positionCount = 4;
                _boundsLine.alignment = LineAlignment.View;
                _boundsLine.numCornerVertices = 2;
                _boundsLine.shadowCastingMode = ShadowCastingMode.Off;
                _boundsLine.receiveShadows = false;

                if (boundsMaterial != null)
                    _boundsLine.material = boundsMaterial;
                else
                {
                    var shader = Shader.Find("Sprites/Default");
                    if (shader != null) _boundsLine.material = new Material(shader);
                    else Debug.LogWarning("[Gameplay] Sprites/Default not found — assign 'Bounds Material'.");
                }
            }

            RefreshBoundsVisual();
        }

        private void RefreshBoundsVisual()
        {
            if (_boundsLine == null) return;
            float hw = simWidth * 0.5f, hh = simHeight * 0.5f;
            _boundsLine.SetPosition(0, new Vector3(-hw, -hh, 0f));
            _boundsLine.SetPosition(1, new Vector3(hw, -hh, 0f));
            _boundsLine.SetPosition(2, new Vector3(hw, hh, 0f));
            _boundsLine.SetPosition(3, new Vector3(-hw, hh, 0f));
            _boundsLine.startWidth = boundsThickness;
            _boundsLine.endWidth = boundsThickness;
            _boundsLine.startColor = boundsColor;
            _boundsLine.endColor = boundsColor;
            _boundsLine.enabled = showBounds;
        }

        // ------------------------------------------------------------ grid (cell size)

        private void InitGridVisual()
        {
            if (_gridQuad == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(go.GetComponent<Collider>());
                go.name = "SimGrid";
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0f, 0f, -0.05f); // behind boids & bounds line
                _gridQuad = go.transform;

                var shader = gridMaterial != null ? gridMaterial.shader : Shader.Find("Hidden/SimGrid");

                if (shader == null)
                {
                    Debug.LogWarning(
                        "[Gameplay] Hidden/SimGrid shader not found — create it or assign 'Grid Material'.");
                    go.SetActive(false);
                    return;
                }

                _gridMatInstance = new Material(shader);
                go.GetComponent<MeshRenderer>().sharedMaterial = _gridMatInstance;
            }

            RefreshGridVisual();
        }

        private void RefreshGridVisual()
        {
            if (_gridQuad == null) return;
            bool active = showGrid && _gridMatInstance != null;
            _gridQuad.gameObject.SetActive(active);
            if (!active) return;

            _gridQuad.localScale = new Vector3(simWidth, simHeight, 1f);
            _gridMatInstance.SetColor("_Color", gridColor);
            _gridMatInstance.SetFloat("_CellSize", cellSize);
            _gridMatInstance.SetFloat("_Thickness", Mathf.Max(0.001f, gridThickness));
        }

        // ------------------------------------------------------------ loop

        void Update()
        {
            fps = (int)(1f / Time.unscaledDeltaTime);

            if (prevCount != Count)
            {
                Dispose();
                InitCamera();
                InitSystems();
                ui.Init(flockingSystem); // rebind: InitSystems created a NEW FlockingSystem
                RefreshGridVisual(); // new system -> cellSize from settings again
                prevCount = Count;
                return;
            }

            _turningSystem.TurnUpdate(ref boidsData, ref boidInstances,
                new float2(-xRange, xRange), new float2(-yRange, yRange), flockingSetting.turnFactor);
            flockingSystem.UpdateFlocking(ref boidsData, ref boidInstances, ref spatialHash);

            _instancedRenderingSystem.UpdateData(ref boidInstances);
            _instancedRenderingSystem.Draw();
        }

        void OnDisable() => Dispose();

        private void OnDestroy()
        {
            if (_gridMatInstance != null) Destroy(_gridMatInstance);
        }

        void Dispose()
        {
            flockingSystem?.Dispose();
            _instancedRenderingSystem?.Dispose();
            if (boidsData.IsCreated) boidsData.Dispose();
            if (boidInstances.IsCreated) boidInstances.Dispose();
            if (spatialHash.IsCreated) spatialHash.Dispose();
        }

        private NativeArray<Boid> boidsData;
        private NativeParallelMultiHashMap<int3, int> spatialHash;
        private NativeArray<InstanceData> boidInstances;
    }
}