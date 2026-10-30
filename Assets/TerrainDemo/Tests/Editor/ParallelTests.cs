using NUnit.Framework;
using TerrainDemo.Core;
using UnityEngine;

namespace TerrainDemo.Tests
{
    public class ParallelTests
    {
        private static TerrainParams MakeParams(FractalMode mode, int terrace = 0)
        {
            return new TerrainParams
            {
                Seed = 12345,
                Resolution = 129,
                WorldSize = 200f,
                HeightScale = 30f,
                Scale = 120f,
                Octaves = 6,
                Lacunarity = 2f,
                Persistence = 0.5f,
                HeightExponent = 1.4f,
                TerraceLevels = terrace,
                Mode = mode,
                Offset = mode == FractalMode.RidgedMultifractal ? 1f : 0.7f,
                Gain = 2f,
            };
        }

        [TestCase(FractalMode.BasicFbm, 0)]
        [TestCase(FractalMode.BasicFbm, 8)]
        [TestCase(FractalMode.Multifractal, 0)]
        [TestCase(FractalMode.RidgedMultifractal, 0)]
        public void Heightmap_ParallelMatchesSerial_Bitwise(FractalMode mode, int terrace)
        {
            TerrainParams p = MakeParams(mode, terrace);
            float[,] serial = HeightmapGenerator.Generate(p, false);
            float[,] parallel = HeightmapGenerator.Generate(p, true);

            for (int z = 0; z < p.Resolution; z++)
            {
                for (int x = 0; x < p.Resolution; x++)
                {
                    Assert.AreEqual(serial[z, x], parallel[z, x], 0f, $"Height differs at ({x},{z})");
                }
            }
        }

        [Test]
        public void Mesh_ParallelMatchesSerial_Bitwise()
        {
            TerrainParams p = MakeParams(FractalMode.Multifractal);
            float[,] heights = HeightmapGenerator.Generate(p, false);
            MeshData serial = MeshDataBuilder.Build(heights, p, false);
            MeshData parallel = MeshDataBuilder.Build(heights, p, true);

            CollectionAssert.AreEqual(serial.Triangles, parallel.Triangles);
            for (int i = 0; i < serial.Vertices.Length; i++)
            {
                Assert.AreEqual(serial.Vertices[i], parallel.Vertices[i], $"Vertex {i} differs");
                Assert.AreEqual(serial.UVs[i], parallel.UVs[i], $"UV {i} differs");
                Assert.AreEqual(serial.Slopes[i], parallel.Slopes[i], 0f, $"Slope {i} differs");
                Assert.AreEqual(serial.colors[i], parallel.colors[i], $"Color {i} differs");
            }
        }
    }
}
