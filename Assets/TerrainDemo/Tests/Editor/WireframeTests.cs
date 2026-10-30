using System.Collections.Generic;
using NUnit.Framework;
using TerrainDemo.Core;

namespace TerrainDemo.Tests
{
    public class WireframeTests
    {
        [TestCase(2)]
        [TestCase(5)]
        [TestCase(33)]
        public void WireIndices_CountMatchesEdgeFormula(int resolution)
        {
            int cells = resolution - 1;
            int[] indices = MeshDataBuilder.BuildWireIndices(resolution);
            Assert.AreEqual(2 * (2 * resolution * cells + cells * cells), indices.Length);
        }

        [Test]
        public void WireIndices_AreAllWithinVertexRange()
        {
            const int resolution = 17;
            foreach (int index in MeshDataBuilder.BuildWireIndices(resolution))
            {
                Assert.That(index, Is.InRange(0, resolution * resolution - 1));
            }
        }

        [Test]
        public void WireIndices_CoverEveryTriangleEdge()
        {
            var parameters = new TerrainParams { Resolution = 9, WorldSize = 100f, HeightScale = 10f };
            var heights = new float[parameters.Resolution, parameters.Resolution];
            MeshData mesh = MeshDataBuilder.BuildGeometry(heights, parameters, false);

            int[] wire = MeshDataBuilder.BuildWireIndices(parameters.Resolution);
            var edges = new HashSet<long>();
            for (int i = 0; i < wire.Length; i += 2) edges.Add(Key(wire[i], wire[i + 1]));

            for (int t = 0; t < mesh.Triangles.Length; t += 3)
            {
                int a = mesh.Triangles[t], b = mesh.Triangles[t + 1], c = mesh.Triangles[t + 2];
                Assert.IsTrue(edges.Contains(Key(a, b)), $"Missing edge {a}-{b}");
                Assert.IsTrue(edges.Contains(Key(b, c)), $"Missing edge {b}-{c}");
                Assert.IsTrue(edges.Contains(Key(c, a)), $"Missing edge {c}-{a}");
            }
        }

        private static long Key(int a, int b)
        {
            int lo = a < b ? a : b;
            int hi = a < b ? b : a;
            return ((long)lo << 32) | (uint)hi;
        }
    }
}
