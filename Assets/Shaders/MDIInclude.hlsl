#ifndef MDI_TRANSFORM_INCLUDED
#define MDI_TRANSFORM_INCLUDED

StructuredBuffer<float4x4> _SortedOTW;
StructuredBuffer<float4x4> _SortedWTO;

// InstanceIDf : float received from ShaderGraph (the Instance ID node outputs unity_InstanceID).
// Transforms position and normal from object space to world space
// using the matrix of the corresponding instance in _SortedOTW/_SortedWTO.
//
// Position  : OTW * float4(posOS, 1)  — translation + rotation + scale
// Normal    : normalOS * WTO           — implicit inverse-transpose (mul right by WTO)
//
// Requires "Enable GPU Instancing" ON in the ShaderGraph and material.enableInstancing = true.
// DrawMeshInstancedIndirect sets unity_BaseInstanceID = startInstanceLocation per call.
// Unity then sets unity_InstanceID = SV_InstanceID + unity_BaseInstanceID
//                                  = localID        + _MeshOffsets[meshType]
//                                  = global sorted index into _SortedOTW / _SortedWTO.
void MDITransformBis_float(float  InstanceIDf,
                        float3 positionOS,
                        float3 normalOS,
                        out float3 positionWS,
                        out float3 normalWS)
{
    int i = (int)InstanceIDf; // = unity_InstanceID = global sorted index
    float4x4 otw = _SortedOTW[i];
    float4x4 wto = _SortedWTO[i];

    positionWS = mul(otw, float4(positionOS, 1.0)).xyz;
    normalWS   = normalize(mul(normalOS, (float3x3)wto));
}

#endif