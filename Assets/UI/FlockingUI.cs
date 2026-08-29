using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DearBoids; // remove if FlockingSystem is in the global namespace
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// Dear ImGui-styled runtime control panel (UI Toolkit) for FlockingSystem.
/// Multiple draggable windows (Boids Controls / View / Stats) registered in a
/// top "Windows" menu with visibility checkmarks.
/// </summary>
public class FlockingUI : MonoBehaviour
{
    private bool _built;

    [Header("Wiring")] [SerializeField] private PanelRenderer panelRenderer;
    [SerializeField] private StyleSheet theme; // DearImGui.uss (auto-loads from Resources/ if empty)
    [SerializeField] private InputActionReference toggleAction;

    [Header("Optional")] [Tooltip("Returns the current boid count (Stats row + initial slider value).")]
    public Func<int> BoidCountProvider;

    [Tooltip("Called when the user commits a new boid count (on drag release).")]
    public Action<int> BoidCountRequested;

    [Tooltip("Returns the initial camera zoom (orthographic size).")]
    public Func<float> CameraZoomProvider;

    [Tooltip("Called live while the zoom slider drags.")]
    public Action<float> CameraZoomRequested;

    [Tooltip("Returns the simulation bounds width in world units.")]
    public Func<float> SimWidthProvider;

    [Tooltip("Called live while the width slider drags.")]
    public Action<float> SimWidthRequested;

    [Tooltip("Returns the simulation bounds height in world units.")]
    public Func<float> SimHeightProvider;

    [Tooltip("Called live while the height slider drags.")]
    public Action<float> SimHeightRequested;

    [Tooltip("Returns the current bounds rectangle color.")]
    public Func<Color> BoundsColorProvider;

    [Tooltip("Called when the bounds color changes.")]
    public Action<Color> BoundsColorRequested;

    [Tooltip("Called when the bounds visibility toggle changes.")]
    public Action<bool> ShowBoundsRequested;

    [Tooltip("Called when the Cell Size slider changes (drives the grid + spatial hash).")]
    public Action<float> CellSizeRequested;

    [Tooltip("Returns the current grid color.")]
    public Func<Color> GridColorProvider;

    [Tooltip("Called when the grid color changes.")]
    public Action<Color> GridColorRequested;

    [Tooltip("Called when the grid visibility toggle changes.")]
    public Action<bool> ShowGridRequested;

    [Tooltip("Escape hatch for attach-style PanelRenderer APIs.")]
    public VisualElement ExternalRoot;

    private FlockingSystem _target;
    private UIDocument _document;
    private VisualElement _panelRoot;
    private VisualElement _menuBar;
    private VisualElement _menuDropdown;
    private ImGuiWindow _mainWindow, _viewWindow, _statsWindow;
    private Label _boidValue, _fpsValue, _frameValue;
    private Coroutine _buildRoutine;
    private bool _rootHooksInstalled;

    private float _avgDt = 1f / 60f;
    private float _statsTimer;

    private static readonly Color FpsGood = new Color(0.56f, 0.83f, 0.26f);
    private static readonly Color FpsWarn = new Color(0.98f, 0.78f, 0.22f);
    private static readonly Color FpsBad = new Color(0.94f, 0.33f, 0.33f);

    private readonly List<Control> _controls = new List<Control>();

    private struct Control
    {
        public ImGuiSliderFloat Slider;
        public Func<float> Get; // source of truth for rebind refresh (null = never refreshed)
        public Action<float> Set;
        public float Initial;
    }

    // ------------------------------------------------------------------ lifecycle

    private void Awake()
    {
        if (panelRenderer == null) panelRenderer = GetComponent<PanelRenderer>();
        if (theme == null) theme = Resources.Load<StyleSheet>("DearImGui");
        _document = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        if (toggleAction != null)
        {
            toggleAction.action.Enable(); // remove if its action map is enabled elsewhere
            toggleAction.action.performed += OnTogglePerformed;
        }

        if (_mainWindow == null) TryBuild();
    }

    private void OnDisable()
    {
        if (toggleAction != null)
            toggleAction.action.performed -= OnTogglePerformed;

        if (_buildRoutine != null)
        {
            StopCoroutine(_buildRoutine);
            _buildRoutine = null;
        }
    }

    private void Start()
    {
        if (_mainWindow == null) TryBuild();
    }

