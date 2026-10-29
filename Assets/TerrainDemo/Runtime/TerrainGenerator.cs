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

        public string LastStageStats = "";

        public double HeightMs { get; private set; }
        public double MeshMs { get; private set; }
        public double UploadMs { get; private set; }
        public double NormalsMs { get; private set; }
        public double BoundsMs { get; private set; }
        public double ColliderMs { get; private set; }
        public double TotalMs => HeightMs + MeshMs + UploadMs + NormalsMs + BoundsMs + ColliderMs;

        public event Action Generated;

        public TerrainParams Parameters
        {
            get => parameters;
            set => parameters = value;
        }

        [ContextMenu("Generate")]
        public void Generate()
        {
            var meshFilter = GetComponent<MeshFilter>();
            var clock = Stopwatch.StartNew();

            float[,] heights = HeightmapGenerator.Generate(parameters);
            clock.Stop();
            HeightMs = clock.Elapsed.TotalMilliseconds;

            clock.Restart();
            MeshData meshData = MeshDataBuilder.Build(heights, parameters);
            clock.Stop();
            MeshMs = clock.Elapsed.TotalMilliseconds;

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

            // Apply Collider
            clock.Restart();
            var collider = GetComponent<MeshCollider>();
            if (collider != null) collider.sharedMesh = mesh;
            clock.Stop();
            ColliderMs = clock.Elapsed.TotalMilliseconds;

            LastStageStats = string.Format(CultureInfo.InvariantCulture,
                "Height {0:F1}ms ({1}x{1}) | Mesh {2:F1}ms | Upload {3:F1}ms | Normals {4:F1}ms | Bounds {5:F1}ms | Collider {6:F1}ms",
                HeightMs, parameters.Resolution, MeshMs, UploadMs, NormalsMs, BoundsMs, ColliderMs);

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
