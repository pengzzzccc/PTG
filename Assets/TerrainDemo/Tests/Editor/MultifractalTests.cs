// ─────────────────────────────────────────────────────────────────────────────
// File: Tests/Editor/MultifractalTests.cs
// Module: Procedural terrain generation · Tests (Unity Test Framework, EditMode)
// ─────────────────────────────────────────────────────────────────────────────


using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using TerrainDemo.Core;
using Debug = UnityEngine.Debug;

namespace TerrainDemo.Tests
{
    public class MultifractalTests
    {
        private const float RangeEps = 1e-4f;

        // ── Helpers ─────────────────────────────────────────────────────────

        /// <summary>Builds a fully explicit parameter set with a chosen fractal mode.</summary>
        private static TerrainParams MakeParams(int seed, int resolution, FractalMode mode)
        {
            return new TerrainParams
            {
                Seed = seed,
                Mode = mode,
                Resolution = resolution,
                WorldSize = 200f,
                HeightScale = 30f,
                Scale = 120f,
                Octaves = 5,
                Lacunarity = 2f,
                Persistence = 0.5f,
                HeightExponent = 1f,
            };
        }

        /// <summary>FNV-1a 64-bit hash over the bytes of every float in the map.</summary>
        private static string FnvHash(float[,] map)
        {
            const ulong offsetBasis = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offsetBasis;
            for (int z = 0; z < map.GetLength(0); z++)
                for (int x = 0; x < map.GetLength(1); x++)
                    foreach (byte b in System.BitConverter.GetBytes(map[z, x]))
                    {
                        hash ^= b;
                        hash *= prime;
                    }
            return hash.ToString("X16");
        }

        // ── 1. Determinism and separation ───────────────────────────────────

        [Test]
        public void Multifractal_SameSeed_ReproducesValues()
        {
            var noiseA = new FractalNoise2D(42, 6, 2f, 0.5f, 8f, FractalMode.Multifractal, 0.7f, 2f);
            var noiseB = new FractalNoise2D(42, 6, 2f, 0.5f, 8f, FractalMode.Multifractal, 0.7f, 2f);
            Vector2[] probePoints =
            {
                new Vector2(0.5f, 0.5f), new Vector2(13.7f, 2.2f), new Vector2(-8.3f, 55.5f),
                new Vector2(100.25f, 200.75f), new Vector2(254.9f, 1.1f),
            };
            foreach (Vector2 p in probePoints)
                Assert.AreEqual(noiseA.SampleRaw(p.x, p.y), noiseB.SampleRaw(p.x, p.y),
                    $"Same-seed instances disagreed at ({p.x},{p.y})");
            Debug.Log("[Multifractal · same seed] two seed=42 instances bitwise equal at 5 probe points ✓");
        }

        [Test]
        public void Multifractal_DiffersFromBasicFbmOnSameSeed()
        {
            var feedback = new FractalNoise2D(5, 5, 2f, 0.5f, 8f, FractalMode.Multifractal, 0.7f, 2f);
            var basic = new FractalNoise2D(5, 5, 2f, 0.5f, 8f);
            float maxDiff = 0f;
            for (int y = 0; y < 33; y++)
                for (int x = 0; x < 33; x++)
                {
                    // Fractional stride so probes do not all land on integer lattice points.
                    float px = x * 2f + 0.37f, py = y * 2f + 0.53f;
                    maxDiff = Mathf.Max(maxDiff, Mathf.Abs(feedback.SampleRaw(px, py) - basic.Sample(px, py)));
                }
            Debug.Log($"[Multifractal · mode separation] same seed=5, feedback vs fixed-weight over a 33×33 grid: max difference {maxDiff:F4} (required > 0.01)");
            Assert.Greater(maxDiff, 0.01f, "The feedback mode produced nearly the same values as fixed-weight fBm; the altitude feedback is not taking effect");
        }

        // ── 2. The paper's core claim: roughness grows with altitude ────────

        [Test]
        public void Multifractal_DetailGrowsWithAltitude()
        {
            var noise = new FractalNoise2D(7, 6, 2f, 0.5f, 4f, FractalMode.Multifractal, 0.7f, 2f);
            const int samples = 128;
            const float span = 64f;
            var values = new float[samples, samples];
            for (int z = 0; z < samples; z++)
                for (int x = 0; x < samples; x++)
                    values[z, x] = noise.SampleRaw(x * span / (samples - 1), z * span / (samples - 1));

            // Per-cell roughness via second differences (interior cells only).
            var cells = new List<(float value, float rough)>();
            for (int z = 1; z < samples - 1; z++)
                for (int x = 1; x < samples - 1; x++)
                {
                    float v = values[z, x];
                    float rough = Mathf.Abs(2f * v - values[z, x - 1] - values[z, x + 1])
                                + Mathf.Abs(2f * v - values[z - 1, x] - values[z + 1, x]);
                    cells.Add((v, rough));
                }
            cells.Sort((a, b) => a.value.CompareTo(b.value));

            int quarter = cells.Count / 4;
            double lowSum = 0.0, highSum = 0.0;
            for (int i = 0; i < quarter; i++) lowSum += cells[i].rough;
            for (int i = cells.Count - quarter; i < cells.Count; i++) highSum += cells[i].rough;
            double lowMean = lowSum / quarter, highMean = highSum / quarter;

            Debug.Log($"[Multifractal · altitude feedback] mean second-difference energy — lowest quarter: {lowMean:F5}, highest quarter: {highMean:F5}; "
                      + $"ratio {highMean / lowMean:F3} (required > 1.2)");
            Assert.Greater(lowMean, 1e-5, "Low-band roughness near zero; the sampled field looks constant");
            Assert.Greater(highMean, 1.2 * lowMean,
                "High-altitude cells are not rougher than low-altitude cells; the altitude feedback is not taking effect");
        }

