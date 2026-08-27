Shader "Rwizen/Liquid2"
{
    Properties
    {
        _Color("Color", Color) = (1,0,0,0)
        _Speed("Speed", Float) = 0
        _Albedo("Albedo", 2D) = "white" {}
        _Size("Size", Float) = 0.1
        _Height("Height", Float) = 0
        _WavePatternColor("Wave Pattern Color", Color) = (0.2470588,0.7764706,0.9098039,1)
        _WavePattern("Wave Pattern", 2D) = "white" {}
        [IntRange]_WavePatternSize("Wave Pattern Size", Range( 1 , 20)) = 5
        _Falloff("Falloff", Float) = 0.02
        _WavePatternPower("Wave Pattern Power", Range( 0 , 100)) = 5
        _Opacity("Opacity", Float) = 0.76
        _WaveAnimSpeed("Wave Anim Speed", Range( -10 , 10)) = 3
        _WavesPattern("WavesPattern", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Overlay" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            ZTest Always
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Speed;
                float _Size;
                float _Height;
                float4 _WavePatternColor;
                float _WavePatternSize;
                float _Falloff;
                float _WavePatternPower;
                float _Opacity;
                float _WaveAnimSpeed;
                float4 _Albedo_ST;
                float4 _WavePattern_ST;
                float4 _WavesPattern_ST;
            CBUFFER_END

            TEXTURE2D(_Albedo);
            SAMPLER(sampler_Albedo);
            TEXTURE2D(_WavePattern);
            SAMPLER(sampler_WavePattern);
            TEXTURE2D(_WavesPattern);
            SAMPLER(sampler_WavesPattern);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;

                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                // Height progress cutoff
                float cutoff = 1.0 - _Height;
                float clampResult = saturate((i.uv.y - cutoff) / _Falloff);
                float opacityWS = clampResult * _Opacity;

                // Base albedo
                float2 uv_Albedo = i.uv * _Albedo_ST.xy + _Albedo_ST.zw;
                float4 albedoColor = _Color * SAMPLE_TEXTURE2D(_Albedo, sampler_Albedo, uv_Albedo);

                // Wave pattern 1 scrolling
                float2 waveSize = float2(_WavePatternSize, _WavePatternSize);
                float2 waveScroll = float2(1.0, _Time.y * _WaveAnimSpeed);
                float2 uv_Wave1 = i.uv * waveSize + waveScroll;
                float4 wave1 = SAMPLE_TEXTURE2D(_WavePattern, sampler_WavePattern, uv_Wave1);

                // Wave pattern 2 scrolling
                float2 wavesScroll = float2(1.0, 1.0 - (_Time.y * _WaveAnimSpeed / 5.0));
                float2 uv_Wave2 = i.uv * float2(1.0, 1.0) + wavesScroll;
                float4 wave2 = SAMPLE_TEXTURE2D(_WavesPattern, sampler_WavesPattern, uv_Wave2);

                // Combine waves
                float4 waveColor = (wave1 * wave2) * _WavePatternColor;

                // Blend emission and apply power
                float4 emission = lerp(float4(0, 0, 0, 0), waveColor, clampResult) * _WavePatternPower;

                float4 finalColor;
                finalColor.rgb = albedoColor.rgb + emission.rgb;
                finalColor.a = opacityWS;

                // Pre-multiply alpha because the original has Blend One OneMinusSrcAlpha or similar
                finalColor.rgb *= finalColor.a;

                return finalColor;
            }
            ENDHLSL
        }
    }
    FallBack "Universal Forward"
}