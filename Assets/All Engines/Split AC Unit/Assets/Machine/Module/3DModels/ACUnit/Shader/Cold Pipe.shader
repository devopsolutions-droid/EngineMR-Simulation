Shader "Custom/COLDPipeFlow"
{
    Properties
    {
        _EmptyTex ("Empty Pipe Texture", 2D) = "white" {}      // Texture for empty pipe
        _FlowTex ("Flow Texture", 2D) = "white" {}             // Flowing overlay texture
        _NormalMap ("Normal Map", 2D) = "bump" {}              // Normal map for surface details

        _LiquidColor ("Liquid Color", Color) = (0, 0, 1, 1)    // Color of the liquid
        _FillAmount ("Fill Amount", Range(0,1)) = 0.0          // Liquid fill progression
        _FlowSpeed ("Flow Speed", Float) = 1.0                 // Flow texture scrolling speed
        _NormalStrength ("Normal Strength", Range(0, 2)) = 1.0 // Strength of the normal map

        _Smoothness ("Smoothness", Range(0, 1)) = 0.5          // Surface smoothness
        _Metallic ("Metallic", Range(0, 1)) = 0.5              // Metallic reflection
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
            #include "Lighting.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;  
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 worldNormal : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
                float3 worldTangent : TEXCOORD3;
                float3 worldBinormal : TEXCOORD4;
            };

            sampler2D _EmptyTex;
            sampler2D _FlowTex;
            sampler2D _NormalMap;

            float4 _LiquidColor;  // Liquid color instead of a texture
            float _FillAmount;
            float _FlowSpeed;
            float _NormalStrength;
            float _Smoothness;
            float _Metallic;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                
                float3 worldTangent = UnityObjectToWorldDir(v.tangent.xyz);
                float3 worldBinormal = cross(o.worldNormal, worldTangent) * v.tangent.w;
                
                o.worldTangent = worldTangent;
                o.worldBinormal = worldBinormal;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // UV flow animation for liquid texture
                float2 flowUV = i.uv + float2(0, _Time.y * _FlowSpeed);
                fixed4 flowTex = tex2D(_FlowTex, flowUV);   // Flow texture pattern

                // Sample the empty pipe texture
                fixed4 emptyColor = tex2D(_EmptyTex, i.uv); 

                // **Reverse Fill (Top to Bottom)**
                float filled = step(1.0 - _FillAmount, i.uv.y);
                fixed4 liquidColor = _LiquidColor * flowTex;   // Liquid is now just a color
                fixed4 pipeColor = lerp(emptyColor, liquidColor, filled);

                // Sample normal map and apply strength
                float3 normalMap = UnpackNormal(tex2D(_NormalMap, i.uv));
                normalMap = normalize(lerp(float3(0, 0, 1), normalMap, _NormalStrength));

                // Transform normal map to world space
                float3 worldNormal = normalize(
                    normalMap.x * i.worldTangent +
                    normalMap.y * i.worldBinormal +
                    normalMap.z * i.worldNormal
                );

                // Specular reflection
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                float3 reflectDir = reflect(-viewDir, worldNormal);
                float spec = pow(max(0, dot(viewDir, reflectDir)), 32.0) * _Smoothness;

                // Combine final color with metallic and smoothness
                float3 finalColor = pipeColor.rgb * (1 - _Metallic) + spec * _Metallic;

                return fixed4(finalColor, 1.0);
            }
            ENDCG
        }
    }
}
