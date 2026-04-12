using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

namespace VATBaker
{
    public static class TextureBaker
    {
        /// <summary>
        /// Bakes three atlas textures (Position, Normal, UV) from a list of expanded meshes.
        ///
        /// Atlas layout:
        ///   Width  = totalVerts (= maxFaceCount * 3)
        ///   Height = meshes.Count (one row per mesh)
        ///
        /// For meshes with fewer vertices than totalVerts, the remaining columns
        /// are filled with degenerate data: position collapsed to vertex[0] of that
        /// mesh (zero-area triangles that the GPU discards), alpha channel = 0.
        ///
        /// Texture format: RGBAHalf (16-bit float) by default, RGBAFloat if useFullFloat.
        /// All textures: Point filter, Clamp wrap, linear color space (no sRGB).
        /// </summary>
        public static void BakeAtlases(
            List<MeshExpander.ExpandedMesh> meshes,
            int totalVerts,
            string outputPath,
            bool useFullFloat,
            out Texture2D posAtlas,
            out Texture2D normAtlas,
            out Texture2D uvAtlas)
        {
            int meshCount = meshes.Count;

            if (totalVerts > SystemInfo.maxTextureSize)
                Debug.LogWarning($"[VAT Baker] totalVerts ({totalVerts}) exceeds SystemInfo.maxTextureSize " +
                                 $"({SystemInfo.maxTextureSize}). Consider reducing mesh complexity.");

            var format = useFullFloat ? TextureFormat.RGBAFloat : TextureFormat.RGBAHalf;

            posAtlas  = CreateAtlasTexture(totalVerts, meshCount, format);
            normAtlas = CreateAtlasTexture(totalVerts, meshCount, format);
            uvAtlas   = CreateAtlasTexture(totalVerts, meshCount, format);

            var posPixels  = new Color[totalVerts * meshCount];
            var normPixels = new Color[totalVerts * meshCount];
            var uvPixels   = new Color[totalVerts * meshCount];

            for (int meshIdx = 0; meshIdx < meshCount; meshIdx++)
            {
                var em        = meshes[meshIdx];
                int realVerts = em.faceCount * 3;

                // Pre-read vertex[0] for degenerate fill
                Vector3 degenPos = em.positions.Length > 0 ? em.positions[0] : Vector3.zero;

                for (int v = 0; v < totalVerts; v++)
                {
                    int pixelIdx = meshIdx * totalVerts + v;

                    if (v < realVerts)
                    {
                        Vector3 p = em.positions[v];
                        Vector3 n = em.normals[v];
                        Vector2 uv = em.uvs[v];

                        posPixels[pixelIdx]  = new Color(p.x,  p.y,  p.z,  1f);
                        normPixels[pixelIdx] = new Color(n.x,  n.y,  n.z,  1f);
                        uvPixels[pixelIdx]   = new Color(uv.x, uv.y, 0f,   1f);
                    }
                    else
                    {
                        // Degenerate vertex: all 3 corners of extra triangles collapse
                        // to the same position → zero-area → GPU rasterizer discards.
                        // Alpha = 0 flags these as degenerate for shader inspection.
                        posPixels[pixelIdx]  = new Color(degenPos.x, degenPos.y, degenPos.z, 0f);
                        normPixels[pixelIdx] = new Color(0f, 1f, 0f, 0f);
                        uvPixels[pixelIdx]   = new Color(0f, 0f, 0f, 0f);
                    }
                }
            }

            posAtlas.SetPixels(posPixels);
            normAtlas.SetPixels(normPixels);
            uvAtlas.SetPixels(uvPixels);

            // Apply without mipmaps; keep CPU-readable for post-bake inspection.
            posAtlas.Apply(false, false);
            normAtlas.Apply(false, false);
            uvAtlas.Apply(false, false);

            EnsureDirectory(outputPath);

            SaveAtlasAsset(posAtlas,  outputPath, "PositionAtlas.asset");
            SaveAtlasAsset(normAtlas, outputPath, "NormalAtlas.asset");
            SaveAtlasAsset(uvAtlas,   outputPath, "UVAtlas.asset");
        }

        static Texture2D CreateAtlasTexture(int width, int height, TextureFormat format)
        {
            var tex = new Texture2D(width, height, format, false, true)
            {
                filterMode = FilterMode.Point,
                wrapMode   = TextureWrapMode.Clamp
            };
            return tex;
        }

        static void SaveAtlasAsset(Texture2D tex, string outputPath, string filename)
        {
            string path = outputPath.TrimEnd('/') + "/" + filename;
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(tex, path);
        }

        static void EnsureDirectory(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string folder = Path.GetFileName(path);
            EnsureDirectory(parent);
            AssetDatabase.CreateFolder(parent, folder);
        }
    }
}