    private void OnTogglePerformed(InputAction.CallbackContext _)
    {
        if (_mainWindow != null) _mainWindow.Toggle(); // hotkey toggles the main window
    }

    private void Update()
    {
        float dt = Mathf.Max(Time.unscaledDeltaTime, 1e-5f);
        _avgDt = Mathf.Lerp(_avgDt, dt, 0.08f);

        if (_statsWindow == null || _fpsValue == null) return;
        if (_statsWindow.resolvedStyle.display == DisplayStyle.None) return;

        _statsTimer += dt;
        if (_statsTimer < 0.2f) return;
        _statsTimer = 0f;

        int fps = Mathf.RoundToInt(1f / _avgDt);
        _fpsValue.text = fps.ToString();
        _fpsValue.style.color = fps >= 60 ? FpsGood : (fps >= 30 ? FpsWarn : FpsBad);
        _frameValue.text = (_avgDt * 1000f).ToString("0.0") + " ms";

        if (BoidCountProvider != null)
            _boidValue.text = BoidCountProvider().ToString("N0");
    }

    public void Init(FlockingSystem flockingSystem) => Bind(flockingSystem);

    /// <summary>Point the panel at a FlockingSystem instance.</summary>
    public void Bind(FlockingSystem target)
    {
        _target = target;
        if (target == null) return;

        if (_built)
        {
            RefreshTargetValues(); // rebind WITHOUT rebuilding — layout is preserved
            return;
        }

        TryBuild();
    }

    private void RefreshTargetValues()
    {
        foreach (var c in _controls)
        {
            if (c.Get == null) continue; // e.g. color components — kept by Gameplay, not reset
            float v = c.Get();
            c.Slider.SetValueWithoutNotify(v);
        }
    }

    private void ToggleMenu()
    {
        if (_menuDropdown == null) return;
        bool open = _menuDropdown.style.display == DisplayStyle.Flex;
        _menuDropdown.style.display = open ? DisplayStyle.None : DisplayStyle.Flex;
    }

    private void HideMenu()
    {
        if (_menuDropdown != null)
            _menuDropdown.style.display = DisplayStyle.None;
    }

    // ------------------------------------------------------------------ panel bootstrapping

    private void TryBuild()
    {
        if (_built || _buildRoutine != null) return;

        if (_panelRoot == null) _panelRoot = ResolveRoot();

        if (_panelRoot != null && _target != null)
        {
            BuildPanel(_panelRoot);
            return;
        }

        _buildRoutine = StartCoroutine(BuildWhenPanelReady());
    }

    private IEnumerator BuildWhenPanelReady()
    {
        const float timeout = 5f;
        float deadline = Time.realtimeSinceStartup + timeout;

        while (Time.realtimeSinceStartup < deadline)
        {
            if (_panelRoot == null) _panelRoot = ResolveRoot();
            if (_panelRoot != null && _target != null) break;
            yield return null;
        }

        _buildRoutine = null;

        if (_panelRoot == null)
        {
            Debug.LogError(
                "[FlockingUI] Could not obtain a panel root.\n" +
                "1. Right-click the FlockingUI component header -> 'Dump PanelRenderer API'.\n" +
                "2. If your PanelRenderer needs a Visual Tree Asset, assign any UXML to it.\n" +
                "3. Or add a plain UIDocument, or set ExternalRoot from code.");
            yield break;
        }

        if (_target == null)
        {
            Debug.LogWarning("[FlockingUI] Panel ready, but Bind()/Init() was never called.");
            yield break;
        }

        if (_built) yield break;

        BuildPanel(_panelRoot);
    }

    private VisualElement ResolveRoot()
    {
        if (ExternalRoot != null) return ExternalRoot;

        if (panelRenderer != null)
        {
            var ve = FindVisualElementMember(panelRenderer);
            if (ve != null) return ve;
        }

        if (_document == null) _document = GetComponent<UIDocument>();
        if (_document != null) return _document.rootVisualElement;

        return null;
    }

