// ─────────────────────────────────────────────────────────────────────────────
// File: Core/HeightmapGenerator.cs
// Module: Procedural terrain generation · Core (pure function)
// Status: Fully implemented
// ─────────────────────────────────────────────────────────────────────────────

using UnityEngine;

namespace TerrainDemo.Core
{
    /// <summary>
    /// Pure function turning parameters into a heightmap: a TerrainParams in, a
    /// resolution×resolution float array out, values in [0,1]. It is the heart of the
    /// pipeline's "generation" stage and the object of the end-to-end determinism check.
    /// </summary>
    public static class HeightmapGenerator
    {
        /// <summary>
        /// Generates the heightmap. Contract: both array dimensions equal
        /// parameters.Resolution, indexed [z, x] (row-major, z = row, x = column);
        /// values in [0,1]; pure — never mutates the parameters, never reads the clock or
        /// the environment, and repeated calls with the same parameters are bitwise equal.
        /// BasicFbm values are already normalized to [0,1]; the altitude-feedback modes
        /// (Multifractal, RidgedMultifractal) accumulate raw values that are not bounded,
        /// so those maps are normalized by their own min/max — a pure per-map operation —
        /// before shaping.
        /// </summary>
        public static float[,] Generate(TerrainParams parameters)
        {
            int resolution = parameters.Resolution;
            float cell = parameters.WorldSize / (resolution - 1);
            var noise = new FractalNoise2D(parameters.Seed, parameters.Octaves,
                parameters.Lacunarity, parameters.Persistence, parameters.Scale,
                parameters.Mode, parameters.Offset, parameters.Gain);

            var heights = new float[resolution, resolution];

            if (parameters.Mode == FractalMode.BasicFbm)
            {
                // Fixed-weight path: fBm values already live in [0,1], so shaping applies
                // point by point in a single pass.
                for (int z = 0; z < resolution; z++)
                {
                    for (int x = 0; x < resolution; x++)
                    {
                        float h = noise.Sample(x * cell, z * cell);

                        // Terrace quantization: floor(h·L)/L folds the continuous height
                        // into L steps (skipped entirely when disabled).
                        if (parameters.TerraceLevels > 0)
                        {
                            float levels = parameters.TerraceLevels;
                            h = Mathf.Floor(h * levels) / levels;
                        }

                        // Power remap: e>1 flattens valleys and stretches peaks.
                        heights[z, x] = Mathf.Pow(h, parameters.HeightExponent);
                    }
                }
            }
            else
            {
                // Feedback path, first pass: raw accumulation — the altitude feedback
                // makes values asymmetric and unbounded, so min/max are tracked too.
                float min = float.MaxValue, max = float.MinValue;
                for (int z = 0; z < resolution; z++)
                {
                    for (int x = 0; x < resolution; x++)
                    {
                        float h = noise.SampleRaw(x * cell, z * cell);
                        heights[z, x] = h;
                        min = Mathf.Min(min, h);
                        max = Mathf.Max(max, h);
                    }
                }

                // Second pass: per-map min/max normalization into [0,1] (pure and
                // deterministic — the map is a pure function of the parameters), then the
                // same shaping steps as the basic path.
                float range = max - min;
                for (int z = 0; z < resolution; z++)
                {
                    for (int x = 0; x < resolution; x++)
                    {
                        float h = range > 0f ? (heights[z, x] - min) / range : 0.5f;

                        if (parameters.TerraceLevels > 0)
                        {
                            float levels = parameters.TerraceLevels;
                            h = Mathf.Floor(h * levels) / levels;
                        }

                        heights[z, x] = Mathf.Pow(h, parameters.HeightExponent);
                    }
                }
            }
            return heights;
        }
    }
}


