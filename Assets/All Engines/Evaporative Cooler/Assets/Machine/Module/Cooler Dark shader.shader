Shader "Custom/SpiralPipeFlowEmissionTexture"
{
    Properties
    {
        _EmptyTex ("Empty Pipe Texture", 2D) = "white" {}        
        _LiquidTex ("Liquid Texture", 2D) = "white" {}           
        _FlowTex ("Flow Texture", 2D) = "white" {}               
        _EmissionTex ("Emission Texture", 2D) = "white" {}       

        _FillAmount ("Fill Amount", Range(0,1)) = 0.0            
        _FlowSpeed ("Flow Speed", Float) = 1.0                   

        _Smoothness ("Smoothness", Range(0, 1)) = 0.5            
        _Metallic ("Metallic", Range(0, 1)) = 0.5                

        _EmissionColor ("Emission Color", Color) = (0, 0.5, 1, 1)  
        _EmissionStrength ("Emission Strength", Range(0, 10)) = 1.0 
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
            sampler2D _EmissionTex;     

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
                // Move UVs only in the Y-axis
                float2 flowUV = i.uv + float2(0, _Time.y * _FlowSpeed);

                // Sample the textures
                fixed4 emptyColor = tex2D(_EmptyTex, i.uv);          
                fixed4 liquidColor = tex2D(_LiquidTex, i.uv);        
                fixed4 flowTex = tex2D(_FlowTex, flowUV);            
                fixed4 emissionTex = tex2D(_EmissionTex, flowUV);    

                // **Reverse Fill (Top to Bottom)**
                float filled = step(1.0 - _FillAmount, i.uv.y);
                
                fixed4 pipeColor = lerp(emptyColor, liquidColor * flowTex, filled);

                // Specular reflection
                float3 normal = normalize(i.worldNormal);
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
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
