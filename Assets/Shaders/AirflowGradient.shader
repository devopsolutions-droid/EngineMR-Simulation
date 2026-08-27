Shader "Custom/AirflowGradient"
{
    Properties
    {
        _ScrollSpeed  ("Scroll Speed",    Range(0, 15))  = 4.0
        _FlowTiling   ("Flow Tiling",     Range(1, 30))  = 8.0
        _NoiseTex     ("Noise Texture",   2D) = "white" {}
        _NoiseStrength("Noise Strength",  Range(0, 1))   = 0.25
        _Opacity      ("Opacity",         Range(0, 1))   = 0.097
        _GlowIntensity("Glow Intensity",  Range(1, 6))   = 2.5
        _RimPower     ("Rim Power",       Range(0.5, 6)) = 2.0
        _RimIntensity ("Rim Intensity",   Range(0, 3))   = 1.2
        // Partition: gradient t = UV.y * _UVScaleY + _UVOffsetY
        _UVOffsetY    ("UV Offset Y",     Range(0, 1))   = 0.0
        _UVScaleY     ("UV Scale Y",      Range(0, 1))   = 1.0
        // Crop: discard fragments outside [_UVClipMin, _UVClipMax] in UV.y
        _UVClipMin    ("UV Clip Min",     Range(0, 1))   = 0.0
        _UVClipMax    ("UV Clip Max",     Range(0, 1))   = 1.0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+50" "RenderType"="Transparent" }
        ZWrite Off
        ZTest LEqual
        Cull Off
        Blend One One

        Pass
        {
            Name "AirflowGradientForward"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float  _ScrollSpeed;
                float  _FlowTiling;
                float  _NoiseStrength;
                float  _Opacity;
                float  _GlowIntensity;
                float  _RimPower;
                float  _RimIntensity;
                float  _UVOffsetY;
                float  _UVScaleY;
                float  _UVClipMin;
                float  _UVClipMax;
            CBUFFER_END

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 worldNormal: TEXCOORD1;
                float3 viewDir    : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // Full engine gradient: blue(0) -> cyan(0.2) -> white(0.4) -> yellow(0.6) -> orange(0.8) -> red(1.0)
            float3 AirflowColor(float t)
            {
                t = saturate(t);
                float3 blue   = float3(0.00, 0.35, 1.00);
                float3 cyan   = float3(0.00, 0.85, 1.00);
                float3 white  = float3(1.00, 1.00, 1.00);
                float3 yellow = float3(1.00, 0.90, 0.00);
                float3 orange = float3(1.00, 0.38, 0.00);
                float3 red    = float3(1.00, 0.04, 0.00);

                float3 col = blue;
                col = lerp(col, cyan,   saturate(t / 0.20));
                col = lerp(col, white,  saturate((t - 0.20) / 0.20));
                col = lerp(col, yellow, saturate((t - 0.40) / 0.20));
                col = lerp(col, orange, saturate((t - 0.60) / 0.20));
                col = lerp(col, red,    saturate((t - 0.80) / 0.20));
                return col;
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS  = TransformObjectToHClip(v.positionOS.xyz);
                o.uv          = v.uv;
                o.worldNormal = TransformObjectToWorldNormal(v.normalOS);
                float3 worldPos = TransformObjectToWorld(v.positionOS.xyz);
                o.viewDir     = normalize(GetCameraPositionWS() - worldPos);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                // Hard crop: discard outside the assigned UV.y range
                clip(i.uv.y - _UVClipMin);
                clip(_UVClipMax - i.uv.y);

                // Map this partition's UV.y into its gradient slice
                float t = saturate(i.uv.y * _UVScaleY + _UVOffsetY);

                float speed   = _ScrollSpeed * (0.4 + t * 1.6);
                float2 flowUV = float2(i.uv.x * _FlowTiling, i.uv.y * 2.0 - _Time.y * speed);

                float2 noiseUV = float2(i.uv.x * 3.0, i.uv.y * 3.0 + _Time.y * 0.7);
                float  noise   = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV).r;
                float  turb    = (noise - 0.5) * _NoiseStrength * (0.3 + t * 0.9);

                float flow = sin((flowUV.y + turb) * 3.14159) * 0.5 + 0.5;
                flow = pow(max(flow, 0.001), 1.3 - t * 0.6);

                float3 col = AirflowColor(t);

                float3 N   = normalize(i.worldNormal);
                float3 V   = normalize(i.viewDir);
                float  rim = pow(1.0 - saturate(dot(N, V)), _RimPower);
                col += col * rim * _RimIntensity;

                col *= _GlowIntensity * (0.55 + t * 1.0);

                float alpha = flow * _Opacity * (0.4 + rim * 0.6);
                return half4(col * alpha, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
