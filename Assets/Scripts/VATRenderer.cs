using UnityEngine;

/// <summary>
/// Lightweight component attached to each VAT instance.
/// Automatically registers / unregisters with VATInstanceRenderer.
/// No MeshRenderer, no MaterialPropertyBlock — draw calls are handled centrally.
/// </summary>
public class VATRenderer : MonoBehaviour
{
    public int MeshIndex;
    // Spawn index — determines stable ordering in the draw call.
    // Assigned by VATBenchmarkSpawner, independent of OnEnable order.
    public int SpawnIndex = -1;

    void OnEnable()  => VATInstanceRenderer.Register(this);
    void OnDisable() => VATInstanceRenderer.Unregister(this);
}
