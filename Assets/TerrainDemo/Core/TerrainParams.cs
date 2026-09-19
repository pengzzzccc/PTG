// ─────────────────────────────────────────────────────────────────────────────
// File: Core/TerrainParams.cs
// Module: Procedural terrain generation · Core (pure data)
// Status: Fully implemented (three presets + shaping fields)
// ─────────────────────────────────────────────────────────────────────────────

using System;
using UnityEngine;

namespace TerrainDemo.Core
{
    /// <summary>
    /// Every parameter the terrain generator needs. Serializable, so it can be tweaked in
    /// the Inspector and stored as presets. Apart from Seed, each parameter only changes
    /// "looks", not determinism — same seed with different parameters is a different
    /// terrain, and so is the same parameter with a different seed.
    /// </summary>
    [Serializable]
    public class TerrainParams
    {
        /// <summary>The random seed — the single source of randomness for the whole system (zero is legal).</summary>
        public int Seed = 1;

        /// <summary>Heightmap edge length in grid points (not cells); 257 means 256×256 cells.</summary>
        public int Resolution = 257;

        /// <summary>Terrain edge length in meters (real-world size).</summary>
        public float WorldSize = 200f;

        /// <summary>Maximum height in meters; heightmap values in [0,1] are multiplied by it.</summary>
        public float HeightScale = 30f;

        /// <summary>fBm base wavelength in meters: the feature size of octave 0; larger means gentler terrain.</summary>
        public float Scale = 120f;

        /// <summary>Octave count: how many layers are stacked from low to high frequency.</summary>
        public int Octaves = 5;

        /// <summary>Lacunarity: per-layer frequency multiplier; the classic value is 2.</summary>
        public float Lacunarity = 2f;

        /// <summary>Persistence: per-layer amplitude multiplier; the classic value is 0.5, larger is noisier.</summary>
        public float Persistence = 0.5f;

        /// <summary>Height exponent: remaps the normalized height to h^e; e&gt;1 flattens valleys and sharpens peaks.</summary>
        public float HeightExponent = 1f;

        /// <summary>Terrace levels: 0 disables; &gt;0 quantizes the height to that many steps before the power remap (mesa shaping).</summary>
        [Range(0, 64)] public int TerraceLevels = 0;

        /// <summary>Fractal layering mode: fixed-weight fBm or the altitude-feedback variants of Musgrave et al. (1989), §3.3.</summary>
        public FractalMode Mode = FractalMode.BasicFbm;

        /// <summary>Feedback offset. Multifractal uses ≈0.7 to lift the noise above zero; RidgedMultifractal needs ≈1.0 so the squared inversion never flips sign.</summary>
        public float Offset = 0.7f;

        /// <summary>Feedback gain for the weight update; larger values concentrate detail onto peaks.</summary>
        public float Gain = 2f;

        /// <summary>Preset 1: rolling hills — few layers, gentle amplitudes; showcases the low-frequency structure itself.</summary>
        public static TerrainParams RollingHills()
        {
            return new TerrainParams
            {
                Seed = 1,
                Scale = 150f,
                Octaves = 4,
                Persistence = 0.45f,
                HeightScale = 18f,
                HeightExponent = 1.2f,
            };
        }

        /// <summary>Preset 2: rugged peaks — Multifractal with altitude feedback (offset 1.165, gain 2.64), hand-tuned against the rendered result; low foothills stay smooth while the summit massif grows jagged.</summary>
        public static TerrainParams RuggedPeaks()
        {
            return new TerrainParams
            {
                Seed = 1,
                Scale = 175f,
                Octaves = 7,
                Lacunarity = 1.96f,
                Persistence = 0.43f,
                HeightScale = 45f,
                HeightExponent = 2.33f,
                Mode = FractalMode.Multifractal,
                Offset = 1.165f,
                Gain = 2.64f,
            };
        }

        /// <summary>Preset 3: terraced mesas — quantization remap produces flat-topped plateaus; showcases shaping tools.</summary>
        public static TerrainParams TerracedMesas()
        {
            return new TerrainParams
            {
                Seed = 1,
                Scale = 130f,
                Octaves = 5,
                Persistence = 0.5f,
                HeightScale = 30f,
                HeightExponent = 1f,
                TerraceLevels = 8,
            };
        }
    }
}

// ── Implementation notes ─────────────────────────────────────────────────────
//
// Purpose. Collect every decision involved in "generating one mountain" into a single
// serializable object. The talk defines controllability as "exact reproduction from a
// seed", but far more is controllable: wavelength, layer count, persistence and the
// exponent together define a terrain's character. Centralizing parameters gives the
// determinism claim "same seed + same parameters ⇒ same terrain" a concrete input
// definition, and the three presets correspond to the deliverable's "three terrains
// showing different structural features".
//
// Principle. The class is a [Serializable] pure data container with no behaviour, so it
// serializes safely, edits live in the Inspector, and is constructed by tests through
// object initializers. The defaults are the classic combination (resolution 257, size
// 200 m, wavelength 120 m, five layers, 2/0.5), which produces roughly "one mountain"
// in a 200 m square and anchors the performance estimate (~66k vertices). Family and
// TerraceLevels are two later-added shaping dimensions: the former switches the noise
// family (demo/comparison of both styles), the latter folds heights into L quantized
// steps via floor(h·L)/L for flat-topped mesas — defaults (Perlin, 0) leave existing
// parameters untouched.
//
// Approach. The three presets deliberately share Seed=1, so when switching presets all
// terrain differences are attributable to parameters alone — much more persuasive in a
// demo. Each targets one feature: hills show low-frequency structure, peaks show
// high-frequency detail and exponent contrast, mesas show quantization shaping. The
// exact numbers are meant to be tuned against the rendered result; presets are just
// three well-separated, characterful points in parameter space.
// ─────────────────────────────────────────────────────────────────────────────
