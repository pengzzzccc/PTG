using System.Collections.Generic;
using UnityEngine;

namespace TerrainDemo.Core
{
    public struct MeshData
    {
        public Vector3[] Vertices;

        public int[] Triangles;

        public Vector2[] UVs;

        public Color[] colors;
    }


    public static class MeshDataBuilder
    {
        public static MeshData Build(float[,] heights, TerrainParams parameters)
        {
            int resolution = parameters.Resolution;
            float cell = parameters.WorldSize / (resolution - 1);
            int vertexCount = resolution * resolution;

            List<ColorLayer> layers = parameters.ColorLayers;
            if (layers == null || layers.Count == 0) layers = TerrainParams.DefaultColorLayers();
            int layerCount = Mathf.Clamp(parameters.ColorCount, 1, layers.Count);

            var mesh = new MeshData
            {
                Vertices = new Vector3[vertexCount],
                UVs = new Vector2[vertexCount],
                Triangles = new int[(resolution - 1) * (resolution - 1) * 6],
                colors = new Color[vertexCount],
            };

            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int index = z * resolution + x;
                    mesh.Vertices[index] = new Vector3(
                        x * cell,
                        heights[z, x] * parameters.HeightScale,
                        z * cell);
                    mesh.UVs[index] = new Vector2(
                        x / (float)(resolution - 1),
                        z / (float)(resolution - 1));
                    mesh.colors[index] = GetColor(heights, x, z, resolution, cell, parameters.HeightScale, layers, layerCount);
                }
            }

            int t = 0;
            for (int z = 0; z < resolution - 1; z++)
            {
                for (int x = 0; x < resolution - 1; x++)
                {
                    int i = z * resolution + x;
                    mesh.Triangles[t++] = i;
                    mesh.Triangles[t++] = i + resolution;
                    mesh.Triangles[t++] = i + 1;
                    mesh.Triangles[t++] = i + 1;
                    mesh.Triangles[t++] = i + resolution;
                    mesh.Triangles[t++] = i + resolution + 1;
                }
            }
            return mesh;
        }


        private const float EdgeWidth = 0.05f;

        private static Color GetColor(float[,] heights, int x, int z, int resolution, float cell, float heightScale,
            List<ColorLayer> layers, int layerCount)
        {
            //get height at four point
            float h = heights[z, x];
            float hb = heights[Mathf.Max(z - 1, 0), x];
            float ht = heights[Mathf.Min(z + 1, resolution - 1), x];
            float hr = heights[z, Mathf.Min(x + 1, resolution - 1)];
            float hl = heights[z, Mathf.Max(x - 1, 0)];

            //slop
            float dx = (hr - hl) * heightScale / (2f * cell);
            float dz = (ht - hb) * heightScale / (2f * cell);

            Vector3 normal = new Vector3(-dx, 1f, -dz).normalized;
            float slop = 1f - normal.y;

            Color c = layers[0].Color;
            for (int i = 0; i < layerCount; i++)
            {
                ColorLayer layer = layers[i];
                float weight = RangeWeight(h, layer.HeightMin, layer.HeightMax)
                    * RangeWeight(slop, layer.SlopeMin, layer.SlopeMax);
                c = Color.Lerp(c, layer.Color, weight);
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
