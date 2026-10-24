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

        private sealed class ColorCard
        {
            public VisualElement Root;
            public VisualElement Swatch;
            public Slider R;
            public Slider G;
            public Slider B;
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

                Bind(card.R, (p, v) => p.ColorLayers[index].Color.r = v, p => p.ColorLayers[index].Color.r);
                Bind(card.G, (p, v) => p.ColorLayers[index].Color.g = v, p => p.ColorLayers[index].Color.g);
                Bind(card.B, (p, v) => p.ColorLayers[index].Color.b = v, p => p.ColorLayers[index].Color.b);
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

                card.R.RegisterValueChangedCallback(_ => RefreshSwatch(card));
                card.G.RegisterValueChangedCallback(_ => RefreshSwatch(card));
                card.B.RegisterValueChangedCallback(_ => RefreshSwatch(card));
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
                    Swatch = new VisualElement(),
                    R = new Slider("R", 0f, 1f) { showInputField = true },
                    G = new Slider("G", 0f, 1f) { showInputField = true },
                    B = new Slider("B", 0f, 1f) { showInputField = true },
                    Height = new MinMaxSlider("Height", 0f, 1f, 0f, 1f),
                    Slope = new MinMaxSlider("Slope", 0f, 1f, 0f, 1f),
                };

                var header = new VisualElement();
                header.AddToClassList("color-card-header");
                var title = new Label($"Color {i + 1}");
                title.AddToClassList("color-card-title");
                card.Swatch.AddToClassList("color-swatch");
                header.Add(title);
                header.Add(card.Swatch);

                card.Root.AddToClassList("color-card");
                card.Root.Add(header);
                card.Root.Add(card.R);
                card.Root.Add(card.G);
                card.Root.Add(card.B);
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
                RefreshSwatch(_colorCards[i]);
            }
        }

        private static void RefreshSwatch(ColorCard card)
        {
            card.Swatch.style.backgroundColor = new Color(card.R.value, card.G.value, card.B.value);
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
            generator.Generate();
            _dirty = false;
            _lastGenerateTime = Time.unscaledTime;
        }
    }
}
