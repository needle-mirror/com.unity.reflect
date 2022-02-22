#ifndef UNITY_SYNC_STANDARD_CORE_INCLUDED
#define UNITY_SYNC_STANDARD_CORE_INCLUDED

#include "UnityCG.cginc"

float2 RotateUV(half2 tex, half4 ST, half rad)
{
    half c = cos(rad);
    half s = sin(rad);
    return float2(tex.x * c - tex.y * s, tex.x * s + tex.y * c);
}

fixed4 GetColor(sampler2D tex, half2 uv, half brightness, bool invert)
{
    fixed4 c = tex2D(tex, uv) * brightness;
    return lerp(c, fixed4(1.0 - c.r, 1.0 - c.g, 1.0 - c.b, c.a), invert);
}

half GetGrey(sampler2D tex, half2 uv, half brightness, bool invert)
{
    half c = tex2D(tex, uv).r * brightness;
    return lerp(c, 1.0 - c, invert);
}

#define ROTATE_UV(tex, name) tex = RotateUV(tex, name##_ST, name##_R * 0.0174533f);

#define DECLARE_MAP(name) sampler2D name##; half4 name##_ST; half name##_I; half name##_B
#define DECLARE_MAP_ROT(name) half name##_R

struct Input
{
    float2 texcoord;
    float4 screenPos;
};

void vert(inout appdata_full v, out Input o)
{
    UNITY_INITIALIZE_OUTPUT(Input, o);
    o.texcoord = v.texcoord.xy;
}

// Tint
fixed4 _Reflect_Tint;

// Albedo
fixed4 _Reflect_AlbedoColor;
bool _Reflect_UseAlbedoMap;

half _Reflect_MainTex_Fade;
DECLARE_MAP(_Reflect_MainTex);
#if _REFLECT_MAP_ROTATION
DECLARE_MAP_ROT(_Reflect_MainTex);
#endif


// Normal
bool _Reflect_UseNormalMap;

half _Reflect_NormalScale;
DECLARE_MAP(_Reflect_NormalMap);
#if _REFLECT_MAP_ROTATION
DECLARE_MAP_ROT(_Reflect_NormalMap);
#endif

// Smoothness
half _Reflect_Smoothness;

bool _Reflect_UseSmoothnessMap;

DECLARE_MAP(_Reflect_SmoothnessMap);
#if _REFLECT_MAP_ROTATION
DECLARE_MAP_ROT(_Reflect_SmoothnessMap);
#endif

// Metallic
half _Reflect_Metallic;

bool _Reflect_UseMetallicMap;

DECLARE_MAP(_Reflect_MetallicMap);
#if _REFLECT_MAP_ROTATION
DECLARE_MAP_ROT(_Reflect_MetallicMap);
#endif

// Emission
fixed4 _Reflect_Emission;

bool _Reflect_UseEmissionMap;
DECLARE_MAP(_Reflect_EmissionMap);
#if _REFLECT_MAP_ROTATION
DECLARE_MAP_ROT(_Reflect_EmissionMap);
#endif

// Cutout
bool _Reflect_UseCutout;
half _Reflect_CutoutThreshold;

DECLARE_MAP(_Reflect_CutoutMap);
#if _REFLECT_MAP_ROTATION
DECLARE_MAP_ROT(_Reflect_CutoutMap);
#endif

void ComputeOpaqueShader(float2 texcoord, inout SurfaceOutputStandard o)
{
    if (_Reflect_UseCutout)
    {
        float2 cutoutUV = texcoord;

        #if _REFLECT_MAP_ROTATION
        ROTATE_UV(cutoutUV, _Reflect_CutoutMap);
        #endif

        half cutout = GetGrey(_Reflect_CutoutMap, TRANSFORM_TEX(cutoutUV, _Reflect_CutoutMap), _Reflect_CutoutMap_B, _Reflect_CutoutMap_I);

        clip(cutout - _Reflect_CutoutThreshold);
    }

    // Albedo color
    o.Albedo = _Reflect_AlbedoColor;

    // Albedo
    if (_Reflect_UseAlbedoMap)
    {
        float2 albedoUV = texcoord;

        #if _REFLECT_MAP_ROTATION
        ROTATE_UV(albedoUV, _Reflect_MainTex);
        #endif

        fixed4 c = GetColor(_Reflect_MainTex, TRANSFORM_TEX(albedoUV, _Reflect_MainTex), _Reflect_MainTex_B, _Reflect_MainTex_I);

        o.Albedo = lerp(o.Albedo, c, _Reflect_MainTex_Fade);
    }

    // Tint
    o.Albedo *= _Reflect_Tint;

    // Normal
    if (_Reflect_UseNormalMap)
    {
        float2 normalUV = texcoord;

        #if _REFLECT_MAP_ROTATION
        ROTATE_UV(normalUV, _Reflect_NormalMap);
        #endif

        half4 n = tex2D(_Reflect_NormalMap, TRANSFORM_TEX(normalUV, _Reflect_NormalMap));
        half scale = (1.0f + _Reflect_NormalMap_I * -2.0f) * _Reflect_NormalScale * _Reflect_NormalMap_B;

        o.Normal = UnpackScaleNormal(n, scale);
    }

    // Smoothness
    o.Smoothness = _Reflect_Smoothness;

    if (_Reflect_UseSmoothnessMap)
    {
        float2 smoothnessUV = texcoord;

        #if _REFLECT_MAP_ROTATION
        ROTATE_UV(smoothnessUV, _Reflect_SmoothnessMap);
        #endif

        half s = GetGrey(_Reflect_SmoothnessMap, TRANSFORM_TEX(smoothnessUV, _Reflect_SmoothnessMap), _Reflect_SmoothnessMap_B, _Reflect_SmoothnessMap_I);

        o.Smoothness *= s;
    }

    // Metallic
    o.Metallic = _Reflect_Metallic;

    if (_Reflect_UseMetallicMap)
    {
        float2 metallicUV = texcoord;

        #if _REFLECT_MAP_ROTATION
        ROTATE_UV(metallicUV, _Reflect_MetallicMap);
        #endif

        half m = GetGrey(_Reflect_MetallicMap, TRANSFORM_TEX(metallicUV, _Reflect_MetallicMap), _Reflect_MetallicMap_B, _Reflect_MetallicMap_I);

        o.Metallic *= m;
    }

    // Emission
    o.Emission = _Reflect_Emission;

    if (_Reflect_UseEmissionMap)
    {
        float2 emissionUV = texcoord;

        #if _REFLECT_MAP_ROTATION
        ROTATE_UV(emissionUV, _Reflect_EmissionMap);
        #endif

        half4 e = GetColor(_Reflect_EmissionMap, TRANSFORM_TEX(emissionUV, _Reflect_EmissionMap), _Reflect_EmissionMap_B, _Reflect_EmissionMap_I);

        o.Emission = e;
    }
}

#endif // UNITY_SYNC_STANDARD_CORE_INCLUDED
