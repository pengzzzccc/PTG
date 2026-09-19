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

// ── Implementation notes ─────────────────────────────────────────────────────
//
// Purpose. This step turns the "mathematical definition of noise" into "a concrete,
// comparable table of data". The talk's pipeline reads: layered noise builds structure
// and detail, the seed controls reproducibility, then the heightmap becomes a mesh.
// This class is where the first two meet: input is only parameters, output is only
// data, and no scene object is touched in between. Because input and output are both
// pure data, "same seed + same parameters ⇒ bitwise identical heightmap" is directly
// testable — this is where controllability stops being theory and becomes a fact.
//
// Principle. Both paths share the same front: a row-major double loop over
// [Resolution, Resolution], converting grid indices to world coordinates (x, z) with
// cell = WorldSize/(Resolution−1). The fixed-weight path samples the normalized fBm
// directly; the feedback path samples the raw altitude accumulation and then normalizes
// the whole map by its own min/max — the local-normalization pattern, required because
// feedback values are unbounded and per-point normalization is impossible. Quantization
// (terrace steps) and the power remap then apply as per-point 1D shapers that never
// break determinism. Determinism overall rests on three layers: randomness only from
// the seed-constructed noise instance, a fixed loop order, and bitwise-deterministic
// float arithmetic throughout.
//
// Approach. The noise instance is built once outside the loops; the loops only sample.
// The basic path is kept byte-identical to its pre-feedback form because existing
// tests compare it bitwise. The shaping steps are duplicated across the two paths on
// purpose: merging them into one shared pass would change the basic path's operation
// order and break bitwise reproducibility for a purely cosmetic deduplication. At
// large resolutions this step dominates memory and time (257² floats ≈ 260 KB,
// milliseconds); if performance work ever starts, this is the first candidate to move
// to the Job System, and the pure-function interface already reserves room for it.
// ─────────────────────────────────────────────────────────────────────────────
