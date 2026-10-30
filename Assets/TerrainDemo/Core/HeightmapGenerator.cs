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
            var noise = new FractalNoise2D(parameters.Seed, parameters.Octaves,
                parameters.Lacunarity, parameters.Persistence, parameters.Scale,
                parameters.Mode, parameters.Offset, parameters.Gain);

            var heights = new float[resolution, resolution];
            int terrace = parameters.TerraceLevels;
            float exponent = parameters.HeightExponent;

            if (parameters.Mode == FractalMode.BasicFbm)
            {
                ForRows(resolution, parallel, z =>
                {
                    for (int x = 0; x < resolution; x++)
                    {
                        heights[z, x] = Shape(noise.Sample(x * cell, z * cell), terrace, exponent);
                    }
                });
            }
            else
            {
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

                float globalMin = float.MaxValue, globalMax = float.MinValue;
                for (int z = 0; z < resolution; z++)
                {
                    globalMin = Mathf.Min(globalMin, rowMin[z]);
                    globalMax = Mathf.Max(globalMax, rowMax[z]);
                }

                float range = globalMax - globalMin;
                ForRows(resolution, parallel, z =>
                {
                    for (int x = 0; x < resolution; x++)
                    {
                        float h = range > 0f ? (heights[z, x] - globalMin) / range : 0.5f;
                        heights[z, x] = Shape(h, terrace, exponent);
                    }
                });
            }
            return heights;
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

        private static float Shape(float h, int terrace, float exponent)
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
