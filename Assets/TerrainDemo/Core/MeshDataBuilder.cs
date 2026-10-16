using UnityEngine;

namespace TerrainDemo.Core
{
    public struct MeshData
    {
        public Vector3[] Vertices;

        public int[] Triangles;

        public Vector2[] UVs;
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
    }
}
