using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Benchmark spawner: generates a grid of instances to compare rendering modes.
///
/// Each instance spawns four child GameObjects at the same world position, one per mode:
///   Classic    — "Classic" layer, individual MeshRenderer with source mesh
///   VAT        — "VAT" layer, shared VAT base mesh, mesh index passed via buffer
///   Instancing — no MeshRenderer, registered with InstancingInstanceRenderer
///   VFX        — no MeshRenderer, registered with MDIInstanceRenderer (VFX Graph)
///
/// Use Camera Layer Visibility (or Camera.cullingMask) to isolate rendering modes
/// in the Frame Debugger / Profiler.
/// </summary>
public class VATBenchmarkSpawner : MonoBehaviour
{
    [Header("Grid")]
    [Tooltip("Total number of instances to spawn. Grid is always square: side = ceil(sqrt(instanceCount)).")]
    public int instanceCount = 100;

    [Tooltip("Spacing in world units between each cell.")]
    public float spacing = 6f;

    [Tooltip("Random seed for mesh index distribution. 0 = random.")]
    public int randomSeed = 42;

    [Header("Meshes")]
    [Tooltip("Source meshes (one per building type). The index is shared across all rendering modes.")]
    public Mesh[] sourceMeshes;

    [Header("Materials")]
    public Material classicMaterial;

    [Header("VAT")]
    [Tooltip("Layer name for VAT GameObjects.")]
    public string vatLayerName = "VAT";

    [Header("Instancing")]
    [Tooltip("Material with GPU Instancing enabled for Instancing mode.")]
    public Material instancingMaterial;

    // ─────────────────────────────────────────────────────────────────────────
    // Context menus
    // ─────────────────────────────────────────────────────────────────────────

    [ContextMenu("Spawn Benchmark Grid")]
    void SpawnGrid()
    {
        if (!ValidateSetup()) return;

        ClearChildren();

        int classicLayer = LayerMask.NameToLayer("Classic");
        int vatLayer     = LayerMask.NameToLayer(vatLayerName);

        if (classicLayer == -1) Debug.LogWarning("[VAT Benchmark] Layer \"Classic\" not found.");
        if (vatLayer     == -1) Debug.LogWarning($"[VAT Benchmark] Layer \"{vatLayerName}\" not found.");

        var rng = new System.Random(randomSeed == 0 ? System.Environment.TickCount : randomSeed);

        // Square grid: side = ceil(sqrt(instanceCount))
        int cols = Mathf.CeilToInt(Mathf.Sqrt(instanceCount));
        float offset = (cols - 1) * spacing * 0.5f;

        for (int i = 0; i < instanceCount; i++)
        {
            int col = i % cols;
            int row = i / cols;
            var localPos  = new Vector3(col * spacing - offset, 0f, row * spacing - offset);
            int meshIndex = rng.Next(0, sourceMeshes.Length);

            // Common parent
            var instance = new GameObject($"Instance_{i:000}");
            instance.transform.SetParent(transform, false);
            instance.transform.localPosition = localPos;

            // Classic renderer
            var classicGO = new GameObject("Classic");
            classicGO.transform.SetParent(instance.transform, false);
            if (classicLayer != -1) classicGO.layer = classicLayer;
            classicGO.AddComponent<MeshFilter>().sharedMesh = sourceMeshes[meshIndex];
            var mr = classicGO.AddComponent<MeshRenderer>();
            mr.sharedMaterial = classicMaterial;

            // VATRenderer — self-registers in play mode via OnEnable
            // No MeshRenderer — draw call handled by VATInstanceRenderer
            var vatGO = new GameObject("VAT");
            vatGO.transform.SetParent(instance.transform, false);
            if (vatLayer != -1) vatGO.layer = vatLayer;
            var vr = vatGO.AddComponent<VATRenderer>();
            vr.MeshIndex  = meshIndex;
            vr.SpawnIndex = i; // Stable order — independent of OnEnable order

            // InstancingRenderer — self-registers via OnEnable in play mode
            // No MeshRenderer — draw call handled by InstancingInstanceRenderer
            var instGO = new GameObject("Instancing");
            instGO.transform.SetParent(instance.transform, false);
            var ir       = instGO.AddComponent<InstancingRenderer>();
            ir.Mesh       = sourceMeshes[meshIndex];
            ir.SpawnIndex = i;

            // MDIRenderer — self-registers via OnEnable in play mode
            // No MeshRenderer — rendering handled by MDIInstanceRenderer (VFX Graph)
            var mdiGO = new GameObject("MDI");
            mdiGO.transform.SetParent(instance.transform, false);
            var mdr        = mdiGO.AddComponent<VFXInstance>();
            mdr.MeshIndex  = meshIndex;
            mdr.SpawnIndex = i;
        }

        Debug.Log($"[VAT Benchmark] {instanceCount} instances spawned ({cols}×{Mathf.CeilToInt((float)instanceCount / cols)} grid).");

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(gameObject);
#endif
    }

    [ContextMenu("Clear")]
    void ClearChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
                continue;
            }
#endif
            Destroy(transform.GetChild(i).gameObject);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Validation
    // ─────────────────────────────────────────────────────────────────────────

    bool ValidateSetup()
    {
        if (sourceMeshes == null || sourceMeshes.Length == 0)
        {
            Debug.LogError("[VAT Benchmark] sourceMeshes is empty.");
            return false;
        }
        if (classicMaterial == null)
        {
            Debug.LogError("[VAT Benchmark] classicMaterial is not assigned.");
            return false;
        }
        if (instanceCount <= 0)
        {
            Debug.LogError("[VAT Benchmark] instanceCount must be > 0.");
            return false;
        }
        return true;
    }
}
