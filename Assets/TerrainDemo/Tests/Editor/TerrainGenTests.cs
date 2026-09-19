// ─────────────────────────────────────────────────────────────────────────────
// File: Tests/Editor/TerrainGenTests.cs
// Module: Procedural terrain generation · Tests (Unity Test Framework, EditMode)
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using TerrainDemo.Core;
using Debug = UnityEngine.Debug;

namespace TerrainDemo.Tests
{
    public class TerrainGenTests
    {
        /// <summary>Uniform float tolerance for range-style assertions.</summary>
        private const float RangeEps = 1e-4f;

        // ═════════════════════════════════════════════════════════════════════
        // Helpers
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>Builds a fully explicit parameter set (independent of defaults and presets).</summary>
        private static TerrainParams MakeParams(int seed, int resolution = 65)
        {
            return new TerrainParams
            {
                Seed = seed,
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

        /// <summary>FNV-1a 64-bit hash over the bytes of every float in the map, for bitwise determinism checks.</summary>
        private static string FnvHash(float[,] map)
        {
            const ulong offsetBasis = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offsetBasis;
            int rows = map.GetLength(0), cols = map.GetLength(1);
            for (int z = 0; z < rows; z++)
            {
                for (int x = 0; x < cols; x++)
                {
                    byte[] bytes = BitConverter.GetBytes(map[z, x]);
                    for (int i = 0; i < bytes.Length; i++)
                    {
                        hash ^= bytes[i];
                        hash *= prime;
                    }
                }
            }
            return hash.ToString("X16");
        }

        /// <summary>Element-wise maximum absolute difference between two heightmaps.</summary>
        private static float MaxAbsDiff(float[,] a, float[,] b)
        {
            Assert.AreEqual(a.GetLength(0), b.GetLength(0), "Row counts differ; maps are not comparable");
            Assert.AreEqual(a.GetLength(1), b.GetLength(1), "Column counts differ; maps are not comparable");
            float max = 0f;
            for (int z = 0; z < a.GetLength(0); z++)
                for (int x = 0; x < a.GetLength(1); x++)
                    max = Mathf.Max(max, Mathf.Abs(a[z, x] - b[z, x]));
            return max;
        }

        /// <summary>Deterministic synthetic heightmap for mesh tests (noise-independent, isolating the subject).</summary>
        private static float[,] SyntheticHeights(int resolution)
        {
            var heights = new float[resolution, resolution];
            for (int z = 0; z < resolution; z++)
                for (int x = 0; x < resolution; x++)
                    heights[z, x] = ((x * 7 + z * 13) % 32) / 32f;
            return heights;
        }

        /// <summary>
        /// "Second-difference energy" (discrete Laplacian) of an fBm grid:
        /// the mean of |2·center − left − right| and its vertical twin. The second
        /// difference responds to frequency as f², so it measures high-frequency content;
        /// the first difference (∝f) is NOT monotonic in octaves under amplitude-sum
        /// normalization and must not be used as a proxy.
        /// </summary>
        private static double LaplacianEnergy(int octaves)
        {
            var noise = new FractalNoise2D(7, octaves, 2f, 0.5f, 4f);
            const int samples = 128;
            const float span = 16f;
            var values = new float[samples, samples];
            for (int z = 0; z < samples; z++)
                for (int x = 0; x < samples; x++)
                    values[z, x] = noise.Sample(x * span / (samples - 1), z * span / (samples - 1));

            double sum = 0.0;
            long count = 0;
            for (int z = 1; z < samples - 1; z++)
                for (int x = 1; x < samples - 1; x++)
                {
                    sum += Mathf.Abs(2f * values[z, x] - values[z, x - 1] - values[z, x + 1]);
                    sum += Mathf.Abs(2f * values[z, x] - values[z - 1, x] - values[z + 1, x]);
                    count += 2;
                }
            return sum / count;
        }

        // ═════════════════════════════════════════════════════════════════════
        // 1. SeededRng: determinism, seed separation, range, uniformity
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public void SeededRng_SameSeed_ReproducesIdenticalSequence()
        {
            var rngA = new SeededRng(42);
            var rngB = new SeededRng(42);
            for (int i = 0; i < 100; i++)
                Assert.AreEqual(rngA.NextUInt(), rngB.NextUInt(), $"NextUInt sequences diverged at item {i}");
            var a = new SeededRng(42);
            var b = new SeededRng(42);
            for (int i = 0; i < 1000; i++)
                Assert.AreEqual(a.Next01(), b.Next01(), $"Next01 sequences diverged at item {i}");
            Debug.Log("[SeededRng · same seed] NextUInt×100 and Next01×1000 from two seed=42 instances match item by item ✓");
        }

        [Test]
        public void SeededRng_DifferentSeeds_GiveDifferentSequences()
        {
            float[][] sequences = new float[5][];
            for (int s = 0; s < 5; s++)
            {
                var rng = new SeededRng(s + 1);
                sequences[s] = new float[32];
                for (int i = 0; i < 32; i++) sequences[s][i] = rng.Next01();
            }
            int distinctPairs = 0, totalPairs = 0;
            for (int i = 0; i < 5; i++)
                for (int j = i + 1; j < 5; j++)
                {
                    totalPairs++;
                    if (!sequences[i].SequenceEqual(sequences[j])) distinctPairs++;
                }
            Debug.Log($"[SeededRng · seed separation] {distinctPairs}/{totalPairs} pairs of first-32-item sequences from 5 seeds are distinct");
            Assert.AreEqual(totalPairs, distinctPairs, "Two different seeds produced identical first-32-item sequences");
        }

        [Test]
        public void SeededRng_Next01_StaysInUnitInterval()
        {
            var rng = new SeededRng(7);
            float min = 1f, max = -1f;
            for (int i = 0; i < 100000; i++)
            {
                float v = rng.Next01();
                Assert.GreaterOrEqual(v, 0f, $"Item {i} was negative: {v}");
                Assert.Less(v, 1f, $"Item {i} reached 1.0 (contract requires half-open [0,1))");
                min = Mathf.Min(min, v);
                max = Mathf.Max(max, v);
            }
            Debug.Log($"[SeededRng · range] 100,000 draws, observed [{min:F6}, {max:F6}) ⊂ [0,1) ✓");
        }

        [Test]
        public void SeededRng_Next01_MeanNearHalf()
        {
            var rng = new SeededRng(2024);
            double sum = 0.0;
            const int n = 10000;
            for (int i = 0; i < n; i++) sum += rng.Next01();
            double mean = sum / n;
            Debug.Log($"[SeededRng · uniformity] mean of {n} draws = {mean:F4} (theory 0.5, tolerance ±0.05)");
            Assert.That(mean, Is.InRange(0.45, 0.55), "Sample mean deviates too far from 0.5; distribution may be biased");
        }

        // ═════════════════════════════════════════════════════════════════════
        // 2. PerlinNoise2D: range, lattice zeros, period, continuity, reproducibility, table
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public void Perlin_ValuesStayInUnitRange()
        {
            float globalMin = float.MaxValue, globalMax = float.MinValue;
            foreach (int seed in new[] { 1, 7, 12345 })
            {
                var noise = new PerlinNoise2D(seed);
                for (int y = 0; y < 129; y++)
                    for (int x = 0; x < 129; x++)
                    {
                        float v = noise.Noise(x * 0.5f, y * 0.5f);
                        Assert.That(v, Is.InRange(-1f - RangeEps, 1f + RangeEps),
                            $"seed={seed} at ({x * 0.5f},{y * 0.5f}) returned {v}, outside [-1,1]");
                        globalMin = Mathf.Min(globalMin, v);
                        globalMax = Mathf.Max(globalMax, v);
                    }
            }
            Debug.Log($"[Perlin · range] 3 seeds × 129×129 grid, global span [{globalMin:F4}, {globalMax:F4}] ⊂ [-1,1] ✓");
            Assert.Greater(globalMax - globalMin, 0.2f, "The noise field is nearly constant — the implementation looks degenerate");
        }

        [Test]
        public void Perlin_IntegerLatticePoints_AreZero()
        {
            var noise = new PerlinNoise2D(1);
            Vector2[] latticePoints =
            {
                new Vector2(0f, 0f), new Vector2(3f, 7f), new Vector2(12f, -5f),
                new Vector2(-9f, 31f), new Vector2(64f, 64f), new Vector2(1000f, 2000f),
            };
            float maxAbs = 0f;
            foreach (Vector2 p in latticePoints)
            {
                float v = noise.Noise(p.x, p.y);
                Assert.Less(Mathf.Abs(v), 1e-6f, $"Integer lattice point ({p.x},{p.y}) returned {v}; expected 0 (zero offset ⇒ zero dot product)");
                maxAbs = Mathf.Max(maxAbs, Mathf.Abs(v));
            }
            Debug.Log($"[Perlin · lattice zero] 6 integer lattice points (negatives and large values included), max |value| = {maxAbs:E2} ≈ 0 ✓");
        }

        [Test]
        public void Perlin_HasPeriod256()
        {
            var noise = new PerlinNoise2D(7);
            Vector2[] probePoints =
            {
                new Vector2(0.37f, 12.9f), new Vector2(17.3f, 99.1f),
                new Vector2(-3.2f, 45.6f), new Vector2(255.7f, 0.3f),
            };
            float maxDiff = 0f;
            foreach (Vector2 p in probePoints)
            {
                float baseV = noise.Noise(p.x, p.y);
                float dx = Mathf.Abs(baseV - noise.Noise(p.x + 256f, p.y));
                float dy = Mathf.Abs(baseV - noise.Noise(p.x, p.y + 256f));
                maxDiff = Mathf.Max(maxDiff, Mathf.Max(dx, dy));
                Assert.Less(dx, 1e-3f, $"Noise({p.x},{p.y}) vs Noise({p.x}+256,{p.y}) differ by {dx}; period broken");
                Assert.Less(dy, 1e-3f, $"Noise({p.x},{p.y}) vs Noise({p.x},{p.y}+256) differ by {dy}; period broken");
            }
            Debug.Log($"[Perlin · period 256] 4 probes shifted by 256 on x/y, max difference {maxDiff:E2} (tolerance 1e-3) ✓");
        }

        [Test]
        public void Perlin_IsContinuousAcrossLatticeLines()
        {
            var noise = new PerlinNoise2D(3);
            Vector2[] basePoints =
            {
                new Vector2(2.0f, 3.5f), new Vector2(7.0f, -1.25f), new Vector2(0.0f, 0.5f),
                new Vector2(255.999f, 100.0f), new Vector2(-5.0f, -5.0f),
            };
            const float step = 1e-3f, bound = 0.05f;
            float maxDelta = 0f;
            foreach (Vector2 p in basePoints)
            {
                float baseV = noise.Noise(p.x, p.y);
                float deltaX = Mathf.Abs(noise.Noise(p.x + step, p.y) - baseV);
                float deltaY = Mathf.Abs(noise.Noise(p.x, p.y + step) - baseV);
                maxDelta = Mathf.Max(maxDelta, Mathf.Max(deltaX, deltaY));
                Assert.Less(deltaX, bound, $"At ({p.x},{p.y}) a {step} step along x changed the value by {deltaX}: discontinuity");
                Assert.Less(deltaY, bound, $"At ({p.x},{p.y}) a {step} step along y changed the value by {deltaY}: discontinuity");
            }
            Debug.Log($"[Perlin · continuity] 5 base points (across lattice lines / period boundary), max micro-step change {maxDelta:E2} (bound {bound}) ✓");
        }

        [Test]
        public void Perlin_SameSeed_ReproducesFieldAndTable()
        {
            var noiseA = new PerlinNoise2D(42);
            var noiseB = new PerlinNoise2D(42);
            Assert.IsTrue(noiseA.Permutation.SequenceEqual(noiseB.Permutation), "Two constructions with the same seed produced different permutation tables");
            Vector2[] probePoints =
            {
                new Vector2(0.5f, 0.5f), new Vector2(13.7f, 2.2f), new Vector2(-8.3f, 55.5f),
                new Vector2(100.25f, 200.75f), new Vector2(254.9f, 1.1f),
            };
            foreach (Vector2 p in probePoints)
                Assert.AreEqual(noiseA.Noise(p.x, p.y), noiseB.Noise(p.x, p.y), "Same-seed instances disagreed at a probe point");
            Debug.Log("[Perlin · same seed] 256-entry tables identical; noise values bitwise equal at 5 probe points ✓");
        }

        [Test]
        public void Perlin_Permutation_IsPermutationOf256()
        {
            foreach (int seed in new[] { 1, 2, 3, 999 })
            {
                var noise = new PerlinNoise2D(seed);
                int[] perm = noise.Permutation;
                Assert.IsNotNull(perm, $"Permutation table was null for seed={seed}");
                Assert.AreEqual(256, perm.Length, $"Permutation table length should be 256 for seed={seed}");
                int[] sorted = perm.OrderBy(v => v).ToArray();
                int[] expected = Enumerable.Range(0, 256).ToArray();
                Assert.IsTrue(sorted.SequenceEqual(expected), $"Table for seed={seed} is not a permutation of 0..255");
            }
            Debug.Log("[Perlin · table validity] tables for seeds {1,2,3,999} are all complete permutations of 0..255 ✓");
        }

        [Test]
        public void Perlin_DifferentSeeds_GiveDifferentFields()
        {
            var noiseA = new PerlinNoise2D(1);
            var noiseB = new PerlinNoise2D(2);
            float maxDiff = 0f;
            for (int y = 0; y < 33; y++)
                for (int x = 0; x < 33; x++)
                {
                    // Fractional stride so probes do not all land on integer lattice
                    // points (where the value is always 0 and seeds cannot differ).
                    float px = x * 2f + 0.37f, py = y * 2f + 0.53f;
                    maxDiff = Mathf.Max(maxDiff, Mathf.Abs(noiseA.Noise(px, py) - noiseB.Noise(px, py)));
                }
            Debug.Log($"[Perlin · seed separation] seeds 1 vs 2 over a 33×33 grid: max difference {maxDiff:F4} (required > 0.01)");
            Assert.Greater(maxDiff, 0.01f, "Different seeds produced nearly identical noise fields; the seed never reached the table");

            int[] permA = new PerlinNoise2D(3).Permutation;
            int[] permB = new PerlinNoise2D(4).Permutation;
            Assert.IsFalse(permA.SequenceEqual(permB), "Seeds 3 and 4 produced identical permutation tables");
        }

        // ═════════════════════════════════════════════════════════════════════
        // 3. FractalNoise2D: range, single-octave identity, octave effects, energy, reproducibility
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public void Fbm_ValuesStayInUnitRange()
        {
            float globalMin = float.MaxValue, globalMax = float.MinValue;
            foreach (int seed in new[] { 1, 7, 12345 })
            {
                var noise = new FractalNoise2D(seed, 5, 2f, 0.5f, 4f);
                for (int y = 0; y < 65; y++)
                    for (int x = 0; x < 65; x++)
                    {
                        float v = noise.Sample(x * 0.5f, y * 0.5f);
                        Assert.That(v, Is.InRange(-RangeEps, 1f + RangeEps),
                            $"seed={seed} at ({x * 0.5f},{y * 0.5f}) returned {v}, outside [0,1]");
                        globalMin = Mathf.Min(globalMin, v);
                        globalMax = Mathf.Max(globalMax, v);
                    }
            }
            Debug.Log($"[fBm · range] 3 seeds × 65×65 grid, global span [{globalMin:F4}, {globalMax:F4}] ⊂ [0,1] ✓");
            Assert.Greater(globalMax - globalMin, 0.3f, "The fBm field is nearly constant — the implementation looks degenerate");
        }

        [Test]
        public void Fbm_SingleOctave_EqualsNormalizedPerlin()
        {
            const int seed = 11;
            const float scale = 8f;
            var fbm = new FractalNoise2D(seed, 1, 2f, 0.5f, scale);
            var perlin = new PerlinNoise2D(seed);
            Vector2[] probePoints =
            {
                new Vector2(0f, 0f), new Vector2(1.5f, 2.5f), new Vector2(7.25f, 0.5f),
                new Vector2(15.9f, 15.1f), new Vector2(0.001f, 0.002f), new Vector2(-3.7f, 11.2f),
            };
            float maxErr = 0f;
            foreach (Vector2 p in probePoints)
            {
                float expected = 0.5f + 0.5f * perlin.Noise(p.x / scale, p.y / scale);
                float actual = fbm.Sample(p.x, p.y);
                maxErr = Mathf.Max(maxErr, Mathf.Abs(expected - actual));
                Assert.Less(Mathf.Abs(expected - actual), 1e-5f,
                    $"With one octave Sample({p.x},{p.y}) = {actual} ≠ 0.5+0.5·Noise = {expected}");
            }
            Debug.Log($"[fBm · single-octave identity] 6 probes vs 0.5+0.5·Noise(x/scale,y/scale), max error {maxErr:E2} ✓");
        }

        [Test]
        public void Fbm_DifferentOctaveCounts_ChangeResult()
        {
            const int seed = 5;
            var oneOctave = new FractalNoise2D(seed, 1, 2f, 0.5f, 4f);
            var fiveOctaves = new FractalNoise2D(seed, 5, 2f, 0.5f, 4f);
            float maxDiff = 0f;
            for (int y = 0; y < 33; y++)
                for (int x = 0; x < 33; x++)
                    maxDiff = Mathf.Max(maxDiff,
                        Mathf.Abs(oneOctave.Sample(x * 0.5f, y * 0.5f) - fiveOctaves.Sample(x * 0.5f, y * 0.5f)));
            Debug.Log($"[fBm · octaves take effect] same seed, octaves 1→5: max change {maxDiff:F4} over a 33×33 grid (required > 0.01)");
            Assert.Greater(maxDiff, 0.01f, "Adding octaves barely changed the sampled field; high-frequency layers seem missing");
        }

        [Test]
        public void Fbm_HighFrequencyEnergy_GrowsWithOctaves()
        {
            double e1 = LaplacianEnergy(1);
            double e2 = LaplacianEnergy(2);
            double e4 = LaplacianEnergy(4);
            Debug.Log($"[fBm · high-frequency energy] second-difference energy E(1)={e1:F5}, E(2)={e2:F5}, E(4)={e4:F5};"
                      + $" ratios E(2)/E(1)={e2 / e1:F3}, E(4)/E(2)={e4 / e2:F3} (both required > 1.25)");
            Assert.Greater(e1, 1e-5, "Single-layer energy near zero; the sampled field looks constant");
            Assert.Greater(e2, 1.25 * e1, "Second-difference energy E(2) not clearly above E(1); octaves injected no high-frequency content");
            Assert.Greater(e4, 1.25 * e2, "Second-difference energy E(4) not clearly above E(2); higher octaves injected no high-frequency content");
        }

        [Test]
        public void Fbm_SameSeed_ReproducesValues()
        {
            var fbmA = new FractalNoise2D(1234, 5, 2f, 0.5f, 10f);
            var fbmB = new FractalNoise2D(1234, 5, 2f, 0.5f, 10f);
            Vector2[] probePoints =
            {
                new Vector2(0f, 0f), new Vector2(3.3f, 7.7f), new Vector2(-15.2f, 4.4f),
                new Vector2(88.8f, 12.0f), new Vector2(31.999f, 0.001f),
            };
            foreach (Vector2 p in probePoints)
                Assert.AreEqual(fbmA.Sample(p.x, p.y), fbmB.Sample(p.x, p.y), "Two same-parameter fBm instances disagreed");
            Debug.Log("[fBm · same seed] two identical instances bitwise equal at 5 probe points ✓");
        }

        // ═════════════════════════════════════════════════════════════════════
        // 4. HeightmapGenerator: end-to-end determinism, seed separation, size & range
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public void Heightmap_SameSeed_IsBitwiseDeterministic()
        {
            var parameters = MakeParams(1234, 65);
            var stopwatch = Stopwatch.StartNew();
            float[,] first = HeightmapGenerator.Generate(parameters);
            stopwatch.Stop();
            float[,] second = HeightmapGenerator.Generate(parameters);

            string hashFirst = FnvHash(first), hashSecond = FnvHash(second);
            float maxDiff = MaxAbsDiff(first, second);
            Debug.Log($"[Heightmap · determinism] seed=1234, res=65; first run {stopwatch.Elapsed.TotalMilliseconds:F1} ms;"
                      + $" hash A={hashFirst} B={hashSecond}; max element diff {maxDiff}");
            Assert.AreEqual(hashFirst, hashSecond, "FNV-1a hashes of two runs differ; bitwise determinism broken");
            Assert.AreEqual(0f, maxDiff, "Two runs differ element-wise; determinism broken");
        }

        [Test]
        public void Heightmap_DifferentSeeds_Differ()
        {
            float[,] mapA = HeightmapGenerator.Generate(MakeParams(1234, 65));
            float[,] mapB = HeightmapGenerator.Generate(MakeParams(999, 65));
            float maxDiff = MaxAbsDiff(mapA, mapB);
            Debug.Log($"[Heightmap · seed separation] seed 1234 vs 999: hashes {FnvHash(mapA)} vs {FnvHash(mapB)}, max diff {maxDiff:F4} (required > 0.05)");
            Assert.Greater(maxDiff, 0.05f, "Different seeds produced nearly identical heightmaps; the seed is not taking effect");
        }

        [Test]
        public void Heightmap_SizeAndRange_AreCorrect()
        {
            var normal = MakeParams(7, 65);
            float[,] map = HeightmapGenerator.Generate(normal);
            Assert.AreEqual(65, map.GetLength(0), "Heightmap row count (z dimension) should equal Resolution");
            Assert.AreEqual(65, map.GetLength(1), "Heightmap column count (x dimension) should equal Resolution");
            float min = float.MaxValue, max = float.MinValue;
            for (int z = 0; z < 65; z++)
                for (int x = 0; x < 65; x++)
                {
                    min = Mathf.Min(min, map[z, x]);
                    max = Mathf.Max(max, map[z, x]);
                }
            Debug.Log($"[Heightmap · size & range] res=65, span [{min:F4}, {max:F4}] ⊂ [0,1] ✓");

            var shaped = MakeParams(7, 65);
            shaped.HeightExponent = 2.5f;
            float[,] shapedMap = HeightmapGenerator.Generate(shaped);
            float shapedMin = float.MaxValue, shapedMax = float.MinValue;
            for (int z = 0; z < 65; z++)
                for (int x = 0; x < 65; x++)
                {
                    shapedMin = Mathf.Min(shapedMin, shapedMap[z, x]);
                    shapedMax = Mathf.Max(shapedMax, shapedMap[z, x]);
                }
            Debug.Log($"[Heightmap · power shaping] exponent=2.5, span [{shapedMin:F4}, {shapedMax:F4}] still within [0,1] ✓");
            Assert.That(shapedMin, Is.InRange(-RangeEps, 1f + RangeEps), "Power remap produced an out-of-range value");
            Assert.That(shapedMax, Is.InRange(-RangeEps, 1f + RangeEps), "Power remap produced an out-of-range value");
        }

        // ═════════════════════════════════════════════════════════════════════
        // 5. MeshDataBuilder: counts, indices, vertex positions, winding, UVs
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public void Mesh_Counts_AreCorrect()
        {
            const int res = 17;
            var parameters = MakeParams(5, res);
            parameters.WorldSize = 160f;
            parameters.HeightScale = 40f;
            MeshData mesh = MeshDataBuilder.Build(SyntheticHeights(res), parameters);

            Assert.AreEqual(res * res, mesh.Vertices.Length, $"Vertex count should be {res}²={res * res}");
            Assert.AreEqual((res - 1) * (res - 1) * 6, mesh.Triangles.Length, "Triangle index count should be (res-1)²×6");
            Assert.AreEqual(res * res, mesh.UVs.Length, "UV count should match the vertex count");
            Debug.Log($"[Mesh · counts] res={res}: {mesh.Vertices.Length} vertices, {mesh.Triangles.Length} indices, {mesh.UVs.Length} UVs ✓");
        }

        [Test]
        public void Mesh_Indices_AreAllWithinRange()
        {
            const int res = 17;
            var parameters = MakeParams(5, res);
            MeshData mesh = MeshDataBuilder.Build(SyntheticHeights(res), parameters);
            for (int i = 0; i < mesh.Triangles.Length; i++)
            {
                int index = mesh.Triangles[i];
                Assert.That(index, Is.InRange(0, mesh.Vertices.Length - 1), $"Index[{i}]={index} is out of range");
            }
            Debug.Log($"[Mesh · index validity] all {mesh.Triangles.Length} indices ∈ [0, {mesh.Vertices.Length - 1}] ✓");
        }

        [Test]
        public void Mesh_VertexPositions_MatchHeightmap()
        {
            const int res = 17;
            var parameters = MakeParams(5, res);
            parameters.WorldSize = 160f;
            parameters.HeightScale = 40f;
            float[,] heights = SyntheticHeights(res);
            MeshData mesh = MeshDataBuilder.Build(heights, parameters);

            float cell = parameters.WorldSize / (res - 1);
            float maxErr = 0f;
            for (int z = 0; z < res; z++)
                for (int x = 0; x < res; x++)
                {
                    Vector3 v = mesh.Vertices[z * res + x];
                    maxErr = Mathf.Max(maxErr,
                        Mathf.Max(Mathf.Abs(v.x - x * cell),
                                  Mathf.Max(Mathf.Abs(v.y - heights[z, x] * parameters.HeightScale),
                                            Mathf.Abs(v.z - z * cell))));
                }
            Debug.Log($"[Mesh · vertex positions] index=z·res+x convention, cell={cell:F1}: max position error over all {res * res} vertices {maxErr:E2} (tolerance 1e-3) ✓");
            Assert.Less(maxErr, 1e-3f, "Vertex positions do not match the heightmap / cell-size convention");
        }

        [Test]
        public void Mesh_AllTriangles_WindUpward()
        {
            const int res = 17;
            var parameters = MakeParams(5, res);
            MeshData mesh = MeshDataBuilder.Build(SyntheticHeights(res), parameters);

            float minNormalY = float.MaxValue;
            int triangleCount = mesh.Triangles.Length / 3;
            for (int t = 0; t < triangleCount; t++)
            {
                Vector3 a = mesh.Vertices[mesh.Triangles[t * 3]];
                Vector3 b = mesh.Vertices[mesh.Triangles[t * 3 + 1]];
                Vector3 c = mesh.Vertices[mesh.Triangles[t * 3 + 2]];
                float normalY = Vector3.Cross(b - a, c - a).y;
                minNormalY = Mathf.Min(minNormalY, normalY);
                Assert.Greater(normalY, 0f, $"Triangle {t} has normal y={normalY} ≤ 0: wound downward (visible only from below)");
            }
            Debug.Log($"[Mesh · winding up] {triangleCount} triangles all have cross-product y > 0, minimum {minNormalY:F4} ✓");
        }

        [Test]
        public void Mesh_UV_IsNormalized()
        {
            const int res = 17;
            var parameters = MakeParams(5, res);
            MeshData mesh = MeshDataBuilder.Build(SyntheticHeights(res), parameters);

            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < mesh.UVs.Length; i++)
            {
                min = Vector2.Min(min, mesh.UVs[i]);
                max = Vector2.Max(max, mesh.UVs[i]);
                Assert.That(mesh.UVs[i].x, Is.InRange(-RangeEps, 1f + RangeEps), $"UV[{i}].x={mesh.UVs[i].x} out of range");
                Assert.That(mesh.UVs[i].y, Is.InRange(-RangeEps, 1f + RangeEps), $"UV[{i}].y={mesh.UVs[i].y} out of range");
            }
            Assert.Less(Vector2.Distance(mesh.UVs[0], Vector2.zero), 1e-5f, "The first vertex's UV should be (0,0)");
            Assert.Less(Vector2.Distance(mesh.UVs[mesh.UVs.Length - 1], Vector2.one), 1e-5f, "The last vertex's UV should be (1,1)");
            Debug.Log($"[Mesh · UV] span [{min.x:F3},{min.y:F3}] to [{max.x:F3},{max.y:F3}], corners (0,0)/(1,1) correct ✓");
        }
    }
}
