// Save this as "GetInstanceColor.hlsl" in your project

    StructuredBuffer<float4> _InstanceColorBuffer;

void GetInstanceColor_float(in float InstanceID,out float4 Color)
{
    #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
    Color = _Colors[unity_InstanceID];
    #else
    Color = _InstanceColorBuffer[InstanceID];
    #endif
}