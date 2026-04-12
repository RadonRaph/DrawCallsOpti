using UnityEngine;

/// <summary>
/// Lightweight component attached to each instance in GPU Instancing mode.
/// Automatically registers / unregisters with InstancingInstanceRenderer.
/// No MeshRenderer — draw calls are handled by InstancingInstanceRenderer.
/// </summary>
public class InstancingRenderer : MonoBehaviour
{
    public Mesh Mesh;
    public int  SpawnIndex = -1;

    void OnEnable()  => InstancingInstanceRenderer.Register(this);
    void OnDisable() => InstancingInstanceRenderer.Unregister(this);
}
