using System.Collections.Generic;
using UnityEngine;

namespace TerrainDemo.Core
{
    public struct MeshData
    {
        public Vector3[] Vertices;

        public int[] Triangles;

        public Vector2[] UVs;

        public float[] Slopes;

        public Color[] colors;
    }


    public static class MeshDataBuilder
    {
        private const float EdgeWidth = 0.05f;

        private struct LayerRange
        {
            public Color Color;
            public float HeightMin, HeightMax, SlopeMin, SlopeMax;
            public float HeightLow, HeightHigh, SlopeLow, SlopeHigh;
            public float HeightInnerLow, HeightInnerHigh, SlopeInnerLow, SlopeInnerHigh;
        }

        public static MeshData Build(float[,] heights, TerrainParams parameters, bool parallel = true)
        {
            MeshData mesh = BuildGeometry(heights, parameters, parallel);
            mesh.colors = BuildColors(heights, mesh.Slopes, parameters, parallel);
            return mesh;
        }

        public static MeshData BuildGeometry(float[,] heights, TerrainParams parameters, bool parallel = true)
        {
            int resolution = parameters.Resolution;
            float cell = parameters.WorldSize / (resolution - 1);
            int vertexCount = resolution * resolution;

            var mesh = new MeshData
            {
                Vertices = new Vector3[vertexCount],
                UVs = new Vector2[vertexCount],
                Triangles = new int[(resolution - 1) * (resolution - 1) * 6],
                Slopes = new float[vertexCount],
            };

            Vector3[] vertices = mesh.Vertices;
            Vector2[] uvs = mesh.UVs;
            float[] slopes = mesh.Slopes;
            int[] triangles = mesh.Triangles;
            float heightScale = parameters.HeightScale;

            HeightmapGenerator.ForRows(resolution, parallel, z =>
            {
                for (int x = 0; x < resolution; x++)
                {
                    int index = z * resolution + x;
                    vertices[index] = new Vector3(
                        x * cell,
                        heights[z, x] * heightScale,
                        z * cell);
                    uvs[index] = new Vector2(
                        x / (float)(resolution - 1),
                        z / (float)(resolution - 1));
                    slopes[index] = GetSlope(heights, x, z, resolution, cell, heightScale);
                }
            });

            HeightmapGenerator.ForRows(resolution - 1, parallel, z =>
            {
                int t = z * (resolution - 1) * 6;
                for (int x = 0; x < resolution - 1; x++)
                {
                    int i = z * resolution + x;
                    triangles[t++] = i;
                    triangles[t++] = i + resolution;
                    triangles[t++] = i + 1;
                    triangles[t++] = i + 1;
                    triangles[t++] = i + resolution;
                    triangles[t++] = i + resolution + 1;
                }
            });
            return mesh;
        }

        public static Color[] BuildColors(float[,] heights, float[] slopes, TerrainParams parameters, bool parallel = true)
        {
            int resolution = parameters.Resolution;
            var colors = new Color[resolution * resolution];

            List<ColorLayer> layers = parameters.ColorLayers;
            if (layers == null || layers.Count == 0) layers = TerrainParams.DefaultColorLayers();
            int layerCount = Mathf.Clamp(parameters.ColorCount, 1, layers.Count);
            LayerRange[] ranges = BuildRanges(layers, layerCount);

            HeightmapGenerator.ForRows(resolution, parallel, z =>
            {
                for (int x = 0; x < resolution; x++)
                {
                    int index = z * resolution + x;
                    colors[index] = GetColor(heights[z, x], slopes[index], ranges);
                }
            });
            return colors;
        }

        private static LayerRange[] BuildRanges(List<ColorLayer> layers, int layerCount)
        {
            float half = EdgeWidth * 0.5f;
            var ranges = new LayerRange[layerCount];
            for (int i = 0; i < layerCount; i++)
            {
                ColorLayer layer = layers[i];
                bool openHeightMin = layer.HeightMin <= 0f;
                bool openHeightMax = layer.HeightMax >= 1f;
                bool openSlopeMin = layer.SlopeMin <= 0f;
                bool openSlopeMax = layer.SlopeMax >= 1f;

                ranges[i] = new LayerRange
                {
                    Color = layer.Color,
                    HeightMin = layer.HeightMin,
                    HeightMax = layer.HeightMax,
                    SlopeMin = layer.SlopeMin,
                    SlopeMax = layer.SlopeMax,
                    HeightLow = openHeightMin ? float.NegativeInfinity : layer.HeightMin - half,
                    HeightHigh = openHeightMax ? float.PositiveInfinity : layer.HeightMax + half,
                    SlopeLow = openSlopeMin ? float.NegativeInfinity : layer.SlopeMin - half,
                    SlopeHigh = openSlopeMax ? float.PositiveInfinity : layer.SlopeMax + half,
                    HeightInnerLow = openHeightMin ? float.NegativeInfinity : layer.HeightMin + half,
                    HeightInnerHigh = openHeightMax ? float.PositiveInfinity : layer.HeightMax - half,
                    SlopeInnerLow = openSlopeMin ? float.NegativeInfinity : layer.SlopeMin + half,
                    SlopeInnerHigh = openSlopeMax ? float.PositiveInfinity : layer.SlopeMax - half,
                };
            }
            return ranges;
        }

        private static float GetSlope(float[,] heights, int x, int z, int resolution, float cell, float heightScale)
        {
            //get height at four point
            float hb = heights[Mathf.Max(z - 1, 0), x];
            float ht = heights[Mathf.Min(z + 1, resolution - 1), x];
            float hr = heights[z, Mathf.Min(x + 1, resolution - 1)];
            float hl = heights[z, Mathf.Max(x - 1, 0)];

            //slop
            float dx = (hr - hl) * heightScale / (2f * cell);
            float dz = (ht - hb) * heightScale / (2f * cell);

            Vector3 normal = new Vector3(-dx, 1f, -dz).normalized;
            return 1f - normal.y;
        }

        private static Color GetColor(float h, float slope, LayerRange[] ranges)
        {
            Color c = ranges[0].Color;
            int start = 0;

            for (int i = ranges.Length - 1; i >= 0; i--)
            {
                LayerRange r = ranges[i];
                if (h > r.HeightInnerLow && h < r.HeightInnerHigh && slope > r.SlopeInnerLow && slope < r.SlopeInnerHigh)
                {
                    c = r.Color;
                    start = i + 1;
                    break;
                }
            }

            for (int i = start; i < ranges.Length; i++)
            {
                LayerRange r = ranges[i];
                if (h <= r.HeightLow || h >= r.HeightHigh || slope <= r.SlopeLow || slope >= r.SlopeHigh) continue;

                float weight = RangeWeight(h, r.HeightMin, r.HeightMax) * RangeWeight(slope, r.SlopeMin, r.SlopeMax);
                c = Color.Lerp(c, r.Color, weight);
            }
            return c;
        }

        private static float RangeWeight(float value, float min, float max)
        {
            float low = min <= 0f ? 1f : Mathf.SmoothStep(0f, 1f, (value - min) / EdgeWidth + 0.5f);
            float high = max >= 1f ? 1f : Mathf.SmoothStep(0f, 1f, (max - value) / EdgeWidth + 0.5f);
            return low * high;
        }
    }
}
