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
        private readonly int[] FT = new int[512];
        public int[] Permutation { get; private set; }         

        private readonly float[] GradintX = {1f, -1f, 0f, 0f, 0.70710678f, -0.70710678f, 0.70710678f, -0.70710678f};
        private readonly float[] GradintY = {0f, 0f, 1f, -1f, 0.70710678f, 0.70710678f, -0.70710678f, -0.70710678f};

        // build noise from sudo seed
        public PerlinNoise2D(int seed)
        {
            var ranSeed = new SeededRng(seed);

            // init randam table
            Permutation = new int[256];
            for(int i = 0; i < 256; i++)
            {
                Permutation[i] = i;
            }

            // refish
            int j;
            for(int i = 255; i > 0; i--)
            {
                j = (int)(ranSeed.Next01() * (i + 1)); 
                (Permutation[j], Permutation[i]) = (Permutation[i], Permutation[j]);
            }

            // init double table
            for(int i = 0; i < 512; i++)
            {
                FT[i] = Permutation[i & 255]; // same as % 256
            }
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

            float u = fade(fx), v = fade(fy);

            int aa = FT[FT[x0 & 255] + (y0 & 255)] & 7;
            int ab = FT[FT[x0 & 255] + ((y0+1) & 255)] & 7;
            int ba = FT[FT[(x0+1) & 255] + (y0 & 255)] & 7;
            int bb = FT[FT[(x0+1) & 255] + ((y0+1) & 255)] & 7;

            float d00 = GradintX[aa] * fx + GradintY[aa] * fy;
            float d01 = GradintX[ab] * fx + GradintY[ab] * (fy - 1);
            float d10 = GradintX[ba] * (fx - 1) + GradintY[ba] * fy;
            float d11 = GradintX[bb] * (fx - 1) + GradintY[bb] * (fy - 1);

            return Mathf.Lerp(Mathf.Lerp(d00, d10, u), Mathf.Lerp(d01, d11, u), v);

        }

        private float fade(float x) => x * x * x * (x * (6 * x - 15) + 10);

    }
}