        // ── 3. Heightmap-level contracts for both feedback modes ────────────

        [Test]
        public void Heightmap_Multifractal_IsDeterministicAndInRange()
        {
            var parameters = MakeParams(1234, 65, FractalMode.Multifractal);
            float[,] first = HeightmapGenerator.Generate(parameters);
            float[,] second = HeightmapGenerator.Generate(parameters);

            string hashFirst = FnvHash(first), hashSecond = FnvHash(second);
            Debug.Log($"[Heightmap · Multifractal determinism] seed=1234; hash A={hashFirst} B={hashSecond}");
            Assert.AreEqual(hashFirst, hashSecond, "Two Multifractal runs produced different hashes; determinism broken");

            float min = float.MaxValue, max = float.MinValue;
            for (int z = 0; z < 65; z++)
                for (int x = 0; x < 65; x++)
                {
                    Assert.That(first[z, x], Is.InRange(-RangeEps, 1f + RangeEps), "Multifractal heightmap has an out-of-range value");
                    min = Mathf.Min(min, first[z, x]);
                    max = Mathf.Max(max, first[z, x]);
                }
            Debug.Log($"[Heightmap · Multifractal range] span [{min:F4}, {max:F4}] ⊂ [0,1] ✓");

            float[,] basic = HeightmapGenerator.Generate(MakeParams(1234, 65, FractalMode.BasicFbm));
            float maxDiff = 0f;
            for (int z = 0; z < 65; z++)
                for (int x = 0; x < 65; x++)
                    maxDiff = Mathf.Max(maxDiff, Mathf.Abs(first[z, x] - basic[z, x]));
            Debug.Log($"[Heightmap · mode separation] same seed=1234, Multifractal vs BasicFbm: max difference {maxDiff:F4} (required > 0.05)");
            Assert.Greater(maxDiff, 0.05f, "Same seed produced nearly identical heightmaps for both modes; the Mode field is not taking effect");
        }

        [Test]
        public void Heightmap_RidgedMultifractal_IsDeterministicAndInRange()
        {
            var parameters = MakeParams(1234, 65, FractalMode.RidgedMultifractal);
            float[,] first = HeightmapGenerator.Generate(parameters);
            float[,] second = HeightmapGenerator.Generate(parameters);

            string hashFirst = FnvHash(first), hashSecond = FnvHash(second);
            Debug.Log($"[Heightmap · Ridged determinism] seed=1234; hash A={hashFirst} B={hashSecond}");
            Assert.AreEqual(hashFirst, hashSecond, "Two RidgedMultifractal runs produced different hashes; determinism broken");

            float min = float.MaxValue, max = float.MinValue;
            for (int z = 0; z < 65; z++)
                for (int x = 0; x < 65; x++)
                {
                    Assert.That(first[z, x], Is.InRange(-RangeEps, 1f + RangeEps), "Ridged heightmap has an out-of-range value");
                    min = Mathf.Min(min, first[z, x]);
                    max = Mathf.Max(max, first[z, x]);
                }
            Debug.Log($"[Heightmap · Ridged range] span [{min:F4}, {max:F4}] ⊂ [0,1] ✓");

            float[,] plain = HeightmapGenerator.Generate(MakeParams(1234, 65, FractalMode.Multifractal));
            float maxDiff = 0f;
            for (int z = 0; z < 65; z++)
                for (int x = 0; x < 65; x++)
                    maxDiff = Mathf.Max(maxDiff, Mathf.Abs(first[z, x] - plain[z, x]));
            Debug.Log($"[Heightmap · variant separation] same seed=1234, Ridged vs Multifractal: max difference {maxDiff:F4} (required > 0.01)");
            Assert.Greater(maxDiff, 0.01f, "Ridged and plain Multifractal produced nearly identical maps; the ridge shaping is not taking effect");
        }

        // ── 4. Preset smoke test ────────────────────────────────────────────

        [Test]
        public void RuggedPeaks_Preset_GeneratesInRange()
        {
            var parameters = TerrainParams.RuggedPeaks();
            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();
            float[,] map = HeightmapGenerator.Generate(parameters);
            stopwatch.Stop();

            Assert.AreEqual(parameters.Resolution, map.GetLength(0), "Row count should equal Resolution");
            Assert.AreEqual(parameters.Resolution, map.GetLength(1), "Column count should equal Resolution");
            float min = float.MaxValue, max = float.MinValue;
            for (int z = 0; z < parameters.Resolution; z++)
                for (int x = 0; x < parameters.Resolution; x++)
                {
                    Assert.That(map[z, x], Is.InRange(-RangeEps, 1f + RangeEps), "RuggedPeaks heightmap has an out-of-range value");
                    min = Mathf.Min(min, map[z, x]);
                    max = Mathf.Max(max, map[z, x]);
                }
            Debug.Log($"[Preset · Rugged Peaks] res={parameters.Resolution}, multifractal (altitude feedback), {stopwatch.Elapsed.TotalMilliseconds:F1} ms, "
                      + $"span [{min:F4}, {max:F4}] ⊂ [0,1] ✓");
            Assert.AreEqual(FractalMode.Multifractal, parameters.Mode, "The RuggedPeaks preset should showcase the multifractal altitude-feedback mode");
        }
    }
}
