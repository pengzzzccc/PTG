// ─────────────────────────────────────────────────────────────────────────────
// File: Core/PerlinNoise2D.cs
// Module: Procedural terrain generation · Core layer (pure C#, no Unity runtime)
// Status: Fully implemented (table-family reference implementation)
// ─────────────────────────────────────────────────────────────────────────────

using UnityEngine;

namespace TerrainDemo.Core
{
    /// <summary>
    /// Classic 2D Perlin gradient noise whose permutation table is generated from the
    /// seed, so the noise field itself is reproducible.
    /// </summary>
    public class PerlinNoise2D
    {
        // Eight unit gradient directions: four axis-aligned + four diagonals (normalized by 1/√2)
        private static readonly float[] GradientX = { 1f, -1f, 1f, -1f, 0.70710678f, -0.70710678f, 0.70710678f, -0.70710678f };
        private static readonly float[] GradientY = { 0f, 0f, 0f, 0f, 0.70710678f, 0.70710678f, -0.70710678f, -0.70710678f };

        // A doubled 512-entry table removes the second modulo: perm[perm[X] + Y] stays in range directly.
        private readonly int[] _table = new int[512];

        /// <summary>The seed-shuffled 256-entry permutation table; contract: exactly a permutation of 0..255.</summary>
        public int[] Permutation { get; private set; }

        /// <summary>Builds the noise from an integer seed; instances built from the same seed are bitwise identical.</summary>
        public PerlinNoise2D(int seed)
        {
            var rng = new SeededRng(seed);
            Permutation = new int[256];
            for (int i = 0; i < 256; i++) Permutation[i] = i;

            // Fisher–Yates shuffle: the seed enters the noise field through SeededRng —
            // the key link that carries reproducibility down to the final heightmap.
            // The j>i guard keeps the shuffle valid even under float rounding.
            for (int i = 255; i > 0; i--)
            {
                int j = (int)(rng.Next01() * (i + 1));
                if (j > i) j = i;
                (Permutation[i], Permutation[j]) = (Permutation[j], Permutation[i]);
            }
            for (int i = 0; i < 512; i++) _table[i] = Permutation[i & 255];
        }

        /// <summary>
        /// Samples the 2D noise. Contract: range [-1,1]; every integer lattice point
        /// (negative coordinates included) returns 0; the field is periodic with period
        /// 256, i.e. Noise(x,y) = Noise(x+256,y) = Noise(x,y+256).
        /// </summary>
        public float Noise(float x, float y)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            float u = Fade(fx), v = Fade(fy);

            int aa = Lookup(x0, y0) & 7, ba = Lookup(x0 + 1, y0) & 7;
            int ab = Lookup(x0, y0 + 1) & 7, bb = Lookup(x0 + 1, y0 + 1) & 7;

            float d00 = GradientX[aa] * fx + GradientY[aa] * fy;
            float d10 = GradientX[ba] * (fx - 1f) + GradientY[ba] * fy;
            float d01 = GradientX[ab] * fx + GradientY[ab] * (fy - 1f);
            float d11 = GradientX[bb] * (fx - 1f) + GradientY[bb] * (fy - 1f);

            return Mathf.Lerp(Mathf.Lerp(d00, d10, u), Mathf.Lerp(d01, d11, u), v);
        }

        /// <summary>Double table lookup for a corner's gradient slot; negative coordinates wrap naturally via two's-complement masking.</summary>
        private int Lookup(int x, int y) => _table[_table[x & 255] + (y & 255)];

        /// <summary>Quintic ease: its first derivative vanishes at 0 and 1, keeping the field C1-continuous everywhere.</summary>
        private static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);
    }
}

// ── Implementation notes ─────────────────────────────────────────────────────
//
// Purpose. This is the lowest-level "continuous random field" of the pipeline: every
// fBm octave samples it. The choice follows the noise survey (Lagae et al., 2010) —
// gradient noise buys good isotropy and spectral controllability at very low cost, the
// best speed/quality trade-off for this project. Unity's built-in Mathf.PerlinNoise
// cannot be seeded, so the seed never reaches the noise and controllability fails;
// hence a custom implementation.
//
// Principle. Classic Perlin divides the plane into unit cells, each lattice corner
// hiding a fixed-length gradient vector. Sampling locates the cell, computes fractional
// coordinates, applies the quintic fade, resolves the four corners' gradient indices
// through the permutation table, takes the dot product of "gradient · offset" at each
// corner, and bilinearly interpolates. Integer lattice points returning 0 is a natural
// consequence (the offset vector is zero, so every dot product is zero), and the 256
// period comes from masking coordinates to 8 bits — both free properties are used by
// the tests to verify correctness. The gradient set is 8 fixed unit directions, so the
// theoretical bound |noise| ≤ √2/2 satisfies the range contract with margin.
//
// Approach. The constructor builds the table with a SeededRng-driven Fisher–Yates
// shuffle. Negative coordinates must be floored (a plain cast truncates toward zero and
// breaks continuity), then masked with & 255 — a classic pitfall the tests cover
// explicitly. The doubled table merely folds "mask then index" into one lookup, a
// semantics-free micro-optimization. Noise is stateless and mutates nothing, so any
// number of replays are safe.
// ─────────────────────────────────────────────────────────────────────────────