    private static VisualElement FindVisualElementMember(object owner)
    {
        if (owner == null) return null;
        var type = owner.GetType();

        foreach (var flags in new[]
                 {
                     BindingFlags.Public | BindingFlags.Instance,
                     BindingFlags.NonPublic | BindingFlags.Instance
                 })
        {
            foreach (var prop in type.GetProperties(flags))
            {
                if (prop.GetIndexParameters().Length != 0 ||
                    !typeof(VisualElement).IsAssignableFrom(prop.PropertyType))
                    continue;

                try
                {
                    if (prop.GetValue(owner) is VisualElement ve) return ve;
                }
                catch
                {
                    /* property may throw before the panel exists */
                }
            }

            foreach (var field in type.GetFields(flags))
                if (typeof(VisualElement).IsAssignableFrom(field.FieldType) &&
                    field.GetValue(owner) is VisualElement ve)
                    return ve;
        }

        return null;
    }

    // ------------------------------------------------------------------ diagnostics

    [ContextMenu("Dump PanelRenderer API")]
    private void DumpPanelRendererApi()
    {
        if (panelRenderer == null)
        {
            Debug.Log("[FlockingUI] panelRenderer is not assigned and not found on this GameObject.");
            return;
        }

        var type = panelRenderer.GetType();
        Debug.Log($"[FlockingUI] {type.FullName}  (assembly: {type.Assembly.GetName().Name})");

        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
        foreach (var p in type.GetProperties(flags))
            Debug.Log($"    property   {p.PropertyType.Name} {p.Name}");
        foreach (var f in type.GetFields(flags))
            Debug.Log($"    field      {f.FieldType.Name} {f.Name}");

        foreach (var m in type.GetMethods(flags))
        {
            if (m.IsSpecialName) continue;
            if (m.DeclaringType == typeof(MonoBehaviour) || m.DeclaringType == typeof(Behaviour) ||
                m.DeclaringType == typeof(Component) || m.DeclaringType == typeof(UnityEngine.Object))
                continue;
            var pars = string.Join(", ", m.GetParameters().Select(pp => $"{pp.ParameterType.Name} {pp.Name}"));
            Debug.Log($"    method     {m.ReturnType.Name} {m.Name}({pars})");
        }
    }

    // ------------------------------------------------------------------ build

    private void BuildPanel(VisualElement root)
    {
        _panelRoot = root;
        _controls.Clear();
        root.Clear();
        root.pickingMode = PickingMode.Ignore; // clicks fall through outside UI

        if (theme != null && !root.styleSheets.Contains(theme))
            root.styleSheets.Add(theme);

        // click-away closes the open dropdown (registered once; root persists across rebuilds)
        if (!_rootHooksInstalled)
        {
            root.RegisterCallback<PointerDownEvent>(e =>
            {
                if (_menuDropdown == null) return;
                if (_menuDropdown.style.display == DisplayStyle.Flex &&
                    !(_menuBar.Contains(e.target as VisualElement)))
                    HideMenu();
            }, TrickleDown.TrickleDown);
            _rootHooksInstalled = true;
        }

        BuildMenuBar(root);

        // ---- windows ----
        _mainWindow = new ImGuiWindow("Boids Controls", 300f, 16f, 30f, theme);
        root.Add(_mainWindow);
        BuildMainContent(_mainWindow.Content);
        RegisterWindow(_mainWindow, "Boids Controls");

        _viewWindow = new ImGuiWindow("View", 260f, 332f, 30f, theme);
        root.Add(_viewWindow);
        BuildViewContent(_viewWindow.Content);
        RegisterWindow(_viewWindow, "View");

        _statsWindow = new ImGuiWindow("Stats", 180f, 608f, 30f, theme);
        root.Add(_statsWindow);
        BuildStatsContent(_statsWindow.Content);
        RegisterWindow(_statsWindow, "Stats");
        _built = true;
    }

    private void BuildMenuBar(VisualElement root)
    {
        _menuBar = new VisualElement();
        _menuBar.AddToClassList("imgui-menubar");
        root.Add(_menuBar);

        var windowsItem = new VisualElement();
        windowsItem.AddToClassList("imgui-menubar__item");
        var windowsLabel = new Label("Windows");
        windowsLabel.pickingMode = PickingMode.Ignore;
        windowsItem.Add(windowsLabel);

        _menuDropdown = new VisualElement();
        _menuDropdown.AddToClassList("imgui-menu");
        _menuDropdown.style.display = DisplayStyle.None;
        windowsItem.Add(_menuDropdown);

        _menuBar.Add(windowsItem);

        windowsItem.RegisterCallback<PointerDownEvent>(e =>
        {
            e.StopPropagation();
            ToggleMenu();
        });
    }

