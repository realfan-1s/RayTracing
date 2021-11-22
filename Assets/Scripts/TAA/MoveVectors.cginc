#ifndef __MOVE__VECTORS__
#define __MOVE__VECTORS__

sampler2D _MainTex;
float4x4 _NonJitterVP;
float4x4 _previousVP;

struct Input
{
    float4 vertex : POSITION;
    float3 normal : NORMAL;
};

struct v2f
{
    float4 pos : SV_POSITION;
    float2 uv : TEXCOORD0;
    float3 ray : TEXCOORD1;
};

v2f VertMotionVectors(Input input) {
    v2f o;
    o.pos = UnityObjectToClipPos(input.vertex);
#ifdef UNITY_HALF_TEXEL_OFFSET
    o.pos.xy += (_ScreenParams.zw - 1.0) * float2(-1, 1) * o.pos.w; // z 是 1.0 + 1.0/宽度，w 为 1.0 + 1.0/高度
#endif
    o.uv = ComputeScreenPos(o.pos);
    o.ray = input.normal;
    return o;
}

inline float2 CalculateMotion(float3 ray) {
    float3 screenPos = ray * (_ProjectionParams.z / ray.z); // 获得远平面距离
    float4 worldPos = mul(unity_CameraToWorld, float4(screenPos, 1.0f));
    // 计算前一帧和当前帧的裁剪平面坐标
    float4 prevClipPos = mul(_previousVP, worldPos);
    float4 currClipPos = mul(_NonJitterVP, worldPos);
    // 计算前一帧和当前帧的屏幕坐标
    float2 screenPrevPos = (prevClipPos.xy / prevClipPos.w + 1.0f) * 0.5f;
    float2 screenCurrPos = (currClipPos.xy / currClipPos.w + 1.0f) * 0.5f;
    return screenCurrPos - screenPrevPos;
}

float4 FragMotionVectors(v2f o) : SV_TARGET {
    return float4(CalculateMotion(o.ray), 0, 1);
}

#endif