using UnityEngine;

namespace TerrainDemo.Core
{
    public static class HeightmapGenerator
    {
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
                for (int z = 0; z < resolution; z++)
                {
                    for (int x = 0; x < resolution; x++)
                    {
                        float h = noise.Sample(x * cell, z * cell);

                        if (parameters.TerraceLevels > 0)
                        {
                            float levels = parameters.TerraceLevels;
                            h = Mathf.Floor(h * levels) / levels;
                        }

                        heights[z, x] = Mathf.Pow(h, parameters.HeightExponent);
                    }
                }
            }
            else
            {
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
