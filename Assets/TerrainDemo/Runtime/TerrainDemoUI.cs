// ─────────────────────────────────────────────────────────────────────────────
// File: Runtime/TerrainDemoUI.cs
// Module: Procedural terrain generation · Unity runtime
// Status: Fully implemented (UI Toolkit; structure in UXML, styles in USS)
// ─────────────────────────────────────────────────────────────────────────────

using System.Collections.Generic;
using TerrainDemo.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace TerrainDemo
{
    /// <summary>
    /// Demo control panel (UI Toolkit): a full-height panel on the left quarter of the
    /// screen exposing every TerrainParams field — seed, resolution, world size, the
    /// octave stack, feedback parameters, shaping — plus presets, regeneration, per-stage
    /// timings and FPS. The visual tree lives in TerrainDemoUI.uxml and the styles in
    /// TerrainDemoUI.uss; this class queries the named elements, injects dropdown
    /// choices, and wires callbacks. Slider edits only mark the pipeline dirty; Update
    /// regenerates at most every ~0.3 s, so dragging stays smooth while feedback stays
    /// near-live.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class TerrainDemoUI : MonoBehaviour
    {
        /// <summary>Terrain generator in the scene; every panel action ends in its Generate().</summary>
        [SerializeField] private TerrainGenerator generator;

        /// <summary>Style sheet for the panel, assigned by DemoSceneBuilder (or the Inspector).</summary>
        [SerializeField] private StyleSheet panelStyleSheet;

        /// <summary>Public accessor so the editor builder can wire the generator reference.</summary>
        public TerrainGenerator Generator
        {
            get => generator;
            set => generator = value;
        }

        /// <summary>Public accessor for the panel style sheet.</summary>
        public StyleSheet PanelStyleSheet
        {
            get => panelStyleSheet;
            set => panelStyleSheet = value;
        }

        private static readonly string[] PresetNames = { "Rolling Hills", "Rugged Peaks", "Terraced Mesas" };
        private static readonly string[] ModeNames = { "Basic FBM", "Multifractal", "Ridged Multifractal" };

        private const float RegenerateIntervalSeconds = 0.3f;

        // Controls, queried from UXML by name.
        private IntegerField _seedField;
        private DropdownField _presetField;
        private DropdownField _modeField;
        private SliderInt _resolutionSlider;
        private Slider _worldSizeSlider;
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
            if (generator != null) CopyParametersToControls(generator.Parameters);
            WireCallbacks(root);
        }

        private void Update()
        {
            if (generator == null) return;

            // Throttled regeneration: slider drags only mark the pipeline dirty, Update
            // flushes at most every RegenerateIntervalSeconds so dragging stays smooth.
            if (_dirty && Time.unscaledTime - _lastGenerateTime >= RegenerateIntervalSeconds)
            {
                SyncParametersFromControls();
                generator.Generate();
                _dirty = false;
                _lastGenerateTime = Time.unscaledTime;
            }

            if (_statsLabel != null)
            {
                // FPS: exponential smoothing over unscaled deltas — reacts to real
                // changes without jittering every frame.
                float dt = Time.unscaledDeltaTime;
                if (dt > 0f) _fps = Mathf.Lerp(_fps, 1f / dt, 0.05f);
                _statsLabel.text = generator.LastStageStats + "\nFPS " + _fps.ToString("F0");
            }
        }

        // ── Wiring ──────────────────────────────────────────────────────────

        /// <summary>Queries every named element from UXML; logs a warning listing anything missing.</summary>
        private bool QueryControls(VisualElement root)
        {
            _seedField = root.Q<IntegerField>("seed-field");
            _presetField = root.Q<DropdownField>("preset-field");
            _modeField = root.Q<DropdownField>("mode-field");
            _resolutionSlider = root.Q<SliderInt>("resolution-slider");
            _worldSizeSlider = root.Q<Slider>("world-size-slider");
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

            bool complete = _seedField != null && _presetField != null && _modeField != null
                && _resolutionSlider != null && _worldSizeSlider != null && _heightScaleSlider != null
                && _scaleSlider != null && _octavesSlider != null && _lacunaritySlider != null
                && _persistenceSlider != null && _offsetSlider != null && _gainSlider != null
                && _exponentSlider != null && _terraceSlider != null && _statsLabel != null;
            if (!complete)
            {
                Debug.LogWarning("[TerrainDemoUI] Expected UXML elements are missing — check the UIDocument's visualTreeAsset.");
            }
            return complete;
        }

        /// <summary>Injects dropdown choices and registers the change callbacks.</summary>
        private void WireCallbacks(VisualElement root)
        {
            _presetField.choices = new List<string>(PresetNames);
            _presetField.index = 0;
            _presetField.RegisterValueChangedCallback(_ =>
            {
                // Presets swap the whole parameter object, then the controls mirror it
                // (each assignment fires a change callback that just re-raises dirty).
                generator.Parameters = _presetField.index switch
                {
                    1 => TerrainParams.RuggedPeaks(),
                    2 => TerrainParams.TerracedMesas(),
                    _ => TerrainParams.RollingHills(),
                };
                CopyParametersToControls(generator.Parameters);
                RequestRegenerate();
            });

            _modeField.choices = new List<string>(ModeNames);
            _modeField.RegisterValueChangedCallback(_ => RequestRegenerate());

            Button randomButton = root.Q<Button>("random-button");
            Button generateButton = root.Q<Button>("generate-button");
            if (randomButton != null)
            {
                // The randomness lives outside the pipeline, so the reproducibility
                // contract ("same seed ⇒ same terrain") is untouched.
                randomButton.clicked += () => _seedField.value = Random.Range(0, int.MaxValue);
            }
            if (generateButton != null) generateButton.clicked += ForceRegenerate;

            // Every remaining control just marks the pipeline dirty; SyncParametersFromControls
            // pulls the values, so each callback body stays identical.
            _seedField.RegisterValueChangedCallback(_ => RequestRegenerate());
            _resolutionSlider.RegisterValueChangedCallback(_ => RequestRegenerate());
            _worldSizeSlider.RegisterValueChangedCallback(_ => RequestRegenerate());
            _heightScaleSlider.RegisterValueChangedCallback(_ => RequestRegenerate());
            _scaleSlider.RegisterValueChangedCallback(_ => RequestRegenerate());
            _octavesSlider.RegisterValueChangedCallback(_ => RequestRegenerate());
            _lacunaritySlider.RegisterValueChangedCallback(_ => RequestRegenerate());
            _persistenceSlider.RegisterValueChangedCallback(_ => RequestRegenerate());
            _offsetSlider.RegisterValueChangedCallback(_ => RequestRegenerate());
            _gainSlider.RegisterValueChangedCallback(_ => RequestRegenerate());
            _exponentSlider.RegisterValueChangedCallback(_ => RequestRegenerate());
            _terraceSlider.RegisterValueChangedCallback(_ => RequestRegenerate());
        }

        /// <summary>Marks the pipeline dirty; Update flushes at most every 0.3 s.</summary>
        private void RequestRegenerate() => _dirty = true;

        /// <summary>Mirrors every control value back into generator.Parameters.</summary>
        private void SyncParametersFromControls()
        {
            TerrainParams p = generator.Parameters;
            p.Seed = _seedField.value;
            p.Mode = (FractalMode)_modeField.index;
            p.Resolution = _resolutionSlider.value;
            p.WorldSize = _worldSizeSlider.value;
            p.HeightScale = _heightScaleSlider.value;
            p.Scale = _scaleSlider.value;
            p.Octaves = _octavesSlider.value;
            p.Lacunarity = _lacunaritySlider.value;
            p.Persistence = _persistenceSlider.value;
            p.Offset = _offsetSlider.value;
            p.Gain = _gainSlider.value;
            p.HeightExponent = _exponentSlider.value;
            p.TerraceLevels = _terraceSlider.value;
        }

        /// <summary>Mirrors generator.Parameters into the controls (used on preset swaps).</summary>
        private void CopyParametersToControls(TerrainParams p)
        {
            _seedField.value = p.Seed;
            _modeField.index = (int)p.Mode;
            _resolutionSlider.value = p.Resolution;
            _worldSizeSlider.value = p.WorldSize;
            _heightScaleSlider.value = p.HeightScale;
            _scaleSlider.value = p.Scale;
            _octavesSlider.value = p.Octaves;
            _lacunaritySlider.value = p.Lacunarity;
            _persistenceSlider.value = p.Persistence;
            _offsetSlider.value = p.Offset;
            _gainSlider.value = p.Gain;
            _exponentSlider.value = p.HeightExponent;
            _terraceSlider.value = p.TerraceLevels;
        }

        /// <summary>Regenerates immediately with the current control values, bypassing the throttle.</summary>
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

// ── Implementation notes ─────────────────────────────────────────────────────
//
// Purpose. The panel is the demo's "experiment bench": every TerrainParams field is
// exposed as a live control on the left quarter of the screen, so reproduction
// experiments (same seed, same mountain), octave-cost measurements and parameter-space
// exploration all happen in Play mode without touching the Inspector.
//
// Principle. Structure and styling stay in UXML/USS; this file only queries named
// elements and wires behaviour. Controls never call Generate directly — they set the
// dirty flag, and Update flushes at most every 0.3 s (RegenerateIntervalSeconds).
// That converts a slider drag (which fires dozens of change events) into a short
// series of full regenerations instead of one per dragged pixel, while feedback still
// feels live at the demo's ~0.1 s generation cost. Presets flow the other way: the
// preset's TerrainParams object replaces generator.Parameters and the controls mirror
// it, so the controls are always a faithful view of the parameters that produced the
// current mesh.
//
// Approach. Parameter sync lives in two symmetric methods (controls→parameters,
// parameters→controls); adding a future parameter means adding it to UXML, the query,
// and both sync methods — nothing else. The Generate button bypasses the throttle for
// an immediate result, and the seed field participates in the same throttled path so
// typing a new seed regenerates hands-free.
// ─────────────────────────────────────────────────────────────────────────────
