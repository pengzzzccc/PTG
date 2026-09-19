// ─────────────────────────────────────────────────────────────────────────────
// File: Core/MeshDataBuilder.cs
// Module: Procedural terrain generation · Core (pure data, Unity math types only)
// Status: Fully implemented
// ─────────────────────────────────────────────────────────────────────────────

using UnityEngine;

namespace TerrainDemo.Core
{
    /// <summary>
    /// Pure-data container for mesh geometry: vertices, triangle indices, UVs. Decoupled
    /// from UnityEngine.Mesh so it can be built and tested without the engine;
    /// TerrainGenerator flushes it into a real Mesh afterwards.
    /// </summary>
    public struct MeshData
    {
        /// <summary>Vertex array, length = Resolution²; index = z·Resolution + x.</summary>
        public Vector3[] Vertices;

        /// <summary>Triangle index array, length = (Resolution−1)²·6; groups of three.</summary>
        public int[] Triangles;

        /// <summary>UV array, one per vertex, values in [0,1].</summary>
        public Vector2[] UVs;
    }

    /// <summary>
    /// Pure function turning a heightmap into mesh geometry data.
    /// </summary>
    public static class MeshDataBuilder
    {
        /// <summary>
        /// Builds the mesh data. Contract: vertices laid out at index = z·Resolution + x
        /// with position (x·cell, heights[z,x]·HeightScale, z·cell); UV = (x/(R−1), z/(R−1));
        /// two triangles per cell, wound so the cross product's y component is positive
        /// seen from above (normal up); every index within the vertex range.
        /// </summary>
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

            // Vertices and UVs: planar coordinates spread linearly, height lifted by
            // HeightScale, UVs normalized to [0,1].
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

            // Triangulation: two triangles per cell sharing the i → i+res+1 diagonal;
            // the winding (i, i+res, i+1) / (i+1, i+res, i+res+1) makes the cross
            // product's y component exactly cell² > 0.
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

// ── Implementation notes ─────────────────────────────────────────────────────
//
// Purpose. The heightmap is "data"; a renderable mesh is "geometry"; this class is the
// only bridge between them. It is split out of TerrainGenerator because one learning
// goal is familiarity with the Unity Mesh API, and the API only cares about three
// things: where vertices are, how triangles connect, how UVs map. Packing those three
// into pure data lets mesh building be unit-tested item by item like the noise, leaving
// the thin flush-into-Mesh layer in the runtime module where it belongs.
//
// Principle. Regular-grid triangulation is a fixed pattern: one vertex per grid point,
// positioned by planar coordinates plus "height × HeightScale"; each cell splits into
// two triangles, for (R−1)²·6 indices in total. Unity treats clockwise as front-facing;
// substituting the chosen winding into the cross product yields a y component of
// exactly cell² — strictly positive, the algebraic guarantee that the terrain faces up,
// and precisely what the tests assert triangle by triangle. UVs are normalized grid
// coordinates with corners exactly (0,0)/(1,1), ready for materials or height-based
// coloring later. Nothing random happens here; determinism is inherited from the
// heightmap itself.
//
// Approach. The vertex loop and the triangulation loop are written separately: the
// former is trivial to self-check (counts, corner positions), while the latter's two
// triangles share a diagonal — a wrong winding is pinpointed by the "normals up"
// assertion down to which half is broken. Arrays are allocated once and filled by
// index, avoiding per-cell recomputation of shared vertices; at the current scale
// (~66k vertices) this runs in milliseconds, so no premature optimization.
// ─────────────────────────────────────────────────────────────────────────────
