using System.Collections.Generic;
using TerrainDemo.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace TerrainDemo
{
    [RequireComponent(typeof(UIDocument))]
    public class TerrainDemoUI : MonoBehaviour
    {
        [SerializeField] private TerrainGenerator generator;

        [SerializeField] private StyleSheet panelStyleSheet;

        public TerrainGenerator Generator
        {
            get => generator;
            set => generator = value;
        }

        public StyleSheet PanelStyleSheet
        {
            get => panelStyleSheet;
            set => panelStyleSheet = value;
        }

        private const float RegenerateIntervalSeconds = 0.3f;

        private const string PresetSelectedClass = "preset-selected";

        private const string TextInputClass = "unity-base-text-field";

        public static bool IsTyping { get; private set; }

        private VisualElement _root;

        private IntegerField _seedField;
        private Button _hillsButton;
        private Button _peaksButton;
        private Button _mesasButton;
        private SliderInt _resolutionSlider;
        private Slider _heightScaleSlider;
        private Slider _scaleSlider;
        private SliderInt _octavesSlider;
        private Slider _lacunaritySlider;
        private Slider _persistenceSlider;
        private Slider _offsetSlider;
        private Slider _gainSlider;
        private Slider _exponentSlider;
        private SliderInt _terraceSlider;
        private Label _statsLabel;
        private SliderInt _colorCountSlider;
        private ScrollView _colorList;

        private Label _tooltip;
        private VisualElement _tooltipTarget;
        private IVisualElementScheduledItem _tooltipTimer;
        private Vector2 _tooltipPosition;
        private Button _viewButton;
        private TerrainWireframe _wireframe;

        private const long TooltipDelayMs = 400;

        private static readonly Dictionary<string, string> Descriptions = new Dictionary<string, string>
        {
            { "seed-field", "The number that drives all randomness. The same seed with the same settings always rebuilds exactly the same terrain." },
            { "random-button", "Pick a new random seed and regenerate the terrain." },
            { "preset-hills", "Rolling Hills: few noise layers and gentle heights for soft, wide hills. Your colour settings are kept." },
            { "preset-peaks", "Rugged Peaks: multifractal noise where high ground gets rougher while valleys stay smooth. Your colour settings are kept." },
            { "preset-mesas", "Terraced Mesas: heights snapped into flat steps to form plateaus. Your colour settings are kept." },
            { "resolution-slider", "Number of vertices along each side of the terrain. Higher values show finer detail but take longer, because the cost grows with the square." },
            { "height-scale-slider", "Height of the highest possible point, in metres." },
            { "scale-slider", "Size of the largest landforms, in metres. Larger values give broader, gentler hills; smaller values give tighter, busier terrain." },
            { "octaves-slider", "Number of noise layers stacked on top of each other. More octaves add finer detail but cost more time." },
            { "lacunarity-slider", "How much finer each noise layer is than the previous one. Higher values add small details faster." },
            { "persistence-slider", "How strong each finer layer is compared with the previous one. Higher values make the surface rougher and noisier." },
            { "offset-slider", "Rugged Peaks only. Lifts the noise before the altitude feedback. Higher values give fuller, bulkier mountains; lower values give broken, patchy ground." },
            { "gain-slider", "Rugged Peaks only. Strength of the altitude feedback. Higher values make peaks rugged while low ground stays smooth." },
            { "exponent-slider", "Reshapes the height curve. Above 1 flattens valleys and sharpens peaks; below 1 does the opposite." },
            { "terrace-slider", "Snaps heights into this many flat steps to make terraces. 0 turns it off." },
            { "generate-button", "Rebuild the whole terrain now with the current settings." },
            { "view-mode-button", "Switch the view: shaded surface, shaded surface with wireframe, or wireframe only." },
            { "color-count-slider", "How many colour layers are used. Layers lower in the list are painted on top of layers above them." },
        };

        private const string PickerDescription = "Click the swatch to open the colour picker: drag in the square for brightness and saturation, drag the strip for hue, or click a quick colour. You can also type a hex code such as #4C8033.";
        private const string HeightRangeDescription = "Height range where this colour appears, from 0 (lowest point) to 1 (highest point). Drag either end.";
        private const string SlopeRangeDescription = "Slope range where this colour appears, from 0 (flat) to 1 (vertical cliff). Drag either end.";

        private sealed class ColorCard
        {
            public VisualElement Root;
            public ColorPickerField Picker;
            public MinMaxSlider Height;
            public MinMaxSlider Slope;
        }

        private readonly List<ColorCard> _colorCards = new List<ColorCard>();

        private sealed class Binding
        {
            public readonly System.Action<TerrainParams> ToParams;
            public readonly System.Action<TerrainParams> ToControl;

            public Binding(System.Action<TerrainParams> toParams, System.Action<TerrainParams> toControl)
            {
                ToParams = toParams;
                ToControl = toControl;
            }
        }

        private readonly List<Binding> _bindings = new List<Binding>();

        private bool _dirty;
        private float _lastGenerateTime;
        private float _fps;

        private void OnEnable()
        {
            UIDocument document = GetComponent<UIDocument>();
            VisualElement root = document != null ? document.rootVisualElement : null;
            if (root == null) return;
            if (generator == null) generator = FindFirstObjectByType<TerrainGenerator>();
            if (panelStyleSheet != null) root.styleSheets.Add(panelStyleSheet);

            if (!QueryControls(root)) return;
            _root = root;
            root.RegisterCallback<NavigationMoveEvent>(evt => evt.StopPropagation(), TrickleDown.TrickleDown);
            BuildColorCards();
            WireSpecialControls(root);
            WireViewMode();
            WireTooltips(root);
            if (generator != null) generator.Parameters.EnsureColorLayers();
            BuildBindings();
            if (generator != null) CopyParametersToControls(generator.Parameters);
        }

        private void Update()
        {
            UpdateFocus();
            if (generator == null) return;

            if (_dirty && Time.unscaledTime - _lastGenerateTime >= RegenerateIntervalSeconds)
            {
                SyncParametersFromControls();
                generator.Generate();
                _dirty = false;
                _lastGenerateTime = Time.unscaledTime;
            }

            if (_statsLabel != null)
            {
                float dt = Time.unscaledDeltaTime;
                if (dt > 0f) _fps = Mathf.Lerp(_fps, 1f / dt, 0.05f);
                _statsLabel.text = generator.LastStageStats + " | FPS " + _fps.ToString("F0");
            }
        }

        private void UpdateFocus()
        {
            FocusController focusController = _root?.focusController;
            if (focusController == null)
            {
                IsTyping = false;
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.wasPressedThisFrame)
            {
                focusController.focusedElement?.Blur();
            }

            IsTyping = false;
            for (VisualElement e = focusController.focusedElement as VisualElement; e != null; e = e.parent)
            {
                if (e.ClassListContains(TextInputClass))
                {
                    IsTyping = true;
                    break;
                }
            }
        }

        private void OnDisable()
        {
            IsTyping = false;
            if (_wireframe != null) _wireframe.ModeChanged -= UpdateViewButton;
        }

        private bool QueryControls(VisualElement root)
        {
            _seedField = root.Q<IntegerField>("seed-field");
            _hillsButton = root.Q<Button>("preset-hills");
            _peaksButton = root.Q<Button>("preset-peaks");
            _mesasButton = root.Q<Button>("preset-mesas");
            _resolutionSlider = root.Q<SliderInt>("resolution-slider");
            _heightScaleSlider = root.Q<Slider>("height-scale-slider");
            _scaleSlider = root.Q<Slider>("scale-slider");
            _octavesSlider = root.Q<SliderInt>("octaves-slider");
            _lacunaritySlider = root.Q<Slider>("lacunarity-slider");
            _persistenceSlider = root.Q<Slider>("persistence-slider");
            _offsetSlider = root.Q<Slider>("offset-slider");
            _gainSlider = root.Q<Slider>("gain-slider");
            _exponentSlider = root.Q<Slider>("exponent-slider");
            _terraceSlider = root.Q<SliderInt>("terrace-slider");
            _statsLabel = root.Q<Label>("stats-label");
            _colorCountSlider = root.Q<SliderInt>("color-count-slider");
            _colorList = root.Q<ScrollView>("color-list");
            _tooltip = root.Q<Label>("tooltip-label");
            _viewButton = root.Q<Button>("view-mode-button");

            bool complete = _seedField != null
                && _colorCountSlider != null && _colorList != null
                && _hillsButton != null && _peaksButton != null && _mesasButton != null
                && _resolutionSlider != null && _heightScaleSlider != null
                && _scaleSlider != null && _octavesSlider != null && _lacunaritySlider != null
                && _persistenceSlider != null && _offsetSlider != null && _gainSlider != null
                && _exponentSlider != null && _terraceSlider != null && _statsLabel != null;
            if (!complete)
            {
                Debug.LogWarning("[TerrainDemoUI] Expected UXML elements are missing — check the UIDocument's visualTreeAsset.");
            }
            return complete;
        }

        private void BuildBindings()
        {
            _bindings.Clear();

            Bind(_seedField, (p, v) => p.Seed = v, p => p.Seed);
            Bind(_resolutionSlider, (p, v) => p.Resolution = v, p => p.Resolution);
            Bind(_heightScaleSlider, (p, v) => p.HeightScale = v, p => p.HeightScale);
            Bind(_scaleSlider, (p, v) => p.Scale = v, p => p.Scale);
            Bind(_octavesSlider, (p, v) => p.Octaves = v, p => p.Octaves);
            Bind(_lacunaritySlider, (p, v) => p.Lacunarity = v, p => p.Lacunarity);
            Bind(_persistenceSlider, (p, v) => p.Persistence = v, p => p.Persistence);
            Bind(_offsetSlider, (p, v) => p.Offset = v, p => p.Offset);
            Bind(_gainSlider, (p, v) => p.Gain = v, p => p.Gain);
            Bind(_exponentSlider, (p, v) => p.HeightExponent = v, p => p.HeightExponent);
            Bind(_terraceSlider, (p, v) => p.TerraceLevels = v, p => p.TerraceLevels);

            Bind(_colorCountSlider, (p, v) => p.ColorCount = v, p => p.ColorCount);
            _colorCountSlider.RegisterValueChangedCallback(_ => RefreshColorCards());

            for (int i = 0; i < _colorCards.Count; i++)
            {
                int index = i;
                ColorCard card = _colorCards[i];

                Bind(card.Picker, (p, v) => p.ColorLayers[index].Color = v, p => p.ColorLayers[index].Color);
                Bind(card.Height,
                    (p, v) =>
                    {
                        p.ColorLayers[index].HeightMin = v.x;
                        p.ColorLayers[index].HeightMax = v.y;
                    },
                    p => new Vector2(p.ColorLayers[index].HeightMin, p.ColorLayers[index].HeightMax));
                Bind(card.Slope,
                    (p, v) =>
                    {
                        p.ColorLayers[index].SlopeMin = v.x;
                        p.ColorLayers[index].SlopeMax = v.y;
                    },
                    p => new Vector2(p.ColorLayers[index].SlopeMin, p.ColorLayers[index].SlopeMax));
            }
        }

        private void BuildColorCards()
        {
            _colorList.Clear();
            _colorCards.Clear();

            for (int i = 0; i < TerrainParams.MaxColorLayers; i++)
            {
                var card = new ColorCard
                {
                    Root = new VisualElement(),
                    Picker = new ColorPickerField(),
                    Height = new MinMaxSlider("Height", 0f, 1f, 0f, 1f),
                    Slope = new MinMaxSlider("Slope", 0f, 1f, 0f, 1f),
                };
                card.Picker.ExpandRequested += TogglePicker;

                var header = new VisualElement();
                header.AddToClassList("color-card-header");
                var title = new Label($"Color {i + 1}");
                title.AddToClassList("color-card-title");
                header.Add(title);

                card.Root.AddToClassList("color-card");
                card.Root.Add(header);
                card.Root.Add(card.Picker);
                card.Root.Add(card.Height);
                card.Root.Add(card.Slope);

                _colorList.Add(card.Root);
                _colorCards.Add(card);
            }
        }

        private void RefreshColorCards()
        {
            int count = _colorCountSlider.value;
            for (int i = 0; i < _colorCards.Count; i++)
            {
                _colorCards[i].Root.style.display = i < count ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void TogglePicker(ColorPickerField source)
        {
            bool open = !source.Expanded;
            foreach (ColorCard card in _colorCards) card.Picker.SetExpanded(card.Picker == source && open);
        }

        private void WireViewMode()
        {
            if (_viewButton == null) return;
            if (generator == null)
            {
                _viewButton.SetEnabled(false);
                return;
            }
            _wireframe = generator.GetComponent<TerrainWireframe>();
            if (_wireframe == null) _wireframe = generator.gameObject.AddComponent<TerrainWireframe>();
            _wireframe.ModeChanged -= UpdateViewButton;
            _wireframe.ModeChanged += UpdateViewButton;
            _viewButton.clicked += _wireframe.CycleMode;
            UpdateViewButton();
        }

        private void UpdateViewButton()
        {
            if (_viewButton == null || _wireframe == null) return;
            _viewButton.text = _wireframe.Mode switch
            {
                ViewMode.ShadedWireframe => "View: Shaded + Wire",
                ViewMode.Wireframe => "View: Wireframe",
                _ => "View: Shaded",
            };
        }

        private void WireTooltips(VisualElement root)
        {
            if (_tooltip == null) return;
            _tooltip.pickingMode = PickingMode.Ignore;
            _tooltip.style.display = DisplayStyle.None;

            foreach (KeyValuePair<string, string> entry in Descriptions)
            {
                RegisterTooltip(root.Q(entry.Key), entry.Value);
            }
            foreach (ColorCard card in _colorCards)
            {
                RegisterTooltip(card.Picker, PickerDescription);
                RegisterTooltip(card.Height, HeightRangeDescription);
                RegisterTooltip(card.Slope, SlopeRangeDescription);
            }
        }

        private void RegisterTooltip(VisualElement target, string text)
        {
            if (target == null) return;
            target.RegisterCallback<PointerEnterEvent>(e =>
            {
                _tooltipTarget = target;
                _tooltipPosition = e.position;
                _tooltip.text = text;
                _tooltipTimer?.Pause();
                _tooltipTimer = _tooltip.schedule.Execute(ShowTooltip).StartingIn(TooltipDelayMs);
            });
            target.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (_tooltipTarget != target) return;
                _tooltipPosition = e.position;
                if (_tooltip.style.display == DisplayStyle.Flex) PositionTooltip();
            });
            target.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                if (_tooltipTarget == target) HideTooltip();
            });
            target.RegisterCallback<PointerDownEvent>(_ => HideTooltip(), TrickleDown.TrickleDown);
        }

        private void ShowTooltip()
        {
            if (_tooltipTarget == null) return;
            _tooltip.style.display = DisplayStyle.Flex;
            _tooltip.BringToFront();
            PositionTooltip();
        }

        private void HideTooltip()
        {
            _tooltipTimer?.Pause();
            _tooltipTarget = null;
            if (_tooltip != null) _tooltip.style.display = DisplayStyle.None;
        }

        private void PositionTooltip()
        {
            float width = float.IsNaN(_tooltip.layout.width) || _tooltip.layout.width <= 0f ? 260f : _tooltip.layout.width;
            float height = float.IsNaN(_tooltip.layout.height) || _tooltip.layout.height <= 0f ? 60f : _tooltip.layout.height;
            float maxX = _root.layout.width - width - 8f;
            float maxY = _root.layout.height - height - 8f;

            float x = _tooltipPosition.x + 16f;
            float y = _tooltipPosition.y + 18f;
            if (x > maxX) x = _tooltipPosition.x - width - 12f;
            if (y > maxY) y = _tooltipPosition.y - height - 12f;

            _tooltip.style.left = Mathf.Max(8f, x);
            _tooltip.style.top = Mathf.Max(8f, y);
        }

        private void Bind<TValue>(BaseField<TValue> field, System.Action<TerrainParams, TValue> set, System.Func<TerrainParams, TValue> get)
        {
            field.RegisterValueChangedCallback(_ => RequestRegenerate());
            _bindings.Add(new Binding(
                p => set(p, field.value),
                p => field.SetValueWithoutNotify(get(p))));
        }

        private void WireSpecialControls(VisualElement root)
        {
            _hillsButton.clicked += () => ApplyPreset(TerrainParams.RollingHills(), _hillsButton);
            _peaksButton.clicked += () => ApplyPreset(TerrainParams.RuggedPeaks(), _peaksButton);
            _mesasButton.clicked += () => ApplyPreset(TerrainParams.TerracedMesas(), _mesasButton);

            Button randomButton = root.Q<Button>("random-button");
            Button generateButton = root.Q<Button>("generate-button");
            if (randomButton != null)
            {
                randomButton.clicked += () => _seedField.value = Random.Range(0, int.MaxValue);
            }
            if (generateButton != null) generateButton.clicked += ForceRegenerate;
        }

        private void ApplyPreset(TerrainParams preset, Button source)
        {
            if (generator == null) return;
            preset.ColorCount = generator.Parameters.ColorCount;
            preset.ColorLayers = generator.Parameters.ColorLayers;
            preset.EnsureColorLayers();
            generator.Parameters = preset;
            CopyParametersToControls(preset);

            _hillsButton.EnableInClassList(PresetSelectedClass, source == _hillsButton);
            _peaksButton.EnableInClassList(PresetSelectedClass, source == _peaksButton);
            _mesasButton.EnableInClassList(PresetSelectedClass, source == _mesasButton);

            RequestRegenerate();
        }

        private void RequestRegenerate() => _dirty = true;

        private void SyncParametersFromControls()
        {
            TerrainParams p = generator.Parameters;
            foreach (Binding binding in _bindings) binding.ToParams(p);
        }

        private void CopyParametersToControls(TerrainParams p)
        {
            foreach (Binding binding in _bindings) binding.ToControl(p);
            RefreshColorCards();
        }

        private void ForceRegenerate()
        {
            if (generator == null) return;
            SyncParametersFromControls();
            generator.Generate(true);
            _dirty = false;
            _lastGenerateTime = Time.unscaledTime;
        }
    }
}
