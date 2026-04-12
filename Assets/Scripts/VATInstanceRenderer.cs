using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Centralized manager — ONE draw call for all VAT instances.
///
/// VATRenderers self-register/unregister via OnEnable/OnDisable.
/// _allKnown keeps the full list even when renderers are disabled,
/// so SetVATActive() can re-enable them without losing references.
///
/// Called by BenchmarkHUD (F key) via SetVATActive(bool).
/// </summary>
[DefaultExecutionOrder(-100)] // Awake runs before all VATRenderer.OnEnable
public class VATInstanceRenderer : MonoBehaviour
{
    public static VATInstanceRenderer Instance { get; private set; }

    [Header("Resources")]
    public Mesh     vatBaseMesh;
    public Material vatMaterial;

    static readonly int PropBuffer = Shader.PropertyToID("_MeshIndexBuffer");

    // All known VATRenderers — List preserves insertion order.
    // Persists through enable/disable cycles, never cleared.
    readonly List<VATRenderer> _allKnown = new();
    // Currently active (enabled) subset — rebuilt in _allKnown order.
    readonly List<VATRenderer> _active   = new();

    GraphicsBuffer _buffer;
    Matrix4x4[]   _matrices = System.Array.Empty<Matrix4x4>();
    bool          _dirty;
    RenderParams  _rp;

    // ─────────────────────────────────────────────────────────────────────────

    void Awake()
    {
        Instance = this;

        int vatLayer = LayerMask.NameToLayer("VAT");
        _rp = new RenderParams(vatMaterial)
        {
            worldBounds       = new Bounds(Vector3.zero, 2000f * Vector3.one),
            shadowCastingMode = ShadowCastingMode.On,
            receiveShadows    = true,
            layer             = vatLayer >= 0 ? vatLayer : 0,
        };
    }

    void OnDestroy()
    {
        _buffer?.Release();
        _buffer = null;
        if (Instance == this) Instance = null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Static API — called by VATRenderer.OnEnable / OnDisable
    // ─────────────────────────────────────────────────────────────────────────

    public static void Register(VATRenderer r)
    {
        if (Instance == null) return;
        if (!Instance._allKnown.Contains(r))
            Instance._allKnown.Add(r);
        if (!Instance._active.Contains(r))
            Instance._active.Add(r);
        Instance._dirty = true;
    }

    public static void Unregister(VATRenderer r)
    {
        if (Instance == null) return;
        Instance._active.Remove(r);
        // _allKnown retains r so it can be re-enabled later
        Instance._dirty = true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Called by BenchmarkHUD on F key
    // ─────────────────────────────────────────────────────────────────────────

    public void SetVATActive(bool vat)
    {
        foreach (var r in _allKnown)
            if (r != null) r.enabled = vat;
        // VATRenderer OnEnable/OnDisable will update _active automatically
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Rendering — ONE draw call per frame
    // ─────────────────────────────────────────────────────────────────────────

    void LateUpdate()
    {
        if (_dirty) Rebuild();
        if (_matrices.Length == 0) return;

        Graphics.RenderMeshInstanced(_rp, vatBaseMesh, 0, _matrices, _matrices.Length);
    }

    void Rebuild()
    {
        // Stable sort by SpawnIndex: ensures matrices[i] and indices[i]
        // always correspond to the same spawn, regardless of OnEnable order.
        _active.Sort((a, b) => a.SpawnIndex.CompareTo(b.SpawnIndex));

        int count = _active.Count;
        _matrices = new Matrix4x4[count];
        var indices = new int[count];

        for (int i = 0; i < count; i++)
        {
            _matrices[i] = _active[i].transform.localToWorldMatrix;
            indices[i]   = _active[i].MeshIndex;
        }

        _buffer?.Release();
        if (count > 0)
        {
            _buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured,
                                         count, sizeof(int));
            _buffer.SetData(indices);
            vatMaterial.SetBuffer(PropBuffer, _buffer);
        }

        _dirty = false;
    }
}
