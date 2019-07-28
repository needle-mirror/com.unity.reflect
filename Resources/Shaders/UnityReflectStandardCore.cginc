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
fixed4 _Tint;

// Albedo
fixed4 _AlbedoColor;
bool _UseAlbedoMap;

half _MainTex_Fade;
DECLARE_MAP(_MainTex);
#if _MAP_ROTATION
DECLARE_MAP_ROT(_MainTex);
#endif


// Normal
bool _UseNormalMap;

half _BumpScale;
DECLARE_MAP(_BumpMap);
#if _MAP_ROTATION
DECLARE_MAP_ROT(_BumpMap);
#endif

// Smoothness
half _Smoothness;

bool _UseSmoothnessMap;

DECLARE_MAP(_SmoothnessMap);
#if _MAP_ROTATION
DECLARE_MAP_ROT(_SmoothnessMap);
#endif

// Metallic
half _Metallic;

bool _UseMetallicMap;

DECLARE_MAP(_MetallicMap);
#if _MAP_ROTATION
DECLARE_MAP_ROT(_MetallicMap);
#endif

// Emission
fixed4 _Emission;

bool _UseEmissionMap;
DECLARE_MAP(_EmissionMap);
#if _MAP_ROTATION
DECLARE_MAP_ROT(_EmissionMap);
#endif

// Cutout
bool _UseCutout;
half _CutoutThreshold;

DECLARE_MAP(_CutoutMap);
#if _MAP_ROTATION
DECLARE_MAP_ROT(_CutoutMap);
#endif

void ComputeOpaqueShader(float2 texcoord, inout SurfaceOutputStandard o)
{
    if (_UseCutout)
    {
        float2 cutoutUV = texcoord;

        #if _MAP_ROTATION
        ROTATE_UV(cutoutUV, _CutoutMap);
        #endif

        half cutout = GetGrey(_CutoutMap, TRANSFORM_TEX(cutoutUV, _CutoutMap), _CutoutMap_B, _CutoutMap_I);

        clip(cutout - _CutoutThreshold);
    }

    // Albedo color
    o.Albedo = _AlbedoColor;

    // Albedo
    if (_UseAlbedoMap)
    {
        float2 albedoUV = texcoord;

        #if _MAP_ROTATION
        ROTATE_UV(albedoUV, _MainTex);
        #endif

        fixed4 c = GetColor(_MainTex, TRANSFORM_TEX(albedoUV, _MainTex), _MainTex_B, _MainTex_I);

        o.Albedo = lerp(o.Albedo, c, _MainTex_Fade);
    }

    // Tint
    o.Albedo *= _Tint;

    // Normal
    if (_UseNormalMap)
    {
        float2 normalUV = texcoord;

        #if _MAP_ROTATION
        ROTATE_UV(normalUV, _BumpMap);
        #endif

        half4 n = tex2D(_BumpMap, TRANSFORM_TEX(normalUV, _BumpMap));
        half scale = (1.0f + _BumpMap_I * -2.0f) * _BumpScale * _BumpMap_B;

        o.Normal = UnpackScaleNormal(n, scale);
    }

    // Smoothness
    o.Smoothness = _Smoothness;

    if (_UseSmoothnessMap)
    {
        float2 smoothnessUV = texcoord;

        #if _MAP_ROTATION
        ROTATE_UV(smoothnessUV, _SmoothnessMap);
        #endif

        half s = GetGrey(_SmoothnessMap, TRANSFORM_TEX(smoothnessUV, _SmoothnessMap), _SmoothnessMap_B, _SmoothnessMap_I);

        o.Smoothness *= s;
    }

    // Metallic
    o.Metallic = _Metallic;

    if (_UseMetallicMap)
    {
        float2 metallicUV = texcoord;

        #if _MAP_ROTATION
        ROTATE_UV(metallicUV, _MetallicMap);
        #endif

        half m = GetGrey(_MetallicMap, TRANSFORM_TEX(metallicUV, _MetallicMap), _MetallicMap_B, _MetallicMap_I);

        o.Metallic *= m;
    }

    // Emission
    o.Emission = _Emission;

    if (_UseEmissionMap)
    {
        float2 emissionUV = texcoord;

        #if _MAP_ROTATION
        ROTATE_UV(emissionUV, _EmissionMap);
        #endif

        half4 e = GetColor(_EmissionMap, TRANSFORM_TEX(emissionUV, _EmissionMap), _EmissionMap_B, _EmissionMap_I);

        o.Emission = e;
    }
}

#endif // UNITY_SYNC_STANDARD_CORE_INCLUDED
