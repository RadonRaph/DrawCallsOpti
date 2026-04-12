using UnityEngine;
using UnityEngine.Rendering;

namespace VATBaker
{
    public static class BaseMeshBuilder
    {
        /// <summary>
        /// Creates the shared base mesh used for all VAT rendering.
        /// Total vertices = totalVerts (= maxFaceCount * 3).
        /// All vertex positions are zero — actual positions come from the Position atlas at runtime.
        /// UV0 encodes the raw integer vertex index as a float for direct texel Load:
        ///   uv0[i].x = (float)i
        /// Triangles are sequential: [0,1,2], [3,4,5], ...
        /// Bounds are set generously to prevent incorrect frustum culling.
        /// </summary>
        public static Mesh Build(int totalVerts)
        {
            var vertices  = new Vector3[totalVerts]; // all Vector3.zero
            var uv0       = new Vector2[totalVerts];
            var triangles = new int[totalVerts];     // sequential indices

            for (int i = 0; i < totalVerts; i++)
            {
                uv0[i]       = new Vector2((float)i, 0f);
                triangles[i] = i;
            }

            var mesh = new Mesh { name = "VATBaseMesh" };

            if (totalVerts > 65535)
                mesh.indexFormat = IndexFormat.UInt32;

            mesh.vertices  = vertices;
            mesh.uv        = uv0;
            mesh.triangles = triangles;

            // Force generous bounds: since all vertices are at origin, Unity would compute
            // zero-area bounds and permanently frustum-cull the renderer.
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(100f, 100f, 100f));

            return mesh;
        }
    }
}
