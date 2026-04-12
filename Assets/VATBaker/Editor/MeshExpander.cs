using UnityEngine;
using System.Collections.Generic;

namespace VATBaker
{
    public static class MeshExpander
    {
        public struct ExpandedMesh
        {
            public string name;
            public Vector3[] positions; // length = faceCount * 3
            public Vector3[] normals;
            public Vector2[] uvs;
            public int faceCount;
        }

        /// <summary>
        /// Expands a shared-vertex (indexed) mesh into an unshared vertex layout.
        /// Each triangle corner becomes a unique vertex — no index reuse.
        /// This eliminates topology mismatch: all meshes use the same sequential
        /// triangle structure [0,1,2], [3,4,5], ... regardless of original connectivity.
        /// </summary>
        public static ExpandedMesh Expand(Mesh source, bool flattenSubmeshes = true)
        {
            Vector3[] srcPositions = source.vertices;
            Vector3[] srcNormals   = source.normals;
            Vector2[] srcUVs       = source.uv;

            bool hasNormals = srcNormals != null && srcNormals.Length == srcPositions.Length;
            bool hasUVs     = srcUVs     != null && srcUVs.Length     == srcPositions.Length;

            // Collect triangle indices across submeshes
            var allIndices = new List<int>();
            int submeshCount = flattenSubmeshes ? source.subMeshCount : 1;
            for (int s = 0; s < submeshCount; s++)
                allIndices.AddRange(source.GetTriangles(s));

            int faceCount  = allIndices.Count / 3;
            int totalVerts = faceCount * 3;

            var outPos  = new Vector3[totalVerts];
            var outNorm = new Vector3[totalVerts];
            var outUV   = new Vector2[totalVerts];

            for (int i = 0; i < allIndices.Count; i++)
            {
                int srcIdx = allIndices[i];
                outPos[i]  = srcPositions[srcIdx];
                outNorm[i] = hasNormals ? srcNormals[srcIdx] : Vector3.up;
                outUV[i]   = hasUVs     ? srcUVs[srcIdx]     : Vector2.zero;
            }

            return new ExpandedMesh
            {
                name      = source.name,
                positions = outPos,
                normals   = outNorm,
                uvs       = outUV,
                faceCount = faceCount
            };
        }

        /// <summary>
        /// Expands all meshes and returns the maximum expanded vertex count
        /// (= maxFaceCount * 3) which determines the base mesh size and atlas width.
        /// </summary>
        public static List<ExpandedMesh> ExpandAll(Mesh[] meshes, bool flattenSubmeshes, out int maxExpandedVertCount)
        {
            var result = new List<ExpandedMesh>(meshes.Length);
            maxExpandedVertCount = 0;

            foreach (var mesh in meshes)
            {
                if (mesh == null) continue;
                var expanded = Expand(mesh, flattenSubmeshes);
                result.Add(expanded);
                int verts = expanded.faceCount * 3;
                if (verts > maxExpandedVertCount)
                    maxExpandedVertCount = verts;
            }

            return result;
        }
    }
}