    /// <summary>Adds a "Windows" menu entry bound to a window, with a synced checkmark.</summary>
    private void RegisterWindow(ImGuiWindow window, string menuTitle)
    {
        var entry = new VisualElement();
        entry.AddToClassList("imgui-menu__item");

        var label = new Label(menuTitle);
        label.pickingMode = PickingMode.Ignore;

        var check = new Label("✓");
        check.AddToClassList("imgui-menu__check");
        check.pickingMode = PickingMode.Ignore;

        entry.Add(label);
        entry.Add(check);

        entry.RegisterCallback<PointerDownEvent>(e =>
        {
            e.StopPropagation();
            window.Toggle();
            HideMenu();
        });

        window.VisibilityChanged += v => check.text = v ? "✓" : "";

        _menuDropdown.Add(entry);
    }

    // ------------------------------------------------------------------ window contents

    private void BuildMainContent(VisualElement content)
    {
        var sim = new ImGuiCollapsingHeader("Simulation");
        content.Add(sim);
        var cellSlider = AddSlider(sim.Body, "Cell Size", 0.1f, 5f, () => _target.cellSize, v => _target.cellSize = v);
        cellSlider.ValueChanged += v => CellSizeRequested?.Invoke(v);
        AddSlider(sim.Body, "Min Speed", 0f, 50f, () => _target.boidMinSpeed, v => _target.boidMinSpeed = v);
        AddSlider(sim.Body, "Max Speed", 0f, 50f, () => _target.boidMaxSpeed, v => _target.boidMaxSpeed = v);

        int countInitial = BoidCountProvider != null ? BoidCountProvider() : 1000;
        var countSlider = new ImGuiSliderFloat("Boid Count", 100f, 300000f,
            countInitial, countInitial, "N0", step: 100f);
        countSlider.Committed += v => BoidCountRequested?.Invoke(Mathf.RoundToInt(v));
        sim.Body.Add(countSlider);

        Separator(content);

        var forces = new ImGuiCollapsingHeader("Forces");
        content.Add(forces);
        AddSlider(forces.Body, "Cohesion", 0f, 3f, () => _target.cohesionStrength, v => _target.cohesionStrength = v);
        AddSlider(forces.Body, "Align Range", 0f, 10f, () => _target.alignmentRange, v => _target.alignmentRange = v);
        AddSlider(forces.Body, "Align Force", 0f, 3f, () => _target.alignmentStrength,
            v => _target.alignmentStrength = v);
        AddSlider(forces.Body, "Avoid Range", 0f, 5f, () => _target.avoidanceRange, v => _target.avoidanceRange = v);
        AddSlider(forces.Body, "Avoid Force", 0f, 3f, () => _target.avoidanceStrength,
            v => _target.avoidanceStrength = v);

        var reset = new Button(ResetToInitial) { text = "Reset Defaults" };
        reset.AddToClassList("imgui-button");
        content.Add(reset);
    }

    private void BuildViewContent(VisualElement content)
    {
        float zoomInitial = CameraZoomProvider != null ? CameraZoomProvider() : 25f;
        var zoomSlider = new ImGuiSliderFloat("Camera Zoom", 5f, 600, zoomInitial, zoomInitial, "0.0");
        zoomSlider.ValueChanged += v => CameraZoomRequested?.Invoke(v);
        content.Add(zoomSlider);

        // Simulation bounds size — independent of camera zoom
        if (SimWidthProvider != null && SimWidthRequested != null)
        {
            float w0 = SimWidthProvider();
            var ws = new ImGuiSliderFloat("Sim Width", 5f, 1200, w0, w0, "0", 1f);
            ws.ValueChanged += v => SimWidthRequested?.Invoke(v);
            content.Add(ws);
        }

        if (SimHeightProvider != null && SimHeightRequested != null)
        {
            float h0 = SimHeightProvider();
            var hs = new ImGuiSliderFloat("Sim Height", 5f, 1200, h0, h0, "0", 1f);
            hs.ValueChanged += v => SimHeightRequested?.Invoke(v);
            content.Add(hs);
        }

        // Bounds visibility + color
        if (BoundsColorProvider != null && BoundsColorRequested != null)
        {
            Separator(content);

            var showToggle = new ImGuiToggle("Show Bounds", true);
            showToggle.ValueChanged += on => ShowBoundsRequested?.Invoke(on);
            content.Add(showToggle);

            AddColorEdit(content, "Bounds Color", BoundsColorProvider(), c => BoundsColorRequested(c));
        }

        // Grid (cell size visualization)
        if (GridColorProvider != null && GridColorRequested != null)
        {
            Separator(content);

            var gridToggle = new ImGuiToggle("Show Grid", true);
            gridToggle.ValueChanged += on => ShowGridRequested?.Invoke(on);
            content.Add(gridToggle);

            AddColorEdit(content, "Grid Color", GridColorProvider(), c => GridColorRequested(c));
        }
    }

