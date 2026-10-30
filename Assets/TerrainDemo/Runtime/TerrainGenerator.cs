using System;
using System.Diagnostics;
using System.Globalization;
using TerrainDemo.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace TerrainDemo
{
    [RequireComponent(typeof(MeshFilter))]
    public class TerrainGenerator : MonoBehaviour
    {
        [SerializeField] private TerrainParams parameters = new TerrainParams();

        [SerializeField] private bool useParallel = true;

        public bool UseParallel
        {
            get => useParallel;
            set => useParallel = value;
        }

        public string LastStageStats = "";

        public double HeightMs { get; private set; }
        public double MeshMs { get; private set; }
        public double ColorMs { get; private set; }
        public double UploadMs { get; private set; }
        public double NormalsMs { get; private set; }
        public double BoundsMs { get; private set; }
        public double TotalMs => HeightMs + MeshMs + ColorMs + UploadMs + NormalsMs + BoundsMs;

        public bool LastWasRecolor { get; private set; }

        public event Action Generated;

        private float[,] _cachedHeights;
        private float[] _cachedSlopes;
        private TerrainParams _shapeSnapshot;

        public TerrainParams Parameters
        {
            get => parameters;
            set => parameters = value;
        }

        [ContextMenu("Generate")]
        public void ForceGenerate() => Generate(true);

        public void Generate() => Generate(false);

        public void Generate(bool forceFull)
        {
            var meshFilter = GetComponent<MeshFilter>();
            Mesh existing = meshFilter.sharedMesh;

            if (!forceFull
                && _cachedHeights != null
                && existing != null
                && existing.vertexCount == _cachedSlopes.Length
                && parameters.SameShape(_shapeSnapshot))
            {
                Recolor(existing);
                return;
            }

            LastWasRecolor = false;
            var clock = Stopwatch.StartNew();

            float[,] heights = HeightmapGenerator.Generate(parameters, useParallel);
            clock.Stop();
            HeightMs = clock.Elapsed.TotalMilliseconds;

            clock.Restart();
            MeshData meshData = MeshDataBuilder.BuildGeometry(heights, parameters, useParallel);
            clock.Stop();
            MeshMs = clock.Elapsed.TotalMilliseconds;

            clock.Restart();
            meshData.colors = MeshDataBuilder.BuildColors(heights, meshData.Slopes, parameters, useParallel);
            clock.Stop();
            ColorMs = clock.Elapsed.TotalMilliseconds;

            _cachedHeights = heights;
            _cachedSlopes = meshData.Slopes;
            _shapeSnapshot = parameters.CloneShape();

            if (meshFilter.sharedMesh == null)
            {
                meshFilter.sharedMesh = new Mesh { name = $"Terrain_{parameters.Seed}" };
            }
            Mesh mesh = meshFilter.sharedMesh;
            mesh.Clear();

            // Apply Index
            mesh.indexFormat = meshData.Vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;

            // Apply mesh's data
            clock.Restart();
            mesh.vertices = meshData.Vertices;
            mesh.triangles = meshData.Triangles;
            mesh.uv = meshData.UVs;
            mesh.colors = meshData.colors;
            clock.Stop();
            UploadMs = clock.Elapsed.TotalMilliseconds;

            // Recalculate Normals
            clock.Restart();
            mesh.RecalculateNormals();
            clock.Stop();
            NormalsMs = clock.Elapsed.TotalMilliseconds;

            // Recalculate Bounds
            clock.Restart();
            mesh.RecalculateBounds();
            clock.Stop();
            BoundsMs = clock.Elapsed.TotalMilliseconds;

            LastStageStats = string.Format(CultureInfo.InvariantCulture,
                "Height {0:F1}ms ({1}x{1}) | Mesh {2:F1}ms | Color {3:F1}ms | Upload {4:F1}ms | Normals {5:F1}ms | Bounds {6:F1}ms",
                HeightMs, parameters.Resolution, MeshMs, ColorMs, UploadMs, NormalsMs, BoundsMs);

            Generated?.Invoke();
        }

        private void Recolor(Mesh mesh)
        {
            LastWasRecolor = true;
            HeightMs = 0;
            MeshMs = 0;
            NormalsMs = 0;
            BoundsMs = 0;

            var clock = Stopwatch.StartNew();
            Color[] colors = MeshDataBuilder.BuildColors(_cachedHeights, _cachedSlopes, parameters, useParallel);
            clock.Stop();
            ColorMs = clock.Elapsed.TotalMilliseconds;

            clock.Restart();
            mesh.colors = colors;
            clock.Stop();
            UploadMs = clock.Elapsed.TotalMilliseconds;

            LastStageStats = string.Format(CultureInfo.InvariantCulture,
                "Recolor ({0}x{0}) | Color {1:F1}ms | Upload {2:F1}ms",
                parameters.Resolution, ColorMs, UploadMs);

            Generated?.Invoke();
        }

        private void Start()
        {
            if (GetComponent<MeshFilter>().sharedMesh == null)
            {
                Generate();
            }
        }
    }
}
