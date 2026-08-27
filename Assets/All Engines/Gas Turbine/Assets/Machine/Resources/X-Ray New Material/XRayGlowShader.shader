Shader "Custom/XRayGlowShader"
{
    Properties
    {
        _MainTex ("Base (RGB)", 2D) = "white" {}
        _Color ("Main Color", Color) = (1,1,1,1)
        _GlowColor ("Glow Color", Color) = (0,1,1,1)
        _GlowIntensity ("Glow Intensity", Range(0,5)) = 1
        _Transparency ("Transparency", Range(0,1)) = 0.5
        _FresnelPower ("Fresnel Power", Range(0.1, 5)) = 2
    }
    SubShader
    {
        Tags {"Queue"="Transparent" "RenderType"="Transparent"}
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard alpha:blend

        sampler2D _MainTex;
        fixed4 _Color;
        fixed4 _GlowColor;
        half _GlowIntensity;
        half _Transparency;
        half _FresnelPower;

        struct Input
        {
            float2 uv_MainTex;
            float3 viewDir;
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Base texture
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;

            // Fresnel effect for the glow
            float fresnel = pow(1.0 - dot(normalize(IN.viewDir), o.Normal), _FresnelPower);
            fixed4 glow = _GlowColor * fresnel * _GlowIntensity;

            // Apply the transparency
            c.a = _Transparency;

            // Output the color and glow
            o.Albedo = c.rgb;
            o.Emission = glow.rgb;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Transparent/Diffuse"
}
