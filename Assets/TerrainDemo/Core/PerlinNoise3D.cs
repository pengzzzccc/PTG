using UnityEngine;

namespace TerrainDemo.Core
{
    public class PerlinNoise3D
    {
        private readonly int[] _table = new int[512];

        public PerlinNoise3D(int seed)
        {
            var rng = new SeededRng(seed);
            var permutation = new int[256];
            for (int i = 0; i < 256; i++) permutation[i] = i;
            for (int i = 255; i > 0; i--)
            {
                int j = (int)(rng.Next01() * (i + 1));
                (permutation[j], permutation[i]) = (permutation[i], permutation[j]);
            }
            for (int i = 0; i < 512; i++) _table[i] = permutation[i & 255];
        }

        public float Noise(float x, float y, float z)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y), z0 = Mathf.FloorToInt(z);
            float fx = x - x0, fy = y - y0, fz = z - z0;
            int xi = x0 & 255, yi = y0 & 255, zi = z0 & 255;

            float u = Fade(fx), v = Fade(fy), w = Fade(fz);

            int a = _table[xi] + yi, aa = _table[a] + zi, ab = _table[a + 1] + zi;
            int b = _table[xi + 1] + yi, ba = _table[b] + zi, bb = _table[b + 1] + zi;

            float x00 = Mathf.Lerp(Grad(_table[aa], fx, fy, fz), Grad(_table[ba], fx - 1, fy, fz), u);
            float x10 = Mathf.Lerp(Grad(_table[ab], fx, fy - 1, fz), Grad(_table[bb], fx - 1, fy - 1, fz), u);
            float x01 = Mathf.Lerp(Grad(_table[aa + 1], fx, fy, fz - 1), Grad(_table[ba + 1], fx - 1, fy, fz - 1), u);
            float x11 = Mathf.Lerp(Grad(_table[ab + 1], fx, fy - 1, fz - 1), Grad(_table[bb + 1], fx - 1, fy - 1, fz - 1), u);

            return Mathf.Lerp(Mathf.Lerp(x00, x10, v), Mathf.Lerp(x01, x11, v), w);
        }

        private static float Fade(float t) => t * t * t * (t * (6 * t - 15) + 10);

        private static float Grad(int hash, float x, float y, float z)
        {
            int h = hash & 15;
            float a = h < 8 ? x : y;
            float b = h < 4 ? y : (h == 12 || h == 14 ? x : z);
            return ((h & 1) == 0 ? a : -a) + ((h & 2) == 0 ? b : -b);
        }
    }
}
