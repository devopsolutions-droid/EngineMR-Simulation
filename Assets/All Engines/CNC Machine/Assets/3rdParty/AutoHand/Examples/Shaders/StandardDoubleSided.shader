Shader "Custom/StandardDoubleSided"
{
    Properties
    {
        _MainTex("Albedo (RGB)", 2D) = "white" {}
        _MetallicGlossMap("Metallic (R) and Smoothness (A)", 2D) = "white" {}
        _BumpMap("Normal Map", 2D) = "bump" {}
        _OcclusionMap("Occlusion", 2D) = "white" {}

        _Color("Color Tint", Color) = (1,1,1,1)
        _Glossiness("Smoothness", Range(0,1)) = 0.5
        _Metallic("Metallic", Range(0,1)) = 0.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off  // Disable backface culling to render both sides

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _MetallicGlossMap;
        sampler2D _BumpMap;
        sampler2D _OcclusionMap;

        fixed4 _Color;
        half _Glossiness;
        half _Metallic;

        struct Input
        {
            float2 uv_MainTex;
            float2 uv_MetallicGlossMap;
            float2 uv_BumpMap;
            float2 uv_OcclusionMap;
            float3 viewDir;
        };

        UNITY_INSTANCING_BUFFER_START(Props)
        UNITY_INSTANCING_BUFFER_END(Props)

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            // Albedo
            fixed4 albedo = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = albedo.rgb;

            // Metallic + Smoothness
            fixed4 metallicGloss = tex2D(_MetallicGlossMap, IN.uv_MetallicGlossMap);
            o.Metallic = metallicGloss.r * _Metallic;
            o.Smoothness = metallicGloss.a * _Glossiness;

            // Normal Map
            o.Normal = UnpackNormal(tex2D(_BumpMap, IN.uv_BumpMap));

            // Occlusion
            fixed occ = tex2D(_OcclusionMap, IN.uv_OcclusionMap).r;
            o.Occlusion = occ;
        }
        ENDCG
    }

    FallBack "Diffuse"
}
