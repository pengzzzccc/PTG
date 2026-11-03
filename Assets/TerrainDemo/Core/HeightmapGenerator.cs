using System;
using System.Threading.Tasks;
using UnityEngine;

namespace TerrainDemo.Core
{
    public static class HeightmapGenerator
    {
        public static float[,] Generate(TerrainParams parameters, bool parallel = true)
        {
            int resolution = parameters.Resolution;
            float cell = parameters.WorldSize / (resolution - 1);
            var noise = CreateNoise(parameters);

            var heights = new float[resolution, resolution];
            var rowMin = new float[resolution];
            var rowMax = new float[resolution];
            ForRows(resolution, parallel, z =>
            {
                float min = float.MaxValue, max = float.MinValue;
                for (int x = 0; x < resolution; x++)
                {
                    float h = noise.SampleRaw(x * cell, z * cell);
                    heights[z, x] = h;
                    min = Mathf.Min(min, h);
                    max = Mathf.Max(max, h);
                }
                rowMin[z] = min;
                rowMax[z] = max;
            });

            Reduce(rowMin, rowMax, out float globalMin, out float range);

            bool island = parameters.Shape == TerrainShape.Island;
            float center = (resolution - 1) * 0.5f;
            float inner = parameters.IslandRadius - parameters.IslandFalloff;
            float outer = parameters.IslandRadius;
            int terrace = parameters.TerraceLevels;
            float exponent = parameters.HeightExponent;

            ForRows(resolution, parallel, z =>
            {
                for (int x = 0; x < resolution; x++)
                {
                    float h = Normalize(heights[z, x], globalMin, range);
                    if (island)
                    {
                        float dx = (x - center) / center, dz = (z - center) / center;
                        h *= IslandMask(Mathf.Sqrt(dx * dx + dz * dz), inner, outer);
                    }
                    heights[z, x] = Shape(h, terrace, exponent);
                }
            });
            return heights;
        }

        internal static FractalNoise2D CreateNoise(TerrainParams parameters)
        {
            return new FractalNoise2D(parameters.Seed, parameters.Octaves, parameters.Lacunarity,
                parameters.Persistence, parameters.Scale, parameters.Offset, parameters.Gain);
        }

        internal static void Reduce(float[] rowMin, float[] rowMax, out float globalMin, out float range)
        {
            globalMin = float.MaxValue;
            float globalMax = float.MinValue;
            for (int i = 0; i < rowMin.Length; i++)
            {
                globalMin = Mathf.Min(globalMin, rowMin[i]);
                globalMax = Mathf.Max(globalMax, rowMax[i]);
            }
            range = globalMax - globalMin;
        }

        internal static float Normalize(float raw, float min, float range)
        {
            return range > 0f ? (raw - min) / range : 0.5f;
        }

        internal static float IslandMask(float distance, float inner, float outer)
        {
            if (distance <= inner) return 1f;
            if (distance >= outer) return 0f;
            return 1f - Mathf.SmoothStep(0f, 1f, (distance - inner) / (outer - inner));
        }

        internal static void ForRows(int rows, bool parallel, Action<int> body)
        {
            if (parallel)
            {
                Parallel.For(0, rows, body);
            }
            else
            {
                for (int z = 0; z < rows; z++) body(z);
            }
        }

        internal static float Shape(float h, int terrace, float exponent)
        {
            if (terrace > 0)
            {
                float levels = terrace;
                h = Mathf.Floor(h * levels) / levels;
            }
            return Mathf.Pow(h, exponent);
        }
    }
}