    private void BuildStatsContent(VisualElement content)
    {
        _boidValue = AddStatRow(content, "Boids");
        _fpsValue = AddStatRow(content, "FPS");
        _frameValue = AddStatRow(content, "Frame");
        if (BoidCountProvider != null)
            _boidValue.text = BoidCountProvider().ToString("N0");
    }

    // ------------------------------------------------------------------ widgets

    private ImGuiSliderFloat AddSlider(VisualElement parent, string label, float min, float max,
        Func<float> get, Action<float> set, string format = "0.00")
    {
        float initial = get();
        var slider = new ImGuiSliderFloat(label, min, max, initial, initial, format);
        slider.ValueChanged += set;
        parent.Add(slider);
        _controls.Add(new Control { Slider = slider, Set = set, Get = get, Initial = initial });
        return slider;
    }

    private static Label AddStatRow(VisualElement parent, string label)
    {
        var row = new VisualElement();
        row.AddToClassList("imgui-stat");

        var l = new Label(label);
        l.AddToClassList("imgui-stat__label");

        var v = new Label("–");
        v.AddToClassList("imgui-stat__value");

        row.Add(l);
        row.Add(v);
        parent.Add(row);
        return v;
    }

    private static void Separator(VisualElement parent)
    {
        var sep = new VisualElement();
        sep.AddToClassList("imgui-separator");
        parent.Add(sep);
    }

    /// ImGui-style ColorEdit: R/G/B/A sliders (0–255) + live swatch. Registered in _controls
    /// so "Reset Defaults" restores it.
    private void AddColorEdit(VisualElement parent, string label, Color initial, Action<Color> onChanged)
    {
        var col = initial;

        var swatch = new VisualElement();
        swatch.AddToClassList("imgui-color__swatch");
        swatch.style.backgroundColor = col;
        swatch.pickingMode = PickingMode.Ignore;

        var row = new VisualElement();
        row.AddToClassList("imgui-color");
        var l = new Label(label);
        l.AddToClassList("imgui-color__label");
        l.pickingMode = PickingMode.Ignore;
        row.Add(l);
        row.Add(swatch);
        parent.Add(row);

        void Apply()
        {
            onChanged(col);
            swatch.style.backgroundColor = col;
        }

        void AddComponent(string name, float value, Action<float> assign)
        {
            var s = new ImGuiSliderFloat(name, 0f, 255f, value, value, "0", 1f);
            s.ValueChanged += v =>
            {
                assign(v);
                Apply();
            };
            parent.Add(s);
            _controls.Add(new Control
            {
                Slider = s, Set = v =>
                {
                    assign(v);
                    Apply();
                },

                Initial = value
            });
        }

        AddComponent("R", col.r * 255f, v => col.r = v / 255f);
        AddComponent("G", col.g * 255f, v => col.g = v / 255f);
        AddComponent("B", col.b * 255f, v => col.b = v / 255f);
        AddComponent("A", col.a * 255f, v => col.a = v / 255f);
    }

    private void ResetToInitial()
    {
        if (_target == null) return;

        foreach (var c in _controls)
        {
            c.Set(c.Initial);
            c.Slider.SetValueWithoutNotify(c.Initial);
        }
    }

    // ------------------------------------------------------------------ custom ImGui widgets

    /// <summary>
    /// ImGui-style draggable window: title bar with collapse ("−"/"+") and close ("×")
    /// buttons, scrollable body. Visibility changes are reported via VisibilityChanged
    /// so the menu checkmark stays in sync.
    /// </summary>
    private class ImGuiWindow : VisualElement
    {
        public VisualElement Content { get; }
        public event Action<bool> VisibilityChanged;

