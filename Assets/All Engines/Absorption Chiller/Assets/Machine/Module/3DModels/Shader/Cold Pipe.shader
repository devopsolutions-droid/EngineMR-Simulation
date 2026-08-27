Shader "Custom/ColdPipeFlowOptimizedStandard"
{
    Properties
    {
        _EmptyTex ("Empty Pipe Texture", 2D) = "white" {}
        _EmptyTiling ("Empty Pipe Tiling", Vector) = (1,1,0,0)
        _EmptyAlpha ("Empty Pipe Transparency", Range(0,1)) = 1.0

        _LiquidTex ("Liquid Texture", 2D) = "white" {}
        _LiquidTiling ("Liquid Tiling", Vector) = (1,1,0,0)
        _LiquidColor ("Liquid Tint Color", Color) = (0,0.5,1,1)
        //_LiquidAlpha ("Liquid Transparency", Range(0,1)) = 1.0

        _FlowTex ("Flow Texture", 2D) = "white" {}
        _FlowTiling ("Flow Tiling", Vector) = (1,1,0,0)
        //_FlowAlpha ("Flow Texture Transparency", Range(0,1)) = 1.0
        _FlowSpeed ("Flow Speed", Range(0,5)) = 1.0

        _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalStrength ("Normal Strength", Range(0,2)) = 1.0

        _FillAmount ("Fill Amount", Range(0,1)) = 0.0

        _Metallic ("Metallic", Range(0,1)) = 0.0
        _Smoothness ("Smoothness", Range(0,1)) = 0.5

        _EmissionTex ("Emission Texture", 2D) = "white" {}
        _EmissionColor ("Emission Color", Color) = (0,0,0,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 200
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        CGPROGRAM
        #pragma surface surf Standard alpha:fade fullforwardshadows
        #pragma target 3.0
        sampler2D _EmptyTex;
        float4 _EmptyTiling;
        float _EmptyAlpha;
        sampler2D _LiquidTex;
        float4 _LiquidTiling;
        float4 _LiquidColor;
        //float _LiquidAlpha;
        sampler2D _FlowTex;
        float4 _FlowTiling;
        //float _FlowAlpha;
        float _FlowSpeed;
        sampler2D _NormalMap;
        float _NormalStrength;
        float _FillAmount;
        float _Metallic;
        float _Smoothness;
        sampler2D _EmissionTex;
        float4 _EmissionColor;

        struct Input
        {
            float2 uv_EmptyTex;
            float2 uv_LiquidTex;
            float2 uv_FlowTex;
            float2 uv_NormalMap;
            float2 uv_EmissionTex;
        };

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            // Empty pipe UV and color
            float2 uvEmpty = IN.uv_EmptyTex * _EmptyTiling.xy;
            fixed4 emptyCol = tex2D(_EmptyTex, uvEmpty);
            emptyCol.a *= _EmptyAlpha;

            // Liquid UV, tint, and transparency
            float2 uvLiquid = IN.uv_LiquidTex * _LiquidTiling.xy;
            fixed4 liquidCol = tex2D(_LiquidTex, uvLiquid) * _LiquidColor;
           // liquidCol.a = _LiquidAlpha;

            // Flow UV offset and transparency
            float2 uvFlow = IN.uv_FlowTex * _FlowTiling.xy + float2(0, _Time.y * _FlowSpeed);
            fixed4 flowCol = tex2D(_FlowTex, uvFlow);
            //flowCol.a *= _FlowAlpha;

            // Combine liquid tint and flow pattern
            liquidCol.rgb *= flowCol.rgb;

            // Fill mask
            float filled = step(1.0 - _FillAmount, IN.uv_LiquidTex.y);
            fixed4 col = lerp(emptyCol, liquidCol, filled);

            // Albedo and transparency
            o.Albedo = col.rgb;
            o.Alpha = col.a;

            // Metallic & smoothness for PBR
            o.Metallic = _Metallic;
            o.Smoothness = _Smoothness;

            // Normal mapping
            fixed4 n = tex2D(_NormalMap, IN.uv_NormalMap * _LiquidTiling.xy);
            o.Normal = UnpackNormal(n) * _NormalStrength;

            // Emission
            fixed4 e = tex2D(_EmissionTex, IN.uv_EmissionTex * _LiquidTiling.xy) * _EmissionColor;
            o.Emission = e.rgb * filled;
        }
        ENDCG
    }
    FallBack "Transparent/Diffuse"
}

