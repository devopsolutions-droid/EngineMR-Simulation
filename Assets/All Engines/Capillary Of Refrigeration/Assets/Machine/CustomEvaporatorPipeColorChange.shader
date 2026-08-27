Shader "Custom/SpiralPipeFlowEmissionTexture"
{
    Properties
    {
        _EmptyTex ("Empty Pipe Texture", 2D) = "white" {}        // Empty pipe texture
        _LiquidTex ("Liquid Texture", 2D) = "white" {}           // Liquid texture
        _FlowTex ("Flow Texture", 2D) = "white" {}               // Flowing overlay texture

        _EmissionTex ("Emission Texture", 2D) = "white" {}       // Emission texture

        _FillAmount ("Fill Amount", Range(0,1)) = 0.0            // Liquid fill progression
        _FlowSpeed ("Flow Speed", Float) = 1.0                   // Flow texture scrolling speed

        _Smoothness ("Smoothness", Range(0, 1)) = 0.5            // Glossiness
        _Metallic ("Metallic", Range(0, 1)) = 0.5                // Metallic reflection

        _EmissionColor ("Emission Color", Color) = (0, 0.5, 1, 1)  // Emission color
        _EmissionStrength ("Emission Strength", Range(0, 10)) = 1.0 // Emission intensity
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Lighting.cginc"  // Lighting support

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;  
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 worldNormal : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
            };

            sampler2D _EmptyTex;
            sampler2D _LiquidTex;
            sampler2D _FlowTex;
            sampler2D _EmissionTex;     // Emission texture

            float _FillAmount;
            float _FlowSpeed;
            float _Smoothness;
            float _Metallic;

            float4 _EmissionColor;
            float _EmissionStrength;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // UV flow animation for liquid texture
                float2 flowUV = i.uv + float2(_Time.y * _FlowSpeed, 0);

                // Sample the textures
                fixed4 emptyColor = tex2D(_EmptyTex, i.uv);          // Empty pipe texture
                fixed4 liquidColor = tex2D(_LiquidTex, i.uv);        // Liquid texture
                fixed4 flowTex = tex2D(_FlowTex, flowUV);            // Flow texture
                fixed4 emissionTex = tex2D(_EmissionTex, flowUV);    // Emission texture (scrolling)

                // Fill progression
                float filled = step(i.uv.y, _FillAmount);
                fixed4 pipeColor = lerp(emptyColor, liquidColor * flowTex, filled);

                // Metallic and smoothness properties
                float3 normal = normalize(i.worldNormal);
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);

                // Specular reflection
                float3 reflectDir = reflect(-viewDir, normal);
                float spec = pow(max(0, dot(viewDir, reflectDir)), 32.0) * _Smoothness;

                // Combine final color with metallic and smoothness
                float3 finalColor = pipeColor.rgb * (1 - _Metallic) + spec * _Metallic;

                // Emission effect with emission texture
                float3 emission = _EmissionColor.rgb * emissionTex.rgb * _EmissionStrength * filled;

                return fixed4(finalColor + emission, 1.0);
            }
            ENDCG
        }
    }
}
