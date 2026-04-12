// VATSampler.hlsl — Custom Function node (ShaderGraph, vertex stage)
//
// Lecture VAT via un seul StructuredBuffer partagé entre toutes les instances.
// Le mesh index de chaque instance est lu via unity_InstanceID — pas de MPB.
//
// Setup ShaderGraph :
//   Custom Function → File → cette include
//   Function : SampleVAT  |  Precision : Float
//   Enable GPU Instancing dans Graph Settings (requis pour unity_InstanceID)
//
// Inputs  : PositionAtlas, NormalAtlas, UVAtlas (Texture2D), VertexIndex (Vector1)
// Outputs : Position (Vector3), Normal (Vector3), AlbedoUV (Vector2)
//
// C# : VATInstanceRenderer.Setup() crée le GraphicsBuffer et appelle
//      vatMaterial.SetBuffer("_MeshIndexBuffer", buffer) — une seule fois.

#ifndef VAT_SAMPLER_INCLUDED
#define VAT_SAMPLER_INCLUDED

// Un seul buffer partagé par toutes les instances du draw call.
// Taille = nombre total d'instances. Indexé par unity_InstanceID.
StructuredBuffer<int> _MeshIndexBuffer;

// ─────────────────────────────────────────────────────────────────────────────

void _LoadVATInternal(
    UnityTexture2D  PositionAtlas,
    UnityTexture2D  NormalAtlas,
    UnityTexture2D  UVAtlas,
    float           VertexIndex,
    out float3      Position,
    out float3      Normal,
    out float2      AlbedoUV)
{
    // unity_InstanceID n'existe que dans le variant instancié (UNITY_INSTANCING_ENABLED).
    // Le guard évite l'erreur de compilation sur le variant non-instancié.
#if defined(UNITY_INSTANCING_ENABLED)
    int meshIndex = _MeshIndexBuffer[unity_InstanceID];
#else
    int meshIndex = 0;
#endif

    int3 texel = int3((int)VertexIndex, meshIndex, 0);

    Position = PositionAtlas.tex.Load(texel).rgb;
    Normal   = NormalAtlas.tex.Load(texel).rgb;
    AlbedoUV = UVAtlas.tex.Load(texel).rg;
}

// ─────────────────────────────────────────────────────────────────────────────

void SampleVAT_float(
    UnityTexture2D  PositionAtlas,
    UnityTexture2D  NormalAtlas,
    UnityTexture2D  UVAtlas,
    float           VertexIndex,
    out float3      Position,
    out float3      Normal,
    out float2      AlbedoUV)
{
    _LoadVATInternal(PositionAtlas, NormalAtlas, UVAtlas,
                     VertexIndex, Position, Normal, AlbedoUV);
}

void SampleVAT_half(
    UnityTexture2D  PositionAtlas,
    UnityTexture2D  NormalAtlas,
    UnityTexture2D  UVAtlas,
    half            VertexIndex,
    out half3       Position,
    out half3       Normal,
    out half2       AlbedoUV)
{
    float3 p; float3 n; float2 uv;
    _LoadVATInternal(PositionAtlas, NormalAtlas, UVAtlas,
                     (float)VertexIndex, p, n, uv);
    Position = (half3)p;
    Normal   = (half3)n;
    AlbedoUV = (half2)uv;
}

#endif // VAT_SAMPLER_INCLUDED
