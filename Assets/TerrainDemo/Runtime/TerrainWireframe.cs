using System;
using TerrainDemo.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace TerrainDemo
{
    public enum ViewMode
    {
        Shaded,
        ShadedWireframe,
        Wireframe,
    }

    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class TerrainWireframe : MonoBehaviour
    {
        [SerializeField] private Shader wireShader;
        [SerializeField] private Color overlayLineColor = new Color(0.05f, 0.05f, 0.05f, 1f);
        [SerializeField] private Color wireLineColor = new Color(0.55f, 0.85f, 1f, 1f);
        [SerializeField] private Color fillColor = new Color(0.08f, 0.08f, 0.09f, 1f);

        public Shader WireShader
        {
            get => wireShader;
            set => wireShader = value;
        }

        public ViewMode Mode { get; private set; } = ViewMode.Shaded;

        public event Action ModeChanged;

        private MeshRenderer _terrainRenderer;
        private MeshFilter _terrainFilter;
        private GameObject _child;
        private Mesh _lineMesh;
        private Material _lineMaterial;
        private Material _fillMaterial;
        private Material _originalMaterial;

        private Vector3[] _vertices;
        private int _resolution;
        private bool _dirty = true;
        private int[] _indices;
        private int _indicesResolution;

        private void Awake()
        {
            _terrainRenderer = GetComponent<MeshRenderer>();
            _terrainFilter = GetComponent<MeshFilter>();
        }

        public void CycleMode()
        {
            SetMode((ViewMode)(((int)Mode + 1) % 3));
        }

        public void SetMode(ViewMode mode)
        {
            if (mode != ViewMode.Shaded && !EnsureObjects()) mode = ViewMode.Shaded;
            Mode = mode;
            if (Mode != ViewMode.Shaded) Rebuild();
            ApplyMode();
            ModeChanged?.Invoke();
        }

        public void OnGeometryChanged(Vector3[] vertices, int resolution)
        {
            _vertices = vertices;
            _resolution = resolution;
            _dirty = true;
            if (Mode != ViewMode.Shaded) Rebuild();
        }

        private bool EnsureObjects()
        {
            if (_child != null) return true;

            Shader shader = wireShader != null ? wireShader : Shader.Find("PTG/Wireframe");
            if (shader == null)
            {
                Debug.LogWarning("[TerrainWireframe] Shader 'PTG/Wireframe' not found; wireframe view is unavailable.");
                return false;
            }

            _lineMaterial = new Material(shader) { name = "PTG Wire Lines" };
            _fillMaterial = new Material(shader) { name = "PTG Wire Fill" };
            _fillMaterial.SetFloat("_OffsetFactor", 0f);
            _fillMaterial.SetFloat("_OffsetUnits", 0f);
            _fillMaterial.SetColor("_Color", fillColor);

            _lineMesh = new Mesh { name = "TerrainWireframe" };
            _child = new GameObject("TerrainDemo_Wireframe") { hideFlags = HideFlags.DontSave };
            _child.transform.SetParent(transform, false);
            _child.AddComponent<MeshFilter>().sharedMesh = _lineMesh;
            var renderer = _child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _lineMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            _child.SetActive(false);
            return true;
        }

        private void Rebuild()
        {
            if (!_dirty || _lineMesh == null) return;

            if (_vertices == null)
            {
                Mesh source = _terrainFilter != null ? _terrainFilter.sharedMesh : null;
                if (source == null) return;
                _vertices = source.vertices;
                _resolution = Mathf.RoundToInt(Mathf.Sqrt(_vertices.Length));
            }
            if (_resolution < 2 || _resolution * _resolution != _vertices.Length) return;

            if (_indices == null || _indicesResolution != _resolution)
            {
                _indices = MeshDataBuilder.BuildWireIndices(_resolution);
                _indicesResolution = _resolution;
            }

            _lineMesh.Clear();
            _lineMesh.indexFormat = _vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            _lineMesh.vertices = _vertices;
            _lineMesh.SetIndices(_indices, MeshTopology.Lines, 0);
            _lineMesh.RecalculateBounds();
            _dirty = false;
        }

        private void ApplyMode()
        {
            if (_child != null) _child.SetActive(Mode != ViewMode.Shaded);
            if (_lineMaterial != null)
            {
                _lineMaterial.SetColor("_Color", Mode == ViewMode.Wireframe ? wireLineColor : overlayLineColor);
            }

            if (Mode == ViewMode.Wireframe)
            {
                if (_originalMaterial == null) _originalMaterial = _terrainRenderer.sharedMaterial;
                _terrainRenderer.sharedMaterial = _fillMaterial;
            }
            else
            {
                RestoreTerrainMaterial();
            }
        }

        private void RestoreTerrainMaterial()
        {
            if (_originalMaterial == null || _terrainRenderer == null) return;
            _terrainRenderer.sharedMaterial = _originalMaterial;
            _originalMaterial = null;
        }

        private void OnDisable()
        {
            RestoreTerrainMaterial();
            if (_child != null) _child.SetActive(false);
            Mode = ViewMode.Shaded;
        }

        private void OnDestroy()
        {
            RestoreTerrainMaterial();
            if (_child != null) Destroy(_child);
            if (_lineMesh != null) Destroy(_lineMesh);
            if (_lineMaterial != null) Destroy(_lineMaterial);
            if (_fillMaterial != null) Destroy(_fillMaterial);
        }
    }
}
