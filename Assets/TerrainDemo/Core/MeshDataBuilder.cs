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
                    mesh.colors[index] = GetColor(heights, x, z, resolution, cell, parameters.HeightScale);
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


        private static Color DeepWater = new Color(0.00f, 0.24f, 0.55f);
        private static Color Water = new Color(0.39f, 0.63f, 0.86f);
        private static Color lightWater = new Color(0.78f, 0.78f, 1.00f);
        private static Color Sand = new Color(0.76f, 0.70f, 0.50f);
        private static Color Grass = new Color(0.30f, 0.50f, 0.20f);
        private static Color Dirt = new Color(0.45f, 0.38f, 0.30f);
        private static Color Snow = new Color(1.00f, 1.00f, 1.00f);
        private static Color Rock = new Color(0.40f, 0.40f, 0.40f);

        private static Color GetColor(float[,] heights, int x, int z, int resolution, float cell, float heightScale)
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

            // apply color
            Color c = DeepWater;
            c = Color.Lerp(c, Water, Mathf.SmoothStep(0f, 1f, (h - 0.06f) / 0.1f));
            c = Color.Lerp(c, lightWater, Mathf.SmoothStep(0f, 1f, (h - 0.089f) / 0.1f));
            c = Color.Lerp(c, Sand, Mathf.SmoothStep(0f, 1f, (h - 0.09f) / 0.1f));
            c = Color.Lerp(c, Grass, Mathf.SmoothStep(0f, 1f, (h - 0.1f) / 0.1f));
            c = Color.Lerp(c, Dirt, Mathf.SmoothStep(0f, 1f, (h - 0.55f) / 0.1f));
            c = Color.Lerp(c, Snow, Mathf.SmoothStep(0f, 1f, (h - 0.75f) / 0.1f));
            c = Color.Lerp(c, Rock, Mathf.SmoothStep(0f, 1f, (slop - 0.35f) / 0.1f));
            return c;
        }
    }
}
