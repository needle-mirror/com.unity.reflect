// Inspired from http://wiki.unity3d.com/index.php/Silhouette-Outlined_Diffuse

Shader "Effects/Outline" {
	Properties {
		_Color ("Main Color", Color) = (.5,.5,.5,1)
		_OutlineColor ("Outline Color", Color) = (0,0,0,1)
		_Outline ("Outline width", Range (0.0, 0.05)) = 0.02
	}
 
CGINCLUDE
#include "UnityCG.cginc"
 
struct appdata {
	float4 vertex : POSITION;
	float3 normal : NORMAL;
};
 
struct v2f {
	float4 pos : POSITION;
	float4 color : COLOR;
};
 
uniform float _Outline;
uniform float4 _OutlineColor;
 

ENDCG
 
	SubShader {
		Tags { "Queue" = "Transparent" }
 
		// note that a vertex shader is specified here but its using the one above
		Pass {
			Name "OUTLINE"
			Tags { "LightMode" = "Always" }
			Cull Off
			ZWrite Off
			ZTest LEqual 
			ColorMask RGB // alpha not used
 
			Blend SrcAlpha OneMinusSrcAlpha
 
CGPROGRAM
#pragma vertex vert
#pragma fragment frag

v2f vert(appdata v) {
	// just make a copy of incoming vertex data but scaled according to normal direction
	v2f o;
	
	float4 worldPos = mul(UNITY_MATRIX_M, v.vertex);        
    o.pos = mul(UNITY_MATRIX_VP,worldPos);
 
	float3 norm   = mul ((float3x3)UNITY_MATRIX_IT_MV, v.normal);
	float2 offset = TransformViewToProjection(norm.xy);
 
    float d = distance(_WorldSpaceCameraPos, worldPos);
 
	o.pos.xy += offset * o.pos.z * _Outline * d;
	o.color = _OutlineColor;
	return o;
}

half4 frag(v2f i) :COLOR {
	return i.color;
}
ENDCG
		}
 
		Pass {
			Name "BASE"
			ZWrite On
			ZTest LEqual
			Blend SrcAlpha OneMinusSrcAlpha
			
			Material {
				Diffuse [_Color]
				Ambient [_Color]
			}
			Lighting On
		}
	}
 
	Fallback "Diffuse"
}