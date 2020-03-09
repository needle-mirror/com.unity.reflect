// Inspired from http://wiki.unity3d.com/index.php/Silhouette-Outlined_Diffuse

Shader "Effects/Outline"
{
	Properties {
		_Color ("Main Color", Color) = (.5,.5,.5,1)
	}
 
	SubShader
	{
		Tags { "Queue" = "Transparent" }
 
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