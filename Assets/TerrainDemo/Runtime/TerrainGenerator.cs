using System.Text;
using System.Diagnostics;
using TerrainDemo.Core;
using UnityEngine;

namespace TerrainDemo
{
    [RequireComponent(typeof(MeshFilter))]
    public class TerrainGenerator : MonoBehaviour
    {
        [SerializeField] private TerrainParams parameters = new TerrainParams();

        public string LastStageStats = "";

        public TerrainParams Parameters
        {
            get => parameters;
            set => parameters = value;
        }

        [ContextMenu("Generate")]
        public void Generate()
        {
            var meshFilter = GetComponent<MeshFilter>();
            var stats = new StringBuilder();
            var clock = Stopwatch.StartNew();

            var fractal = new FractalNoise2D(parameters.Seed, parameters.Octaves, parameters.Lacunarity, parameters.Persistence, parameters.Scale);
            clock.Stop();
            stats.AppendFormat("Noise {0:F1}ms | ", clock.Elapsed.TotalMilliseconds);

            clock.Restart();
            float[,] heights = HeightmapGenerator.Generate(parameters);
            clock.Stop();
            stats.AppendFormat("Heightmap {0:F1}ms ({1}x{1}) | ", clock.Elapsed.TotalMilliseconds, parameters.Resolution);

            clock.Restart();
            MeshData meshData = MeshDataBuilder.Build(heights, parameters);
            clock.Stop();
            stats.AppendFormat("Mesh {0:F1}ms | ", clock.Elapsed.TotalMilliseconds);

            clock.Restart();
            if (meshFilter.sharedMesh == null)
            {
                meshFilter.sharedMesh = new Mesh { name = $"Terrain_{parameters.Seed}" };
            }
            Mesh mesh = meshFilter.sharedMesh;
            mesh.Clear();
            mesh.vertices = meshData.Vertices;
            mesh.triangles = meshData.Triangles;
            mesh.uv = meshData.UVs;
            mesh.colors = meshData.colors;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var collider = GetComponent<MeshCollider>();
            if (collider != null) collider.sharedMesh = mesh;
            clock.Stop();
            stats.AppendFormat("Apply {0:F1}ms", clock.Elapsed.TotalMilliseconds);

            LastStageStats = stats.ToString();
        }

        private void Start()
        {
            if (GetComponent<MeshFilter>().sharedMesh == null)
            {
                Generate();
            }
        }
    }
}
