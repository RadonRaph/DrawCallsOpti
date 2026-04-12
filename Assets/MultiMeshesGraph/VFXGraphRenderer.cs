using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// Per-instance data struct pushed to the VFX Graph GraphicsBuffer.
///
/// [VFXType(VFXTypeOptions.GraphicsBuffer)] makes this struct visible inside the
/// VFX Graph asset as a typed buffer element. In the VFX blackboard, expose a
/// property of type "StructuredBuffer&lt;VFXInstanceData&gt;" named "InstanceBuffer".
/// A "Get Buffer Element" operator then gives per-particle access to each field.
///
/// Field layout (stride = 40 bytes, no padding needed):
///   position   float3  offset  0
///   angles     float3  offset 12  (euler, degrees)
///   scale      float3  offset 24
///   meshIndex  int     offset 36
/// </summary>
[VFXType(VFXTypeAttribute.Usage.GraphicsBuffer)]
public struct VFXInstanceData
{
    public Vector3 position;
    public Vector3 angles;
    public Vector3 scale;
    public int     meshIndex;
}

/// <summary>
/// VFX Graph-based replacement for the old Multi-Draw Indirect renderer.
///
/// Maintains a single VisualEffect instance for all registered MDIRenderer components.
/// When the instance list changes, rebuilds a structured GraphicsBuffer of VFXInstanceData
/// and pushes it to the VFX Graph via two exposed properties:
///   - "InstanceBuffer"  (StructuredBuffer&lt;VFXInstanceData&gt;) : one element per active instance
///   - "InstanceCount"   (int)                                   : number of active instances
///
/// The VFX Graph is then reinitialized so it spawns exactly InstanceCount particles,
/// each reading its transform and mesh index from InstanceBuffer[particleIndex].
///
/// Required VFX Graph setup (done in the VFX asset, not here):
///   Spawner    : Single Burst, Count driven by the "InstanceCount" int parameter
///   Initialize : "Get Buffer Element" on "InstanceBuffer" at index particleId,
///                then wire position / angles / scale / meshIndex to Set Attribute blocks
///   Lifetime   : infinite (no kill block)
/// </summary>
[DefaultExecutionOrder(-100)]
public class VFXGraphRenderer : MonoBehaviour
{
    public static VFXGraphRenderer Instance { get; private set; }

    [Header("VFX Graph")]
    public VisualEffectAsset vfxAsset;

    [Header("Meshes (passed to VFX exposed Mesh parameters)")]
    public Mesh[] sourceMeshes; // index matches MDIRenderer.MeshIndex

    public Texture2D mainTex;

    // stride derived from the public struct so both sides stay in sync
    static readonly int k_Stride = Marshal.SizeOf<VFXInstanceData>(); // 40 bytes

    // -------------------------------------------------------------------------

    readonly List<VFXInstance> _allKnown = new();
    readonly List<VFXInstance> _active   = new();

    VisualEffect      _vfx;
    GraphicsBuffer    _instanceBuffer;
    VFXInstanceData[] _instanceData;

    bool _dirty;
    bool _vfxActive;

    static readonly int PropInstanceBuffer = Shader.PropertyToID("InstanceBuffer");
    static readonly int PropInstanceCount  = Shader.PropertyToID("InstanceCount");
    static readonly int PropMainTex  = Shader.PropertyToID("MainTex");

    // Mesh property IDs for MeshA … MeshF
    static readonly int[] PropMeshes =
    {
        Shader.PropertyToID("MeshA"),
        Shader.PropertyToID("MeshB"),
        Shader.PropertyToID("MeshC"),
        Shader.PropertyToID("MeshD"),
        Shader.PropertyToID("MeshE"),
        Shader.PropertyToID("MeshF"),
    };

    // -------------------------------------------------------------------------

    void Awake()
    {
        Instance = this;

        // Create the single VFX GameObject
        var go = new GameObject("MDI_VFXGraph");
        go.transform.SetParent(transform, false);
        _vfx = go.AddComponent<VisualEffect>();

        if (vfxAsset != null)
            _vfx.visualEffectAsset = vfxAsset;

        _vfx.Stop();
        _vfxActive = false;
    }

    void OnDestroy()
    {
        _instanceBuffer?.Release();
        _instanceBuffer = null;
        if (Instance == this) Instance = null;
    }

    // -------------------------------------------------------------------------
    // Static API called by MDIRenderer.OnEnable / OnDisable
    // -------------------------------------------------------------------------

    public static void Register(VFXInstance r)
    {
        if (Instance == null) return;
        if (!Instance._allKnown.Contains(r)) Instance._allKnown.Add(r);
        if (!Instance._active.Contains(r))   Instance._active.Add(r);
        Instance._dirty = true;
    }

    public static void Unregister(VFXInstance r)
    {
        if (Instance == null) return;
        Instance._active.Remove(r);
        Instance._dirty = true;
    }

    // -------------------------------------------------------------------------
    // Called by BenchmarkHUD (F key) to toggle this rendering mode
    // -------------------------------------------------------------------------

    public void SetMDIActive(bool active)
    {
        _vfxActive = active;

        foreach (var r in _allKnown)
            if (r != null) r.enabled = active;

        if (active)
        {
            // Force a full rebuild so the VFX is in sync when re-enabled
            _dirty = true;
        }
        else
        {
            if (_vfx != null) _vfx.Stop();
        }
    }

    // -------------------------------------------------------------------------

    void LateUpdate()
    {
        if (!_vfxActive) return;
        if (_dirty) RebuildBuffer();
    }

    void RebuildBuffer()
    {
        _dirty = false;

        // Stable sort matching the other renderers
        _active.Sort((a, b) => a.SpawnIndex.CompareTo(b.SpawnIndex));

        int N = _active.Count;

        // Reallocate buffer only when size changes
        if (_instanceBuffer == null || _instanceBuffer.count != Mathf.Max(N, 1))
        {
            _instanceBuffer?.Release();
            _instanceBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                Mathf.Max(N, 1),
                k_Stride);
        }

        if (N > 0)
        {
            // Grow CPU array on demand
            if (_instanceData == null || _instanceData.Length < N)
                _instanceData = new VFXInstanceData[N];

            for (int i = 0; i < N; i++)
            {
                var t = _active[i].transform;
                _instanceData[i] = new VFXInstanceData
                {
                    position  = t.position,
                    angles    = t.eulerAngles,
                    scale     = t.lossyScale,
                    meshIndex = _active[i].MeshIndex,
                };
            }

            _instanceBuffer.SetData(_instanceData, 0, 0, N);
        }

        // Push buffer and count to VFX, then respawn all particles
        _vfx.SetGraphicsBuffer(PropInstanceBuffer, _instanceBuffer);
        _vfx.SetInt(PropInstanceCount, N);
        _vfx.SetTexture(PropMainTex, mainTex);

        // Bind source meshes to exposed Mesh parameters
        if (sourceMeshes != null)
        {
            for (int m = 0; m < sourceMeshes.Length && m < PropMeshes.Length; m++)
            {
                if (sourceMeshes[m] != null)
                    _vfx.SetMesh(PropMeshes[m], sourceMeshes[m]);
            }
        }

        // Reinitialize the VFX so it spawns exactly N particles from the new buffer
        _vfx.Reinit();
    }
}