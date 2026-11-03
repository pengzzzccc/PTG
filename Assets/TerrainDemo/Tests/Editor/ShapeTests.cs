using NUnit.Framework;
using TerrainDemo.Core;
using UnityEngine;

namespace TerrainDemo.Tests
{
    public class ShapeTests
    {
        private const float RangeEps = 1e-4f;

        private static TerrainParams MakeParams(TerrainShape shape, int resolution = 33)
        {
            return new TerrainParams
            {
                Seed = 77,
                Resolution = resolution,
                WorldSize = 200f,
                HeightScale = 30f,
                Scale = 120f,
                Octaves = 5,
                Lacunarity = 2f,
                Persistence = 0.5f,
                HeightExponent = 1f,
                Shape = shape,
            };
        }

        private static float MaxDifference(float[,] a, float[,] b)
        {
            float max = 0f;
            for (int z = 0; z < a.GetLength(0); z++)
                for (int x = 0; x < a.GetLength(1); x++)
                    max = Mathf.Max(max, Mathf.Abs(a[z, x] - b[z, x]));
            return max;
        }

        [Test]
        public void Feedback_OffsetChangesHeightmap()
        {
            TerrainParams a = MakeParams(TerrainShape.Flat);
            TerrainParams b = MakeParams(TerrainShape.Flat);
            b.Offset = a.Offset + 0.4f;
            Assert.Greater(MaxDifference(HeightmapGenerator.Generate(a), HeightmapGenerator.Generate(b)), 0.01f);
        }

        [Test]
        public void Feedback_GainChangesHeightmap()
        {
            TerrainParams a = MakeParams(TerrainShape.Flat);
            TerrainParams b = MakeParams(TerrainShape.Flat);
            b.Gain = a.Gain + 1f;
            Assert.Greater(MaxDifference(HeightmapGenerator.Generate(a), HeightmapGenerator.Generate(b)), 0.01f);
        }

        [Test]
        public void Presets_GenerateInRange()
        {
            foreach (TerrainParams preset in new[] { TerrainParams.RollingHills(), TerrainParams.RuggedPeaks(), TerrainParams.TerracedMesas() })
            {
                preset.Resolution = 65;
                float[,] map = HeightmapGenerator.Generate(preset);
                foreach (float h in map) Assert.That(h, Is.InRange(-RangeEps, 1f + RangeEps));
            }
        }

        [Test]
        public void Island_EdgesSinkAndCentreStays()
        {
            TerrainParams p = MakeParams(TerrainShape.Island, 65);
            float[,] map = HeightmapGenerator.Generate(p);
            int last = p.Resolution - 1;
            Assert.AreEqual(0f, map[0, 0]);
            Assert.AreEqual(0f, map[0, last]);
            Assert.AreEqual(0f, map[last, 0]);
            Assert.AreEqual(0f, map[last, last]);
            Assert.AreEqual(0f, map[0, last / 2]);

            TerrainParams flat = MakeParams(TerrainShape.Flat, 65);
            float[,] flatMap = HeightmapGenerator.Generate(flat);
            Assert.AreEqual(flatMap[last / 2, last / 2], map[last / 2, last / 2]);
        }

        [Test]
        public void Planet_HeightsHaveFaceLayoutAndRange()
        {
            TerrainParams p = MakeParams(TerrainShape.Planet, 17);
            float[,] padded = PlanetBuilder.GenerateHeights(p, false);
            Assert.AreEqual(PlanetBuilder.Faces * 19, padded.GetLength(0));
            Assert.AreEqual(19, padded.GetLength(1));

            PlanetBuilder.BuildGeometry(padded, p, out float[,] heights, false);
            Assert.AreEqual(PlanetBuilder.Faces * 17, heights.GetLength(0));
            Assert.AreEqual(17, heights.GetLength(1));
            foreach (float h in heights) Assert.That(h, Is.InRange(-RangeEps, 1f + RangeEps));
        }

        [Test]
        public void Planet_IsDeterministicAndParallelMatchesSerial()
        {
            TerrainParams p = MakeParams(TerrainShape.Planet, 17);
            float[,] serial = PlanetBuilder.GenerateHeights(p, false);
            float[,] parallel = PlanetBuilder.GenerateHeights(p, true);
            Assert.AreEqual(0f, MaxDifference(serial, parallel));

            MeshData a = PlanetBuilder.BuildGeometry(serial, p, out _, false);
            MeshData b = PlanetBuilder.BuildGeometry(serial, p, out _, true);
            CollectionAssert.AreEqual(a.Triangles, b.Triangles);
            for (int i = 0; i < a.Vertices.Length; i++)
            {
                Assert.AreEqual(a.Vertices[i], b.Vertices[i]);
                Assert.AreEqual(a.Normals[i], b.Normals[i]);
            }
        }

        [Test]
        public void Planet_VerticesStayInShellAndFaceOutward()
        {
            TerrainParams p = MakeParams(TerrainShape.Planet, 17);
            MeshData mesh = PlanetBuilder.BuildGeometry(PlanetBuilder.GenerateHeights(p, false), p, out _, false);
            Vector3 center = PlanetBuilder.Center(p);
            float radius = PlanetBuilder.Radius(p);

            Assert.AreEqual(PlanetBuilder.Faces * 17 * 17, mesh.Vertices.Length);
            foreach (Vector3 v in mesh.Vertices)
            {
                float distance = Vector3.Distance(v, center);
                Assert.That(distance, Is.InRange(radius - 1e-3f, radius + p.HeightScale + 1e-3f));
            }

            for (int t = 0; t < mesh.Triangles.Length; t += 3)
            {
                Vector3 a = mesh.Vertices[mesh.Triangles[t]];
                Vector3 b = mesh.Vertices[mesh.Triangles[t + 1]];
                Vector3 c = mesh.Vertices[mesh.Triangles[t + 2]];
                Vector3 normal = Vector3.Cross(b - a, c - a);
                Vector3 radial = (a + b + c) / 3f - center;
                Assert.Greater(Vector3.Dot(normal, radial), 0f, $"Triangle {t / 3} faces inward");
            }
        }

        [Test]
        public void Planet_SharedEdgesMatchAcrossFaces()
        {
            TerrainParams p = MakeParams(TerrainShape.Planet, 9);
            MeshData mesh = PlanetBuilder.BuildGeometry(PlanetBuilder.GenerateHeights(p, false), p, out _, false);
            var seen = new System.Collections.Generic.Dictionary<Vector3Int, Vector3>();
            int matches = 0;
            foreach (Vector3 v in mesh.Vertices)
            {
                Vector3 direction = (v - PlanetBuilder.Center(p)).normalized;
                var key = Vector3Int.RoundToInt(direction * 10000f);
                if (seen.TryGetValue(key, out Vector3 other))
                {
                    Assert.Less(Vector3.Distance(v, other), 1e-3f);
                    matches++;
                }
                else
                {
                    seen[key] = v;
                }
            }
            Assert.Greater(matches, 0);
        }
    }
}
