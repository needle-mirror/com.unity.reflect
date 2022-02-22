Shader "UnityReflect/Standard Transparent"
{
    Properties
    {
        [Space]
        _Reflect_AlbedoColor("Color", Color) = (1,1,1,0)
        _Reflect_MainTex("Albedo (RGB)", 2D) = "white" {}
        _Reflect_MainTex_Fade("Fade", Range(0, 1)) = 1
        _Reflect_MainTex_B("Brightness", Range(0, 1)) = 1
        _Reflect_MainTex_R("Angle", Range(0, 360)) = 0
        [Toggle] _Reflect_MainTex_I("Invert", Float) = 0

        [Space]
        _Reflect_NormalScale("Bump Scale", Float) = 1.0
        _Reflect_NormalMap("Bump", 2D) = "bump" {}
        _Reflect_NormalMap_B("Brightness", Range(0, 1)) = 1
        _Reflect_NormalMap_R("Angle", Range(0, 360)) = 0
        [Toggle] _Reflect_NormalMap_I("Invert", Float) = 0

        [Space]
        _Reflect_Alpha("Alpha", Range(0,1)) = 1.0
        _Reflect_AlphaMap("Alpha Map", 2D) = "white" {}
        _Reflect_AlphaMap_B("Brightness", Range(0, 1)) = 1
        _Reflect_AlphaMap_R("Angle", Range(0, 360)) = 0
        [Toggle] _Reflect_AlphaMap_I("Invert", Float) = 0

        [Space]
        _Reflect_CutoutThreshold("Cutout Threshold", Range(0,1)) = 0.5
        _Reflect_CutoutMap("Cutout", 2D) = "white" {}
        _Reflect_CutoutMap_B("Brightness", Range(0, 1)) = 1
        _Reflect_CutoutMap_R("Angle", Range(0, 360)) = 0
        [Toggle] _Reflect_CutoutMap_I("Invert", Float) = 0

        [Space]
        _Reflect_Smoothness("Smoothness", Range(0,1)) = 0.5
        _Reflect_SmoothnessMap("Smoothness Map (R)", 2D) = "white" {}
        _Reflect_SmoothnessMap_B("Brightness", Range(0, 1)) = 1
        _Reflect_SmoothnessMap_R("Angle", Range(0, 360)) = 0
        [Toggle] _Reflect_SmoothnessMap_I("Invert", Float) = 0

        [Space]
        _Reflect_Metallic("Metallic", Range(0,1)) = 0.0
        _Reflect_MetallicMap("Metallic Map (R)", 2D) = "white" {}
        _Reflect_MetallicMap_B("Brightness", Range(0, 1)) = 1
        _Reflect_MetallicMap_R("Angle", Range(0, 360)) = 0
        [Toggle] _Reflect_MetallicMap_I("Invert", Float) = 0

        [Space]
        [Enum(UnityEngine.Reflect.EmissionMode)] _Reflect_EmissionMode("Emission Mode", Float) = 0
        _Emission("Emission", Color) = (0,0,0,1)
        _Reflect_EmissionMap("Emission Map", 2D) = "black" {}
        _Reflect_EmissionMap_B("Brightness", Range(0, 1)) = 1
        _Reflect_EmissionMap_R("Angle", Range(0, 360)) = 0
        [Toggle] _Reflect_EmissionMap_I("Invert", Float) = 0

        _Reflect_Tint("Tint", Color) = (1,1,1,1)
        
        [HideInInspector] _Reflect_UseAlbedoMap("__UseAlbedoMap", Float) = 0
        [HideInInspector] _Reflect_UseNormalMap("__UseNormalMap", Float) = 0
        [HideInInspector] _Reflect_UseSmoothnessMap("__UseSmoothnessMap", Float) = 0
        [HideInInspector] _Reflect_UseMetallicMap("__UseMetallicMap", Float) = 0
        [HideInInspector] _Reflect_UseEmissionMap("__UseEmissionMap", Float) = 0
        [HideInInspector] _Reflect_UseCutout("__UseCutout", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" }
        LOD 200

        CGPROGRAM

        #pragma surface surf Standard alpha vertex:vert
        #pragma target 3.0

        #pragma multi_compile _ _REFLECT_MAP_ROTATION
        #pragma multi_compile _ _REFLECT_ALPHA_MAP

        #include "UnityReflectStandardCore.cginc"

        half _Reflect_Alpha;

        #if _REFLECT_ALPHA_MAP
        DECLARE_MAP(_Reflect_AlphaMap);
        #if _REFLECT_MAP_ROTATION
        DECLARE_MAP_ROT(_Reflect_AlphaMap);
        #endif
        #endif

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            ComputeOpaqueShader(IN.texcoord, o);

            o.Alpha = _Reflect_Alpha;

            #if _REFLECT_ALPHA_MAP

            float2 alphaUV = IN.texcoord;

            #if _REFLECT_MAP_ROTATION
            ROTATE_UV(alphaUV, _Reflect_AlphaMap);
            #endif

            half a = tex2D(_Reflect_AlphaMap, TRANSFORM_TEX(alphaUV, _Reflect_AlphaMap)).r;
            a *= _Reflect_AlphaMap_B;
            a = lerp(a, 1.0 - a, _Reflect_AlphaMap_I);

            o.Alpha *= a;

            #endif
        }

        ENDCG
    }
    FallBack "Transparent"
    CustomEditor "UnityEditor.Reflect.UnityReflectStandardShaderGUI"
}