        private readonly VisualElement _body;

        public ImGuiWindow(string title, float width, float x, float y, StyleSheet theme)
        {
            AddToClassList("imgui");
            style.width = width;
            style.left = x;
            style.top = y;
            if (theme != null) styleSheets.Add(theme);

            var titleBar = new VisualElement();
            titleBar.AddToClassList("imgui__titlebar");

            var t = new Label(title);
            t.AddToClassList("imgui__title");
            t.pickingMode = PickingMode.Ignore;
            titleBar.Add(t);

            Button collapse = null;
            collapse = new Button(() =>
            {
                bool showing = _body.style.display == DisplayStyle.Flex;
                _body.style.display = showing ? DisplayStyle.None : DisplayStyle.Flex;
                collapse.text = showing ? "+" : "-";
            }) { text = "-" };
            collapse.AddToClassList("imgui__titlebtn");
            titleBar.Add(collapse);

            var close = new Button(() => SetVisible(false)) { text = "×" };
            close.AddToClassList("imgui__titlebtn");
            titleBar.Add(close);

            Add(titleBar);

            var scroll = new ScrollView();
            scroll.AddToClassList("imgui__scroll");
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
            Add(scroll);

            Content = new VisualElement();
            Content.AddToClassList("imgui__content");
            scroll.Add(Content);
            _body = scroll;

            // click title bar: raise this window above its siblings (ImGui focus behavior), then drag
            titleBar.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button == 0) BringToFront();
            });

            MakeDraggable(titleBar, this);
        }

        public bool IsVisible => resolvedStyle.display != DisplayStyle.None;

        public void SetVisible(bool v)
        {
            style.display = v ? DisplayStyle.Flex : DisplayStyle.None;
            VisibilityChanged?.Invoke(v);
        }

        public void Toggle() => SetVisible(!IsVisible);
    }

    /// <summary>
    /// ImGui-style SliderFloat: "Label" and value are drawn on top of the frame; the grab
    /// fills from the left. Drag anywhere; double-click resets; optional stepping;
    /// 'Committed' fires once on drag release (used for expensive changes like boid count).
    /// </summary>
    private class ImGuiSliderFloat : VisualElement
    {
        public event Action<float> ValueChanged;
        public event Action<float> Committed;

        private readonly VisualElement _fill;
        private readonly Label _valueLabel;
        private readonly float _min, _max, _default, _step;
        private readonly string _format;
        private float _value;

        public ImGuiSliderFloat(string label, float min, float max, float value,
            float defaultValue, string format = "0.00", float step = 0f)
        {
            _min = min;
            _max = max;
            _default = defaultValue;
            _format = format;
            _step = step;

            AddToClassList("imgui-slider");

            _fill = new VisualElement();
            _fill.AddToClassList("imgui-slider__fill");
            _fill.pickingMode = PickingMode.Ignore;
            Add(_fill);

            var nameLabel = new Label(label);
            nameLabel.AddToClassList("imgui-slider__name");
            nameLabel.pickingMode = PickingMode.Ignore;
            Add(nameLabel);

            _valueLabel = new Label();
            _valueLabel.AddToClassList("imgui-slider__val");
            _valueLabel.pickingMode = PickingMode.Ignore;
            Add(_valueLabel);

            SetValueWithoutNotify(value);

            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerCancelEvent>(OnPointerCancel);
        }

        private void OnPointerDown(PointerDownEvent e)
        {
            if (e.button != 0) return;

            if (e.clickCount == 2) // double-click resets to default
            {
                SetValue(_default);
                Committed?.Invoke(_value);
                e.StopPropagation();
                return;
            }

            this.CapturePointer(e.pointerId);
            UpdateFromWorld((Vector2)e.position);
            e.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent e)
        {
            if (!this.HasPointerCapture(e.pointerId)) return;
            UpdateFromWorld((Vector2)e.position);
        }

        private void OnPointerUp(PointerUpEvent e)
        {
            if (!this.HasPointerCapture(e.pointerId)) return;
            this.ReleasePointer(e.pointerId);
            Committed?.Invoke(_value);
        }

        private void OnPointerCancel(PointerCancelEvent e)
        {
            if (this.HasPointerCapture(e.pointerId))
                this.ReleasePointer(e.pointerId);
        }

        private void UpdateFromWorld(Vector2 worldPos)
        {
            var rect = worldBound;
            float t = Mathf.Clamp01((worldPos.x - rect.x) / Mathf.Max(1f, rect.width));
            SetValue(Mathf.Lerp(_min, _max, t));
        }

        public void SetValue(float v)
        {
            SetValueWithoutNotify(v);
            ValueChanged?.Invoke(_value);
        }

        public void SetValueWithoutNotify(float v)
        {
            _value = Mathf.Clamp(v, _min, _max);
            if (_step > 0f)
                _value = _min + Mathf.Round((_value - _min) / _step) * _step;

            float t = _max > _min ? (_value - _min) / (_max - _min) : 0f;
            _fill.style.width = new Length(t * 100f, LengthUnit.Percent);
            _valueLabel.text = _value.ToString(_format);
        }
    }

    /// <summary>ImGui-style collapsing header with a ▾/▸ arrow and a plain body.</summary>
    private class ImGuiCollapsingHeader : VisualElement
    {
        public readonly VisualElement Body;
        private readonly Label _arrow;

        public ImGuiCollapsingHeader(string titleText, bool open = true)
        {
            AddToClassList("imgui-header-group");

            var header = new VisualElement();
            header.AddToClassList("imgui-header");

            _arrow = new Label(open ? "▾" : "▸");
            _arrow.AddToClassList("imgui-header__arrow");
            _arrow.pickingMode = PickingMode.Ignore;

            var text = new Label(titleText);
            text.AddToClassList("imgui-header__title");
            text.pickingMode = PickingMode.Ignore;

            header.Add(_arrow);
            header.Add(text);
            Add(header);

            Body = new VisualElement();
            Body.AddToClassList("imgui-header__body");
            Body.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            Add(Body);

            header.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0) return;
                bool show = Body.style.display == DisplayStyle.None;
                Body.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                _arrow.text = show ? "▾" : "▸";
            });
        }
    }

    /// <summary>ImGui-style checkbox: square frame with an x mark + label.</summary>
    private class ImGuiToggle : VisualElement
    {
        public event Action<bool> ValueChanged;

        private readonly VisualElement _box;
        private readonly Label _mark;
        private bool _value;

        public ImGuiToggle(string label, bool value)
        {
            _value = value;
            AddToClassList("imgui-toggle");

            _box = new VisualElement();
            _box.AddToClassList("imgui-toggle__box");
            _box.EnableInClassList("imgui-toggle__box--on", _value);

            _mark = new Label(_value ? "x" : "");
            _mark.AddToClassList("imgui-toggle__mark");
            _mark.pickingMode = PickingMode.Ignore;
            _box.Add(_mark);

            var text = new Label(label);
            text.AddToClassList("imgui-toggle__text");
            text.pickingMode = PickingMode.Ignore;

            Add(_box);
            Add(text);

            RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0) return;
                SetValue(!_value);
                e.StopPropagation();
            });
        }

        public void SetValue(bool v)
        {
            _value = v;
            _mark.text = v ? "x" : "";
            _box.EnableInClassList("imgui-toggle__box--on", v);
            ValueChanged?.Invoke(v);
        }
    }

    // ------------------------------------------------------------------ dragging

    private static void MakeDraggable(VisualElement handle, VisualElement target)
    {
        Vector2 startPointer = default;
        Vector2 startPos = default;
        bool dragging = false;

        handle.RegisterCallback<PointerDownEvent>(e =>
        {
            if (e.button != 0) return;
            dragging = true;
            startPointer = (Vector2)e.position;
            startPos = new Vector2(target.resolvedStyle.left, target.resolvedStyle.top);
            handle.CapturePointer(e.pointerId);
        });

        handle.RegisterCallback<PointerMoveEvent>(e =>
        {
            if (!dragging) return;
            var delta = (Vector2)e.position - startPointer;
            target.style.left = startPos.x + delta.x;
            target.style.top = startPos.y + delta.y;
        });

        handle.RegisterCallback<PointerUpEvent>(e =>
        {
            dragging = false;
            if (handle.HasPointerCapture(e.pointerId)) handle.ReleasePointer(e.pointerId);
        });

        handle.RegisterCallback<PointerCancelEvent>(e =>
        {
            dragging = false;
            if (handle.HasPointerCapture(e.pointerId)) handle.ReleasePointer(e.pointerId);
        });
    }
}