#ifndef __TAA__
#define __TAA__

#include "UnityCG.cginc"

struct Input{
    float4 vertex : POSITION;
    float2 uv : TEXCOORD;
};

struct v2f{
    float4 pos : SV_POSITION;
    float4 uv : TEXCOORD0; // xy : MainTex.uv, zw: historyBuffer.uv
};

struct Output {
    float4 first : SV_TARGET0;
    float4 second : SV_TARGET1;
};

sampler2D _MainTex;
sampler2D _HistoryTex;

float4 _MainTex_TexelSize;
float4 _HistoryTex_TexelSize;
float2 _Jitter;
uniform sampler2D _CameraMotionVectorsTex;
uniform float _Stationary;
uniform float _Move;
uniform float _SharpAmount;
uniform float _MotionAmplification;

#if UNITY_REVERSE_Z
    #define TAA_STEP(a, b) step(b, a)
#else
    #define TAA_STEP(a, b) step(a, b)
#endif

v2f vert(Input input) {
    v2f o;
    o.pos = UnityObjectToClipPos(input.vertex);
    o.uv = input.uv.xyxy;
#if UNITY_UV_STATS_AT_TOP
    if (_MainTex_TexelSize.y < 0)
        o.uv.y = 1.0 - input.uv.y;
#endif
    return o;
}

inline float max(float3 num) {
    return max(num.x, max(num.y, num.z));
}

inline float rcp(float value) {
    return 1.0 / value;
}

inline float3 ClipToAABB(float3 color, float3 minColor, float3 maxColor) {
    float3 center = 0.5 * (maxColor + minColor);
    float3 extent = 0.5 * (maxColor - minColor);
    float3 offset = color - center;
    float3 stride = abs(offset.xyz / extent.xyz);
    stride.x = max(stride.x, max(stride.y, stride.z));
    if (stride.x > 1.0) {
        return center + offset / stride.x;
    }
    return color;
}

// 色调映射算法必须是可逆的
inline float3 ToneMap(float3 color) {
    return color.rgb * rcp(max(color.rgb) + 1.0);
}

inline float3 ToneMap(float3 color, float weight) {
    return color.rgb * weight * rcp(max(color.rgb) + 1.0);
}

inline float3 ToneMapInvert(float3 color) {
    return color.rgb * rcp(1.0 - max(color.rgb));
}

inline float2 GetNeighborPixel(float2 uv) {
#if UNITY_UV_STARTS_AT_TOP
    const float2 depth = _MainTex_TexelSize.y < 0 ? _MainTex_TexelSize.xy * float2(1, -1) : _MainTex_TexelSize.xy;
#else
    const float2 depth = _MainTex_TexelSize.xy;
#endif
    float4 neightborSampler = float4(
        tex2D(_MainTex, uv + float2(-depth.x, depth.y)).a, 
        tex2D(_MainTex, uv + depth).a, 
        tex2D(_MainTex, uv + float2(depth.x, -depth.y)).a, 
        tex2D(_MainTex, uv - depth).a);

    float3 center = float3(0.0, 0.0, tex2D(_MainTex, uv).a);
    center = lerp(center, float3(-1, 1, neightborSampler.x), TAA_STEP(neightborSampler.x, center.z));
    center = lerp(center, float3(1, 1, neightborSampler.y), TAA_STEP(neightborSampler.y, center.z));
    center = lerp(center, float3(1, -1, neightborSampler.z), TAA_STEP(neightborSampler.z, center.z));
    center = lerp(center, float3(-1, -1, neightborSampler.w), TAA_STEP(neightborSampler.w, center.z));
    neightborSampler = float4(
        tex2D(_MainTex, uv + float2(0, depth.y)).a,
        tex2D(_MainTex, uv + float2(0, -depth.y)).a,
        tex2D(_MainTex, uv + float2(depth.x, 0)).a,
        tex2D(_MainTex, uv + float2(-depth.x, 0)).a);
    center = lerp(center, float3(0, 1, neightborSampler.x), TAA_STEP(neightborSampler.x, center.z));
    center = lerp(center, float3(0, -1, neightborSampler.x), TAA_STEP(neightborSampler.x, center.z));
    center = lerp(center, float3(1, 0, neightborSampler.x), TAA_STEP(neightborSampler.x, center.z));
    center = lerp(center, float3(-1, 0, neightborSampler.x), TAA_STEP(neightborSampler.x, center.z));
    return uv + center.xy * depth;
}

float4 frag(v2f o) : SV_TARGET{
    float2 uv = o.uv.xy;
    const float2 k = _MainTex_TexelSize.xy;
    // 先计算运动矢量
    float2 motion = tex2D(_CameraMotionVectorsTex, GetNeighborPixel(o.uv.zw)).xy;
    float alpha = length(motion * _MotionAmplification);
#if UNITY_UV_STARTS_AT_TOP
    uv -= k.y < 0 ? _Jitter * float2(1, -1) : _Jitter;
#else
    uv -= _Jitter;
#endif
    float4 col = tex2D(_MainTex, uv);
    float3 currCol = col.rgb;
    // 色彩锐化
    float4x4 top = float4x4(
        tex2D(_MainTex, uv - k), // TopLeft
        tex2D(_MainTex, uv + float2(0, -k.y)), // TopMiddle
        tex2D(_MainTex, uv + float2(k.x, -k.y)), // TopRight
        tex2D(_MainTex, uv + float2(-k.x, 0))); // MiddleLeft
    float4x4 bottom = float4x4(
        tex2D(_MainTex, uv + k), // BottomRight
        tex2D(_MainTex, uv + float2(0, k.y)), // BottomMiddle
        tex2D(_MainTex, uv + float2(-k.x, k.y)), // BottomLeft
        tex2D(_MainTex, uv + float2(k.x, 0))); // middleRight
    float3 corner = (top[0] + top[2] + bottom[0] + bottom[2]).rgb * 0.25f;
    currCol += (currCol - corner) * _SharpAmount;
    currCol = max(0, currCol);

    // ToneMap
    currCol = ToneMap(currCol);
    top[0] = float4(ToneMap(top[0].rgb), 1.0f);
    top[1] = float4(ToneMap(top[1].rgb), 1.0f);
    top[2] = float4(ToneMap(top[2].rgb), 1.0f);
    top[3] = float4(ToneMap(top[3].rgb), 1.0f);
    bottom[0] = float4(ToneMap(bottom[0].rgb), 1.0f);
    bottom[1] = float4(ToneMap(bottom[1].rgb), 1.0f);
    bottom[2] = float4(ToneMap(bottom[2].rgb), 1.0f);
    bottom[3] = float4(ToneMap(bottom[3].rgb), 1.0f);

    col = tex2D(_HistoryTex, o.uv.zw - motion);
    float3 prevCol = ToneMap(col.rgb);
    float3 minColor = min(currCol, min(top[0], min(top[1], min(top[2], min(top[3], min(bottom[0], min(bottom[1], min(bottom[2], bottom[3]))))))).rgb);
    float3 maxColor = max(currCol, max(top[0], max(top[1], max(top[2], max(top[3], max(bottom[0], max(bottom[1], max(bottom[2], bottom[3]))))))).rgb);
    prevCol = clamp(prevCol, minColor, maxColor);

    float2 luma = float2(Luminance(currCol), Luminance(prevCol));
    float weight = 1.0 - abs(luma.x - luma.y) / max(luma.x, max(0.2, luma.y));
    weight = lerp(_Move, _Stationary, weight * weight);
    return float4(ToneMapInvert(lerp(currCol, prevCol, weight)), alpha);
}

#endif