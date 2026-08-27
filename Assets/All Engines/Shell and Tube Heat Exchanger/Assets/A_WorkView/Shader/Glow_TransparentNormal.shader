Shader "Custom/Glow/TransparentNormal" {
	Properties {
		_Color ("Color", Color) = (1,1,1,1)
		_Transparency("Transparency", Range(0.0,1)) = 1
		_RimColor ("Rim Color", Color) = (.5,.5,.5,1)
		_RimWidth ("Rim Power", Range(0,5)) = 1
		_MainTex ("Albedo (RGB)", 2D) = "white" {}
		_Glossiness ("Smoothness", Range(0,1)) = 0.5
		_Metallic ("Metallic", Range(0,1)) = 0.0
		_BumpMap("NormalMap(RGB)",2D) = "white" {}

	}
	SubShader
 {
 Tags {"Queue"="Transparent" "RenderType"="Transparent" }



    Pass {
                 ColorMask 0


             }

    ZWrite Off
    //ZTest Greater
    Blend One One

     
    CGPROGRAM
    #pragma surface  surf Standard fullforwardshadows 
    struct Input {
      float2 uv_MainTex;
      float2 uv_BumpMap;
      float3 viewDir;
    };
    //sampler2D _MainTex;
    sampler2D _BumpMap;
    float4 _RimColor;
    float _RimWidth;
    half _Glossiness;
	half _Metallic;

    void surf (Input IN, inout SurfaceOutputStandard o) {
      o.Normal = UnpackNormal (tex2D (_BumpMap, IN.uv_BumpMap));
      o.Metallic = _Metallic;
	  o.Smoothness = _Glossiness;
      half rim = 1.0 - saturate(dot (normalize(IN.viewDir), o.Normal));
      o.Emission = _RimColor.rgb * pow (rim, _RimWidth);
    }
    ENDCG  
   
   ZWrite Off
   Blend One One
   ColorMask RGB
   
    CGPROGRAM
    #pragma surface surf Standard fullforwardshadows alpha:blend
 
    sampler2D _MainTex;
    sampler2D _BumpMap;
    fixed4 _Color;
    float _Transparency;
    half _Glossiness;
    half _Metallic;
     float4 _RimColor;
    float _RimWidth;
 
    struct Input {
        float2 uv_MainTex;
        float2 uv_BumpMap;
        float3 viewDir;
    };
 
    void surf (Input IN, inout SurfaceOutputStandard o) {
        fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
        o.Albedo = c.rgb;
        o.Metallic = _Metallic;
	    o.Smoothness = _Glossiness;
        o.Alpha = _Transparency;
        o.Normal = UnpackNormal(tex2D(_BumpMap, IN.uv_BumpMap));
        half rim = 1.0 - saturate(dot (normalize(IN.viewDir), o.Normal));
      o.Emission = _RimColor.rgb * pow (rim, _RimWidth);
    }
    ENDCG
 }
	FallBack "Diffuse"
}
