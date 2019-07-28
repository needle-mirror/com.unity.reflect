Shader "UnityReflect/Standard Refract"
{
    Properties
    {
        [Space]
        _AlbedoColor("Color", Color) = (1,1,1,0)
        _MainTex("Albedo (RGB)", 2D) = "white" {}
        _MainTex_Fade("Fade", Range(0, 1)) = 1
        _MainTex_B("Brightness", Range(0, 1)) = 1
        _MainTex_R("Angle", Range(0, 360)) = 0
        [Toggle] _MainTex_I("Invert", Float) = 0

        [Space]
        _BumpScale("Bump Scale", Float) = 1.0
        _BumpMap("Bump", 2D) = "bump" {}
        _BumpMap_B("Brightness", Range(0, 1)) = 1
        _BumpMap_R("Angle", Range(0, 360)) = 0
        [Toggle] _BumpMap_I("Invert", Float) = 0

        [Space]
        _Alpha("Alpha", Range(0,1)) = 1.0
        _AlphaMap("Alpha Map", 2D) = "white" {}
        _AlphaMap_B("Brightness", Range(0, 1)) = 1
        _AlphaMap_R("Angle", Range(0, 360)) = 0
        [Toggle] _AlphaMap_I("Invert", Float) = 0

        [Space]
        _CutoutThreshold("Cutout Threshold", Range(0,1)) = 0.5
        _CutoutMap("Cutout", 2D) = "white" {}
        _CutoutMap_B("Brightness", Range(0, 1)) = 1
        _CutoutMap_R("Angle", Range(0, 360)) = 0
        [Toggle] _CutoutMap_I("Invert", Float) = 0

        [Space]
        _Smoothness("Smoothness", Range(0,1)) = 0.5
        _SmoothnessMap("Smoothness Map (R)", 2D) = "white" {}
        _SmoothnessMap_B("Brightness", Range(0, 1)) = 1
        _SmoothnessMap_R("Angle", Range(0, 360)) = 0
        [Toggle] _SmoothnessMap_I("Invert", Float) = 0

        [Space]
        _Metallic("Metallic", Range(0,1)) = 0.0
        _MetallicMap("Metallic Map (R)", 2D) = "white" {}
        _MetallicMap_B("Brightness", Range(0, 1)) = 1
        _MetallicMap_R("Angle", Range(0, 360)) = 0
        [Toggle] _MetallicMap_I("Invert", Float) = 0

        [Space]
        [Enum(UnityEngine.Reflect.EmissionMode)] _EmissionMode("Emission Mode", Float) = 0
        _Emission("Emission", Color) = (0,0,0,1)
        _EmissionMap("Emission Map", 2D) = "black" {}
        _EmissionMap_B("Brightness", Range(0, 1)) = 1
        _EmissionMap_R("Angle", Range(0, 360)) = 0
        [Toggle] _EmissionMap_I("Invert", Float) = 0

        _Tint("Tint", Color) = (1,1,1,1)
        
        [HideInInspector] _UseAlbedoMap("__UseAlbedoMap", Float) = 0
        [HideInInspector] _UseNormalMap("__UseNormalMap", Float) = 0
        [HideInInspector] _UseSmoothnessMap("__UseSmoothnessMap", Float) = 0
        [HideInInspector] _UseMetallicMap("__UseMetallicMap", Float) = 0
        [HideInInspector] _UseEmissionMap("__UseEmissionMap", Float) = 0
        [HideInInspector] _UseCutout("__UseCutout", Float) = 0
    }

    SubShader
    {
        GrabPass { }

        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Opaque" }
        LOD 200

        CGPROGRAM

        #pragma surface surf Standard vertex:vert
        #pragma target 3.0

        #pragma multi_compile _ _MAP_ROTATION
        #pragma multi_compile _ _ALPHA_MAP

        #include "UnityReflectStandardCore.cginc"

        half _Alpha;

        #if _ALPHA_MAP
        DECLARE_MAP(_AlphaMap);
        #if _MAP_ROTATION
        DECLARE_MAP_ROT(_AlphaMap);
        #endif
        #endif

        sampler2D _CameraDepthTexture;
        float4 _CameraDepthTexture_TexelSize;

        sampler2D _GrabTexture;
        float4 _GrabTexture_TexelSize;

        float2 AlignWithGrabTexel(float2 uv)
        {
            #if UNITY_UV_STARTS_AT_TOP
                if (_CameraDepthTexture_TexelSize.y < 0)
                {
                    uv.y = 1 - uv.y;
                }
            #endif

            return (floor(uv * _CameraDepthTexture_TexelSize.zw) + 0.5) * abs(_CameraDepthTexture_TexelSize.xy);
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            ComputeOpaqueShader(IN.texcoord, o);

            float2 uvOffset = o.Normal.xy;
            uvOffset.y *= _CameraDepthTexture_TexelSize.z * abs(_CameraDepthTexture_TexelSize.y);
            float2 uv = AlignWithGrabTexel((IN.screenPos.xy + uvOffset) / IN.screenPos.w);

            float backgroundDepth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv));
            float surfaceDepth = UNITY_Z_0_FAR_FROM_CLIPSPACE(IN.screenPos.z);
            float depthDifference = backgroundDepth - surfaceDepth;

            uvOffset *= saturate(depthDifference);
            uv = AlignWithGrabTexel((IN.screenPos.xy + uvOffset) / IN.screenPos.w);
            backgroundDepth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv));
            depthDifference = backgroundDepth - surfaceDepth;

            half4 col = tex2D(_GrabTexture, uv);

            half a = _Alpha;

            #if _ALPHA_MAP
            float2 alphaUV = IN.texcoord;

            #if _MAP_ROTATION
            ROTATE_UV(alphaUV, _AlphaMap);
            #endif

            half t = tex2D(_AlphaMap, TRANSFORM_TEX(alphaUV, _AlphaMap)).r;
            a *= _AlphaMap_B;
            a *= lerp(t, 1.0 - t, _AlphaMap_I);

            #endif

            o.Albedo = lerp(col.rgb, o.Albedo, a);
        }

        ENDCG
    }
    FallBack "Transparent"
    CustomEditor "UnityEditor.Reflect.UnityReflectStandardShaderGUI"
}
