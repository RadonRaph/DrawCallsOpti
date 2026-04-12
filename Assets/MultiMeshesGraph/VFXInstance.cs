using UnityEngine;

/// <summary>
/// Lightweight component attached to each VFX Graph instance.
/// Automatically registers / unregisters with MDIInstanceRenderer.
/// No MeshRenderer — rendering is handled by the single VisualEffect managed by MDIInstanceRenderer.
/// </summary>
public class VFXInstance : MonoBehaviour
{
    public int MeshIndex;
    public int SpawnIndex = -1;

    void OnEnable()  => VFXGraphRenderer.Register(this);
    void OnDisable() => VFXGraphRenderer.Unregister(this);
}
