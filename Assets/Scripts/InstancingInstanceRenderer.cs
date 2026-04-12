using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Centralized manager for GPU Instancing mode.
///
/// Groups active InstancingRenderers by source Mesh and issues
/// one Graphics.RenderMeshInstanced call per unique mesh (~6 draw calls for 6 meshes).
/// Unlike VAT, real source meshes are used — no texture atlas.
///
/// Called by BenchmarkHUD (F key) via SetInstancingActive(bool).
/// </summary>
[DefaultExecutionOrder(-100)] // Awake runs before all InstancingRenderer.OnEnable
public class InstancingInstanceRenderer : MonoBehaviour
{
    public static InstancingInstanceRenderer Instance { get; private set; }

    [Header("Resources")]
    public Material instancingMaterial;

    // All known InstancingRenderers — List preserves insertion order.
    readonly List<InstancingRenderer> _allKnown = new();
    readonly List<InstancingRenderer> _active   = new();

    // Groups by mesh: one matrix array per unique mesh.
    sealed class MeshGroup
    {
        public Matrix4x4[] Matrices = System.Array.Empty<Matrix4x4>();
        public int          Count;
    }
    readonly Dictionary<Mesh, MeshGroup> _groups = new();

    bool         _dirty;
    RenderParams _rp;

    // Limit imposed by Graphics.RenderMeshInstanced
    const int MaxPerCall = 1023;

    // ─────────────────────────────────────────────────────────────────────────

    void Awake()
    {
        Instance = this;

        _rp = new RenderParams(instancingMaterial)
        {
            worldBounds       = new Bounds(Vector3.zero, 2000f * Vector3.one),
            shadowCastingMode = ShadowCastingMode.On,
            receiveShadows    = true,
        };
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Static API — called by InstancingRenderer.OnEnable / OnDisable
    // ─────────────────────────────────────────────────────────────────────────

    public static void Register(InstancingRenderer r)
    {
        if (Instance == null) return;
        if (!Instance._allKnown.Contains(r))
            Instance._allKnown.Add(r);
        if (!Instance._active.Contains(r))
            Instance._active.Add(r);
        Instance._dirty = true;
    }

    public static void Unregister(InstancingRenderer r)
    {
        if (Instance == null) return;
        Instance._active.Remove(r);
        Instance._dirty = true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Called by BenchmarkHUD on F key
    // ─────────────────────────────────────────────────────────────────────────

    public void SetInstancingActive(bool active)
    {
        foreach (var r in _allKnown)
            if (r != null) r.enabled = active;
        // InstancingRenderer OnEnable/OnDisable will update _active
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Rendering — one draw call per unique mesh
    // ─────────────────────────────────────────────────────────────────────────

    void LateUpdate()
    {
        if (_dirty) Rebuild();
        if (_active.Count == 0) return;

        foreach (var (mesh, g) in _groups)
        {
            if (g.Count == 0 || mesh == null) continue;

            // Chunking: RenderMeshInstanced is limited to 1023 instances per call
            for (int offset = 0; offset < g.Count; offset += MaxPerCall)
            {
                int count = Mathf.Min(MaxPerCall, g.Count - offset);
                Graphics.RenderMeshInstanced(_rp, mesh, 0, g.Matrices, count, offset);
            }
        }
    }

    void Rebuild()
    {
        // Stable sort by SpawnIndex — ensures consistency with Classic mode
        _active.Sort((a, b) => a.SpawnIndex.CompareTo(b.SpawnIndex));

        // Reset counters
        foreach (var g in _groups.Values)
            g.Count = 0;

        // Group by mesh
        foreach (var r in _active)
        {
            if (r == null || r.Mesh == null) continue;

            if (!_groups.TryGetValue(r.Mesh, out var group))
            {
                group = new MeshGroup();
                _groups[r.Mesh] = group;
            }

            // Grow the array if needed
            if (group.Count >= group.Matrices.Length)
            {
                int newSize = Mathf.Max(group.Count + 1, group.Matrices.Length * 2, 16);
                System.Array.Resize(ref group.Matrices, newSize);
            }

            group.Matrices[group.Count++] = r.transform.localToWorldMatrix;
        }

        _dirty = false;
    }
}
