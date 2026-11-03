using System.Collections.Generic;
using UnityEngine;

namespace TerrainDemo.Core
{
    public static class PlanetBuilder
    {
        public const int Faces = 6;

        public const int MaxResolution = 257;

        public const float RadiusFactor = 0.35f;

        private static readonly Vector3[] FaceNormals =
        {
            Vector3.up, Vector3.down, Vector3.right, Vector3.left, Vector3.forward, Vector3.back,
        };

        private static readonly Vector3[] FaceAxisA =
        {
            Vector3.right, Vector3.forward, Vector3.forward, Vector3.up, Vector3.up, Vector3.right,
        };

        private static readonly Vector3[] FaceAxisB =
        {
            Vector3.forward, Vector3.right, Vector3.up, Vector3.forward, Vector3.right, Vector3.up,
        };

        public static int ResolutionFor(TerrainParams parameters)
        {
            return Mathf.Clamp(parameters.Resolution, 2, MaxResolution);
        }

        public static Vector3 Center(TerrainParams parameters)
        {
            return new Vector3(parameters.WorldSize * 0.5f, 0f, parameters.WorldSize * 0.5f);
        }

        public static float Radius(TerrainParams parameters)
        {
            return parameters.WorldSize * RadiusFactor;
        }

        public static Vector3 Direction(int face, int i, int j, int resolution)
        {
            float s = 2f * i / (resolution - 1) - 1f;
            float t = 2f * j / (resolution - 1) - 1f;
            return (FaceNormals[face] + s * FaceAxisA[face] + t * FaceAxisB[face]).normalized;
        }

        public static float[,] GenerateHeights(TerrainParams parameters, bool parallel = true)
        {
            int resolution = ResolutionFor(parameters);
            int padded = resolution + 2;
            int rows = Faces * padded;
            float radius = Radius(parameters);
            FractalNoise2D noise = HeightmapGenerator.CreateNoise(parameters);

            var heights = new float[rows, padded];
            var rowMin = new float[rows];
            var rowMax = new float[rows];

            HeightmapGenerator.ForRows(rows, parallel, row =>
            {
                int face = row / padded;
                int j = row % padded - 1;
                bool interiorRow = j >= 0 && j < resolution;
                float min = float.MaxValue, max = float.MinValue;
                for (int c = 0; c < padded; c++)
                {
                    Vector3 p = Direction(face, c - 1, j, resolution) * radius;
                    float h = noise.SampleRaw(p.x, p.y, p.z);
                    heights[row, c] = h;
                    if (interiorRow && c > 0 && c <= resolution)
                    {
                        min = Mathf.Min(min, h);
                        max = Mathf.Max(max, h);
                    }
                }
                rowMin[row] = min;
                rowMax[row] = max;
            });

            HeightmapGenerator.Reduce(rowMin, rowMax, out float globalMin, out float range);
            int terrace = parameters.TerraceLevels;
            float exponent = parameters.HeightExponent;

            HeightmapGenerator.ForRows(rows, parallel, row =>
            {
                for (int c = 0; c < padded; c++)
                {
                    float h = Mathf.Clamp01(HeightmapGenerator.Normalize(heights[row, c], globalMin, range));
                    heights[row, c] = HeightmapGenerator.Shape(h, terrace, exponent);
                }
            });
            return heights;
        }

        public static MeshData BuildGeometry(float[,] paddedHeights, TerrainParams parameters, out float[,] heights, bool parallel = true)
        {
            int resolution = ResolutionFor(parameters);
            int padded = resolution + 2;
            int faceSize = resolution * resolution;
            int vertexCount = Faces * faceSize;
            int cells = resolution - 1;

            var mesh = new MeshData
            {
                Vertices = new Vector3[vertexCount],
                UVs = new Vector2[vertexCount],
                Normals = new Vector3[vertexCount],
                Slopes = new float[vertexCount],
                Triangles = new int[Faces * cells * cells * 6],
            };
            var interior = new float[Faces * resolution, resolution];

            Vector3[] vertices = mesh.Vertices;
            Vector2[] uvs = mesh.UVs;
            Vector3[] normals = mesh.Normals;
            float[] slopes = mesh.Slopes;
            int[] triangles = mesh.Triangles;
            Vector3 center = Center(parameters);
            float radius = Radius(parameters);
            float heightScale = parameters.HeightScale;

            Vector3 Position(int face, int i, int j)
            {
                float h = paddedHeights[face * padded + j + 1, i + 1];
                return center + Direction(face, i, j, resolution) * (radius + h * heightScale);
            }

            HeightmapGenerator.ForRows(Faces * resolution, parallel, row =>
            {
                int face = row / resolution;
                int j = row % resolution;
                for (int i = 0; i < resolution; i++)
                {
                    int index = face * faceSize + j * resolution + i;
                    interior[row, i] = paddedHeights[face * padded + j + 1, i + 1];

                    Vector3 direction = Direction(face, i, j, resolution);
                    vertices[index] = Position(face, i, j);
                    uvs[index] = new Vector2(i / (float)cells, j / (float)cells);

                    Vector3 alongA = Position(face, i + 1, j) - Position(face, i - 1, j);
                    Vector3 alongB = Position(face, i, j + 1) - Position(face, i, j - 1);
                    Vector3 normal = Vector3.Cross(alongB, alongA).normalized;
                    normals[index] = normal;
                    slopes[index] = Mathf.Clamp01(1f - Vector3.Dot(normal, direction));
                }
            });

            HeightmapGenerator.ForRows(Faces * cells, parallel, row =>
            {
                int face = row / cells;
                int j = row % cells;
                int t = row * cells * 6;
                for (int i = 0; i < cells; i++)
                {
                    int v = face * faceSize + j * resolution + i;
                    triangles[t++] = v;
                    triangles[t++] = v + resolution;
                    triangles[t++] = v + 1;
                    triangles[t++] = v + 1;
                    triangles[t++] = v + resolution;
                    triangles[t++] = v + resolution + 1;
                }
            });

            WeldSeamNormals(mesh, resolution, center);
            heights = interior;
            return mesh;
        }

        private static void WeldSeamNormals(MeshData mesh, int resolution, Vector3 center)
        {
            int faceSize = resolution * resolution;
            int last = resolution - 1;
            var sums = new Dictionary<Vector3Int, Vector3>();
            var seam = new List<int>(Faces * 4 * resolution);

            for (int face = 0; face < Faces; face++)
            {
                for (int j = 0; j < resolution; j++)
                {
                    for (int i = 0; i < resolution; i++)
                    {
                        if (i != 0 && i != last && j != 0 && j != last) continue;
                        int index = face * faceSize + j * resolution + i;
                        seam.Add(index);
                        Vector3Int key = SeamKey(mesh.Vertices[index], center);
                        sums.TryGetValue(key, out Vector3 sum);
                        sums[key] = sum + mesh.Normals[index];
                    }
                }
            }

            foreach (int index in seam)
            {
                Vector3 normal = sums[SeamKey(mesh.Vertices[index], center)].normalized;
                Vector3 direction = (mesh.Vertices[index] - center).normalized;
                mesh.Normals[index] = normal;
                mesh.Slopes[index] = Mathf.Clamp01(1f - Vector3.Dot(normal, direction));
            }
        }

        private static Vector3Int SeamKey(Vector3 vertex, Vector3 center)
        {
            return Vector3Int.RoundToInt((vertex - center).normalized * 1000f);
        }
    }
}
