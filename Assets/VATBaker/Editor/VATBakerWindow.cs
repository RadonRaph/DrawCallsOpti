using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using System.IO;

namespace VATBaker
{
    public class VATBakerWindow : EditorWindow
    {
        VATBakerSettings    _settings;
        SerializedObject    _so;
        ReorderableList     _meshList;
        Vector2             _scroll;

        [MenuItem("Tools/VAT Baker")]
        static void Open() => GetWindow<VATBakerWindow>("VAT Baker");

        void OnEnable() => LoadOrCreateSettings();

        // ─────────────────────────────────────────────────────────────────────
        // Settings persistence
        // ─────────────────────────────────────────────────────────────────────

        void LoadOrCreateSettings()
        {
            string[] guids = AssetDatabase.FindAssets("t:VATBakerSettings");
            if (guids.Length > 0)
            {
                _settings = AssetDatabase.LoadAssetAtPath<VATBakerSettings>(
                    AssetDatabase.GUIDToAssetPath(guids[0]));
            }
            else
            {
                _settings = CreateInstance<VATBakerSettings>();
                CreateFolderRecursive("Assets/VATBaker");
                AssetDatabase.CreateAsset(_settings, "Assets/VATBaker/VATBakerSettings.asset");
                AssetDatabase.SaveAssets();
            }

            _so = new SerializedObject(_settings);

            var meshesProp = _so.FindProperty("sourceMeshes");
            _meshList = new ReorderableList(_so, meshesProp, true, true, true, true);
            _meshList.drawHeaderCallback  = r => EditorGUI.LabelField(r, "Source Meshes");
            _meshList.drawElementCallback = (rect, index, active, focused) =>
            {
                var elem = meshesProp.GetArrayElementAtIndex(index);
                EditorGUI.PropertyField(
                    new Rect(rect.x, rect.y + 2, rect.width, EditorGUIUtility.singleLineHeight),
                    elem, GUIContent.none);
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // GUI
        // ─────────────────────────────────────────────────────────────────────

        void OnGUI()
        {
            if (_settings == null || _so == null) { LoadOrCreateSettings(); return; }

            _so.Update();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            GUILayout.Space(8);
            EditorGUILayout.LabelField("VAT Baker", EditorStyles.boldLabel);
            GUILayout.Space(4);

            _meshList.DoLayoutList();

            GUILayout.Space(8);
            EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_so.FindProperty("outputPath"),           new GUIContent("Output Path"));
            EditorGUILayout.PropertyField(_so.FindProperty("flattenSubmeshes"),     new GUIContent("Flatten Submeshes"));
            EditorGUILayout.PropertyField(_so.FindProperty("useFullFloatPrecision"),new GUIContent("Full Float Precision (32-bit)"));

            _so.ApplyModifiedProperties();

            GUILayout.Space(8);
            DrawStats();

            GUILayout.Space(8);
            using (new EditorGUI.DisabledScope(!CanBake()))
            {
                if (GUILayout.Button("Bake", GUILayout.Height(36)))
                    DoBake();
            }

            EditorGUILayout.EndScrollView();
        }

        void DrawStats()
        {
            if (_settings.sourceMeshes == null) return;

            int maxFaces   = 0;
            int validCount = 0;
            foreach (var m in _settings.sourceMeshes)
            {
                if (m == null) continue;
                validCount++;
                int faces = m.triangles.Length / 3;
                if (faces > maxFaces) maxFaces = faces;
            }

            if (validCount == 0) return;

            int totalVerts = maxFaces * 3;
            string fmt = _settings.useFullFloatPrecision ? "RGBAFloat (32-bit)" : "RGBAHalf (16-bit)";
            EditorGUILayout.HelpBox(
                $"Meshes: {validCount}   |   Max faces: {maxFaces}   |   Base mesh verts: {totalVerts}\n" +
                $"Atlas: {totalVerts} × {validCount} px   |   Format: {fmt}",
                MessageType.Info);
        }

        bool CanBake()
        {
            if (_settings.sourceMeshes == null) return false;
            foreach (var m in _settings.sourceMeshes)
                if (m != null) return true;
            return false;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Bake pipeline
        // ─────────────────────────────────────────────────────────────────────

        void DoBake()
        {
            try
            {
                EditorUtility.DisplayProgressBar("VAT Baker", "Expanding meshes…", 0.1f);

                var expanded = MeshExpander.ExpandAll(
                    _settings.sourceMeshes,
                    _settings.flattenSubmeshes,
                    out int maxExpandedVerts);

                if (expanded.Count == 0)
                {
                    EditorUtility.ClearProgressBar();
                    EditorUtility.DisplayDialog("VAT Baker", "No valid meshes found.", "OK");
                    return;
                }

                string outPath = _settings.outputPath.TrimEnd('/');

                // ── Step 1: Base mesh ────────────────────────────────────────
                EditorUtility.DisplayProgressBar("VAT Baker", "Building base mesh…", 0.3f);

                Mesh baseMesh = BaseMeshBuilder.Build(maxExpandedVerts);
                CreateFolderRecursive(outPath);

                string baseMeshPath = outPath + "/BaseMesh.asset";
                AssetDatabase.DeleteAsset(baseMeshPath);
                AssetDatabase.CreateAsset(baseMesh, baseMeshPath);

                // ── Step 2: Atlas textures ───────────────────────────────────
                EditorUtility.DisplayProgressBar("VAT Baker", "Baking atlas textures…", 0.55f);

                TextureBaker.BakeAtlases(
                    expanded,
                    maxExpandedVerts,
                    outPath,
                    _settings.useFullFloatPrecision,
                    out _, out _, out _);

                // ── Finalise ─────────────────────────────────────────────────
                EditorUtility.DisplayProgressBar("VAT Baker", "Saving assets…", 0.9f);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorUtility.ClearProgressBar();

                int maxFaces = maxExpandedVerts / 3;
                Debug.Log($"[VAT Baker] Bake complete. Meshes: {expanded.Count} | " +
                          $"Max faces: {maxFaces} | Atlas: {maxExpandedVerts}×{expanded.Count} | " +
                          $"Output: {outPath}");

                EditorUtility.DisplayDialog("VAT Baker — Done",
                    $"Meshes baked: {expanded.Count}\n" +
                    $"Max faces: {maxFaces}\n" +
                    $"Base mesh verts: {maxExpandedVerts}\n" +
                    $"Atlas: {maxExpandedVerts} × {expanded.Count} px\n\n" +
                    $"Assets saved to:\n{outPath}",
                    "OK");

                // Highlight output folder in Project window
                var folder = AssetDatabase.LoadAssetAtPath<Object>(outPath);
                if (folder != null)
                {
                    EditorGUIUtility.PingObject(folder);
                    Selection.activeObject = folder;
                }
            }
            catch (System.Exception e)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"[VAT Baker] Bake failed: {e}");
                EditorUtility.DisplayDialog("VAT Baker — Error", e.Message, "OK");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        static void CreateFolderRecursive(string path)
        {
            path = path.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string folder = Path.GetFileName(path);
            CreateFolderRecursive(parent);
            AssetDatabase.CreateFolder(parent, folder);
        }
    }
}
