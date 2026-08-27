Shader "Rwizen/Wave"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _Transparency ("Transparency", Range(0, 1)) = 1.0
        _RimColor ("Rim Color", Color) = (0.5, 0.5, 0.5, 1)
        _RimWidth ("Rim Power", Range(0, 5)) = 1.0
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0, 1)) = 0.5
        _Metallic ("Metallic", Range(0, 1)) = 0.0
        _BumpMap ("NormalMap(RGB)", 2D) = "bump" {}
        _XScrollSpeed ("X Scroll Speed", Float) = 1.0
        _YScrollSpeed ("Y Scroll Speed", Float) = 1.0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Transparency;
                float4 _RimColor;
                float _RimWidth;
                float _Glossiness;
                float _Metallic;
                float _XScrollSpeed;
                float _YScrollSpeed;
                float4 _MainTex_ST;
                float4 _BumpMap_ST;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float2 uv_bump    : TEXCOORD1;
                float3 normalWS   : NORMAL;
                float3 tangentWS  : TANGENT;
                float3 bitangentWS: BITANGENT;
                float3 worldPos   : TEXCOORD3;
                float3 viewDirWS  : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                VertexPositionInputs posInputs = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = posInputs.positionCS;
                o.worldPos = posInputs.positionWS;

                VertexNormalInputs normalInputs = GetVertexNormalInputs(v.normalOS, v.tangentOS);
                o.normalWS = normalInputs.normalWS;
                o.tangentWS = normalInputs.tangentWS;
                o.bitangentWS = normalInputs.bitangentWS;

                o.uv = v.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                o.uv_bump = v.uv * _BumpMap_ST.xy + _BumpMap_ST.zw;
                o.viewDirWS = GetWorldSpaceViewDir(posInputs.positionWS);

                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float2 scrollUV = i.uv_bump;
                // Use _Time.y for normal time in seconds (matching _Time.x / _Time.y in Unity)
                scrollUV.x += _XScrollSpeed * _Time.y * 0.1;
                scrollUV.y += _YScrollSpeed * _Time.y * 0.1;

                // Sample normal map and unpack normal
                float4 normalSample = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, scrollUV);
                float3 normalTS = UnpackNormal(normalSample);

                // Construct TBN matrix to transform normal to world space
                float3 normalWS = normalize(i.normalWS);
                float3 tangentWS = normalize(i.tangentWS);
                float3 bitangentWS = normalize(i.bitangentWS);
                float3 normal = TransformTangentToWorld(normalTS, half3x3(tangentWS, bitangentWS, normalWS));
                normal = normalize(normal);

                // Albedo color
                float4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * _Color;

                // View dir and Rim light
                float3 viewDir = normalize(i.viewDirWS);
                float rim = 1.0 - saturate(dot(viewDir, normal));
                float3 rimEmission = _RimColor.rgb * pow(rim, _RimWidth);

                col.rgb += rimEmission;
                col.a *= _Transparency;

                return col;
            }
            ENDHLSL
        }
    }
    FallBack "Universal Forward"
}
