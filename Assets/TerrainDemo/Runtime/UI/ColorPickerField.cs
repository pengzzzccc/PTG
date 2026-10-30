using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace TerrainDemo
{
    public class ColorPickerField : BaseField<Color>
    {
        private const int SvTextureSize = 64;

        private static readonly Color[] QuickColors =
        {
            new Color(0.00f, 0.24f, 0.55f),
            new Color(0.39f, 0.63f, 0.86f),
            new Color(0.78f, 0.78f, 1.00f),
            new Color(0.76f, 0.70f, 0.50f),
            new Color(0.30f, 0.50f, 0.20f),
            new Color(0.13f, 0.33f, 0.15f),
            new Color(0.45f, 0.38f, 0.30f),
            new Color(0.40f, 0.40f, 0.40f),
            new Color(0.22f, 0.21f, 0.20f),
            new Color(1.00f, 1.00f, 1.00f),
        };

        private static Texture2D s_hueTexture;

        private readonly VisualElement _swatch;
        private readonly TextField _hexField;
        private readonly VisualElement _expanded;
        private readonly VisualElement _svArea;
        private readonly VisualElement _svMarker;
        private readonly VisualElement _hueArea;
        private readonly VisualElement _hueMarker;

        private Texture2D _svTexture;
        private float _svTextureHue = -1f;
        private float _h;
        private float _s;
        private float _v;
        private bool _settingFromHsv;

        public VisualElement Swatch => _swatch;
        public VisualElement HexField => _hexField;
        public bool Expanded { get; private set; }

        public event Action<ColorPickerField> ExpandRequested;

        public ColorPickerField() : this(new VisualElement())
        {
        }

        private ColorPickerField(VisualElement input) : base(null, input)
        {
            AddToClassList("color-picker");
            style.flexDirection = FlexDirection.Column;
            input.AddToClassList("color-picker__row");

            _swatch = new VisualElement();
            _swatch.AddToClassList("color-picker__swatch");
            _swatch.RegisterCallback<PointerDownEvent>(e =>
            {
                ExpandRequested?.Invoke(this);
                e.StopPropagation();
            });
            input.Add(_swatch);

            _hexField = new TextField { isDelayed = true, maxLength = 7 };
            _hexField.AddToClassList("color-picker__hex");
            _hexField.RegisterValueChangedCallback(e =>
            {
                string text = e.newValue.Trim();
                if (!text.StartsWith("#")) text = "#" + text;
                if (ColorUtility.TryParseHtmlString(text, out Color parsed))
                {
                    parsed.a = 1f;
                    value = parsed;
                }
                else
                {
                    _hexField.SetValueWithoutNotify(ToHex(value));
                }
            });
            input.Add(_hexField);

            _expanded = new VisualElement();
            _expanded.AddToClassList("color-picker__panel");
            _expanded.style.display = DisplayStyle.None;
            Add(_expanded);

            _svArea = new VisualElement();
            _svArea.AddToClassList("color-picker__sv");
            _svMarker = new VisualElement { pickingMode = PickingMode.Ignore };
            _svMarker.AddToClassList("color-picker__marker");
            _svArea.Add(_svMarker);
            RegisterDrag(_svArea, PickSaturationValue);
            _expanded.Add(_svArea);

            _hueArea = new VisualElement();
            _hueArea.AddToClassList("color-picker__hue");
            _hueArea.style.backgroundImage = new StyleBackground(HueTexture());
            _hueMarker = new VisualElement { pickingMode = PickingMode.Ignore };
            _hueMarker.AddToClassList("color-picker__hue-marker");
            _hueArea.Add(_hueMarker);
            RegisterDrag(_hueArea, PickHue);
            _expanded.Add(_hueArea);

            var quickRow = new VisualElement();
            quickRow.AddToClassList("color-picker__quick");
            foreach (Color quick in QuickColors)
            {
                Color captured = quick;
                var chip = new VisualElement();
                chip.AddToClassList("color-picker__chip");
                chip.style.backgroundColor = captured;
                chip.RegisterCallback<PointerDownEvent>(e =>
                {
                    value = captured;
                    e.StopPropagation();
                });
                quickRow.Add(chip);
            }
            _expanded.Add(quickRow);

            RegisterCallback<DetachFromPanelEvent>(_ => ReleaseTexture());
            SetValueWithoutNotify(Color.white);
        }

        public void SetExpanded(bool expanded)
        {
            Expanded = expanded;
            _expanded.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
            _swatch.EnableInClassList("color-picker__swatch--open", expanded);
            if (expanded) UpdateSvTexture();
        }

        public override void SetValueWithoutNotify(Color newValue)
        {
            newValue.a = 1f;
            base.SetValueWithoutNotify(newValue);

            if (!_settingFromHsv)
            {
                Color.RGBToHSV(newValue, out float h, out float s, out float v);
                if (s > 0f && v > 0f) _h = h;
                if (v > 0f) _s = s;
                _v = v;
            }
            RefreshVisuals();
        }

        private void SetFromHsv()
        {
            _settingFromHsv = true;
            value = Color.HSVToRGB(_h, _s, _v);
            _settingFromHsv = false;
            RefreshVisuals();
        }

        private void PickSaturationValue(Vector2 local)
        {
            Rect rect = _svArea.contentRect;
            if (rect.width <= 0f || rect.height <= 0f) return;
            _s = Mathf.Clamp01(local.x / rect.width);
            _v = 1f - Mathf.Clamp01(local.y / rect.height);
            SetFromHsv();
        }

        private void PickHue(Vector2 local)
        {
            Rect rect = _hueArea.contentRect;
            if (rect.width <= 0f) return;
            _h = Mathf.Clamp01(local.x / rect.width);
            SetFromHsv();
        }

        private void RefreshVisuals()
        {
            if (_swatch == null) return;
            _swatch.style.backgroundColor = value;
            if (_hexField.focusController == null || _hexField.focusController.focusedElement != _hexField)
            {
                _hexField.SetValueWithoutNotify(ToHex(value));
            }

            _svMarker.style.left = Length.Percent(_s * 100f);
            _svMarker.style.top = Length.Percent((1f - _v) * 100f);
            _hueMarker.style.left = Length.Percent(_h * 100f);
            if (Expanded) UpdateSvTexture();
        }

        private void UpdateSvTexture()
        {
            if (_svTexture == null)
            {
                _svTexture = new Texture2D(SvTextureSize, SvTextureSize, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.DontSave,
                };
                _svArea.style.backgroundImage = new StyleBackground(_svTexture);
            }
            if (Mathf.Abs(_svTextureHue - _h) < 1e-4f) return;

            var pixels = new Color32[SvTextureSize * SvTextureSize];
            for (int y = 0; y < SvTextureSize; y++)
            {
                float v = y / (float)(SvTextureSize - 1);
                for (int x = 0; x < SvTextureSize; x++)
                {
                    float s = x / (float)(SvTextureSize - 1);
                    pixels[y * SvTextureSize + x] = Color.HSVToRGB(_h, s, v);
                }
            }
            _svTexture.SetPixels32(pixels);
            _svTexture.Apply(false);
            _svTextureHue = _h;
            _svArea.MarkDirtyRepaint();
        }

        private void ReleaseTexture()
        {
            if (_svTexture == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(_svTexture);
            else UnityEngine.Object.DestroyImmediate(_svTexture);
            _svTexture = null;
            _svTextureHue = -1f;
        }

        private static void RegisterDrag(VisualElement area, Action<Vector2> pick)
        {
            area.RegisterCallback<PointerDownEvent>(e =>
            {
                area.CapturePointer(e.pointerId);
                pick(e.localPosition);
                e.StopPropagation();
            });
            area.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (area.HasPointerCapture(e.pointerId)) pick(e.localPosition);
            });
            area.RegisterCallback<PointerUpEvent>(e =>
            {
                if (area.HasPointerCapture(e.pointerId)) area.ReleasePointer(e.pointerId);
            });
        }

        private static Texture2D HueTexture()
        {
            if (s_hueTexture != null) return s_hueTexture;
            const int width = 256;
            s_hueTexture = new Texture2D(width, 1, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
            var pixels = new Color32[width];
            for (int x = 0; x < width; x++) pixels[x] = Color.HSVToRGB(x / (float)(width - 1), 1f, 1f);
            s_hueTexture.SetPixels32(pixels);
            s_hueTexture.Apply(false);
            return s_hueTexture;
        }

        private static string ToHex(Color color)
        {
            return "#" + ColorUtility.ToHtmlStringRGB(color);
        }
    }
}
