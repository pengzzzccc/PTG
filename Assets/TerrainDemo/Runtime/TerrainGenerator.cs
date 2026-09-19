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

// ── Implementation notes ─────────────────────────────────────────────────────
//
// Purpose. The core layer produces pure data, but the mountain on screen needs a real
// UnityEngine.Mesh. This component is the glue: it organizes "generation" into timed
// stages, flushes MeshData into a MeshFilter, and aggregates per-stage timings into one
// string — supporting the deliverable's "generation time per stage plus real-time FPS"
// and the learning goal "evaluate the cost of each added octave".
//
// Principle. Generate executes five stages in order: noise init, heightmap fill, mesh
// build, normals & assembly, stats. Each stage is timed with System.Diagnostics.
// Stopwatch, which reads the high-resolution counter, ignores Time.timeScale, and
// resolves to sub-millisecond. When assembling, reuse the existing sharedMesh and Clear
// it first (no repeated Mesh resources); assign vertices/triangles/UVs in bulk; then
// RecalculateNormals is mandatory or lighting breaks; a MeshCollider, if present, gets
// the same mesh. The Stage-1 instance is equivalent to the one HeightmapGenerator
// constructs internally — it exists purely to make the "noise init" cost visible to the
// observer (microseconds) and never touches the results.
//
// Approach. The component is deliberately thin: all computation lives in the core
// layer, this only schedules, times and assembles. The Parameters property exposes the
// whole parameter object to the UI: switching a preset replaces the object, changing
// the seed changes one field, and both funnel into Generate to rerun the pipeline —
// the same code path serves the editor ContextMenu and the runtime UI, which is the
// controllability demo's interaction loop. The mesh name carries the seed for easy
// identification, and Start auto-generates when the mesh is missing so "press Play and
// see a mountain" always works.
// ─────────────────────────────────────────────────────────────────────────────
