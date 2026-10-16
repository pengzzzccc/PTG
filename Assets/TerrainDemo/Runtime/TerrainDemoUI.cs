using System.Collections.Generic;
using TerrainDemo.Core;
using UnityEngine;
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
            WireSpecialControls(root);
            BuildBindings();
            if (generator != null) CopyParametersToControls(generator.Parameters);
        }

        private void Update()
        {
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
                _statsLabel.text = generator.LastStageStats.Replace(" | ", "\n") + "\nFPS " + _fps.ToString("F0");
            }
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

            bool complete = _seedField != null
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
