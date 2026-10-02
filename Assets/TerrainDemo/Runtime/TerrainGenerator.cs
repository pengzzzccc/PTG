// ─────────────────────────────────────────────────────────────────────────────
// File: Runtime/TerrainGenerator.cs
// Module: Procedural terrain generation · Unity runtime
// Status: Fully implemented
// ─────────────────────────────────────────────────────────────────────────────

using System.Text;
using System.Diagnostics;
using TerrainDemo.Core;
using UnityEngine;

namespace TerrainDemo
{
    /// <summary>
    /// The pipeline's driver inside the scene: reads parameters, executes stage by stage
    /// with timing, and assembles the result into a Unity Mesh.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    public class TerrainGenerator : MonoBehaviour
    {
        /// <summary>Everything the generation needs; edit seed or parameters in the Inspector and hit Generate again.</summary>
        [SerializeField] private TerrainParams parameters = new TerrainParams();

        /// <summary>Per-stage timings of the last generation (shown in the UI panel).</summary>
        public string LastStageStats = "";

        /// <summary>Current parameters. The UI swaps the whole object when switching presets, then calls Generate.</summary>
        public TerrainParams Parameters
        {
            get => parameters;
            set => parameters = value;
        }

        /// <summary>Runs the whole pipeline; both the ContextMenu and the UI panel call this.</summary>
        [ContextMenu("Generate")]
        public void Generate()
        {
            var meshFilter = GetComponent<MeshFilter>();
            var stats = new StringBuilder();
            var clock = Stopwatch.StartNew();

            // Stage 1 noise init: seed → PRNG → permutation table
            var fractal = new FractalNoise2D(parameters.Seed, parameters.Octaves, parameters.Lacunarity, parameters.Persistence, parameters.Scale);
            clock.Stop();
            stats.AppendFormat("Noise {0:F1}ms | ", clock.Elapsed.TotalMilliseconds);

            // Stage 2 heightmap: per-cell fBm sampling + terrace quantization + power remap
            clock.Restart();
            float[,] heights = HeightmapGenerator.Generate(parameters);
            clock.Stop();
            stats.AppendFormat("Heightmap {0:F1}ms ({1}x{1}) | ", clock.Elapsed.TotalMilliseconds, parameters.Resolution);

            // Stage 3 geometry: heightmap → vertices/triangles/UVs
            clock.Restart();
            MeshData meshData = MeshDataBuilder.Build(heights, parameters);
            clock.Stop();
            stats.AppendFormat("Mesh {0:F1}ms | ", clock.Elapsed.TotalMilliseconds);

            // Stage 4 normals & assembly: flush into the Mesh, recompute normals and bounds, sync collider
            clock.Restart();
            if (meshFilter.sharedMesh == null)
            {
                meshFilter.sharedMesh = new Mesh { name = $"Terrain_{parameters.Seed}" };
            }
            Mesh mesh = meshFilter.sharedMesh;
            mesh.Clear();
            mesh.vertices = meshData.Vertices;
            mesh.triangles = meshData.Triangles;
            mesh.uv = meshData.UVs;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var collider = GetComponent<MeshCollider>();
            if (collider != null) collider.sharedMesh = mesh;
            clock.Stop();
            stats.AppendFormat("Apply {0:F1}ms", clock.Elapsed.TotalMilliseconds);

            // Stage 5 stats: per-stage timings for the UI panel, displayed next to the FPS
            LastStageStats = stats.ToString();
        }

        private void Start()
        {
            // If the scene has no mesh yet (e.g. Play pressed without a prior editor-side
            // generation), generate once automatically.
            if (GetComponent<MeshFilter>().sharedMesh == null)
            {
                Generate();
            }
        }
    }
}

