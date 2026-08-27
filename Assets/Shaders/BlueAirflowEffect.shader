Shader "Custom/AirflowEffectFlipped"
{
    Properties
    {
        _Progress       ("Progress (0=start 1=full)", Range(0,1))  = 0.0
        _ProgressFade   ("Progress Fade Width",       Range(0,0.3))= 0.08

        // Gradient colours (all set to same blue for uniform effect)
        _Colour1        ("Colour1",   Color) = (0.00, 0.40, 1.00, 1.0)
        _Colour2        ("Colour2",   Color) = (0.00, 0.90, 1.00, 1.0)
        _Colour3        ("Colour3",   Color) = (1.00, 0.95, 0.80, 1.0)
        _Colour4        ("Colour4",   Color) = (1.00, 0.50, 0.00, 1.0)
        _Colour5        ("Colour5",   Color) = (1.00, 0.15, 0.00, 1.0)

        // Gradient keypoint positions along the tube (UV.y = 0 intake → 1 exhaust)
        _Key1           ("Key1 UV.y", Range(0,1)) = 0.00
        _Key2           ("Key2 UV.y", Range(0,1)) = 0.20
        _Key3           ("Key3 UV.y", Range(0,1)) = 0.42
        _Key4           ("Key4 UV.y", Range(0,1)) = 0.70
        _Key5           ("Key5 UV.y", Range(0,1)) = 1.00

        _ScrollSpeed   ("Scroll Speed",  Range(0,10))  = 2.0
        _FlowTiling    ("Flow Tiling",   Range(1,20))  = 6.0
        _NoiseTex      ("Noise Texture", 2D) = "white" {}
        _NoiseStrength ("Noise Strength",Range(0,1))   = 0.3
        _NoiseSpeed    ("Noise Speed",   Range(0,5))   = 1.2
        _NoiseTiling   ("Noise Tiling",  Range(1,20))  = 4.0
        _RimPower      ("Rim Power",     Range(0.5,6)) = 2.5
        _RimIntensity  ("Rim Intensity", Range(0,3))   = 1.5
        _Opacity       ("Opacity",       Range(0,1))   = 0.85
        _GlowIntensity ("Glow Intensity",Range(1,5))   = 2.0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+50" "RenderType"="Transparent" "IgnoreProjector"="True" }
        ZWrite Off
        ZTest LEqual
        Cull Off
        Blend One One   // Additive glow

        Pass
        {
            Name "AirflowForward"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float  _Progress;
                float  _ProgressFade;
                float4 _Colour1;
                float4 _Colour2;
                float4 _Colour3;
                float4 _Colour4;
                float4 _Colour5;
                float  _Key1;
                float  _Key2;
                float  _Key3;
                float  _Key4;
                float  _Key5;
                float  _ScrollSpeed;
                float  _FlowTiling;
                float  _NoiseStrength;
                float  _NoiseSpeed;
                float  _NoiseTiling;
                float  _RimPower;
                float  _RimIntensity;
                float  _Opacity;
                float  _GlowIntensity;
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

            // Sample the gradient at a normalised position t (0..1 along tube)
            float3 GradientAt(float t)
            {
                float3 c1 = _Colour1.rgb;
                float3 c2 = _Colour2.rgb;
                float3 c3 = _Colour3.rgb;
                float3 c4 = _Colour4.rgb;
                float3 c5 = _Colour5.rgb;

                if (t <= _Key2)
                {
                    float f = (t - _Key1) / max(_Key2 - _Key1, 0.001);
                    return lerp(c1, c2, f);
                }
                else if (t <= _Key3)
                {
                    float f = (t - _Key2) / max(_Key3 - _Key2, 0.001);
                    return lerp(c2, c3, f);
                }
                else if (t <= _Key4)
                {
                    float f = (t - _Key3) / max(_Key4 - _Key3, 0.001);
                    return lerp(c3, c4, f);
                }
                else
                {
                    float f = (t - _Key4) / max(_Key5 - _Key4, 0.001);
                    return lerp(c4, c5, f);
                }
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                // ── Progress cutoff ───────────────────────────────────────────
                // UV.y runs along the tube: 0 = intake end, 1 = exhaust end
                // The Airflow_v6.1 meshes have UV.y going EXHAUST(0) → INTAKE(1),
                // so we flip it: (1.0 - i.uv.y) to get the correct direction.
                float distFromProgress = _Progress - (1.0 - i.uv.y);
                float progressFade = saturate(distFromProgress / max(_ProgressFade, 0.001));

                // If we're past the progress point, fade to transparent
                if (progressFade < 0.001) return half4(0, 0, 0, 0);

                // ── Gradient color from UV.y position ─────────────────────────
                float3 gradCol = GradientAt(i.uv.y);

                // ── Scrolling ring pattern (adds energy lines on top) ─────────
                float speed   = _ScrollSpeed * (1.0 + _Progress * 3.0);
                float2 flowUV = float2(i.uv.x * _FlowTiling, i.uv.y - _Time.y * speed);

                // ── Noise turbulence ──────────────────────────────────────────
                float2 noiseUV    = float2(i.uv.x * _NoiseTiling, i.uv.y * _NoiseTiling + _Time.y * _NoiseSpeed);
                float  noise      = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV).r;
                float  turbulence = noise * _NoiseStrength * (0.5 + _Progress * 1.5);

                // ── Flow ring pattern for the energy lines ────────────────────
                float flow = sin((flowUV.y + turbulence) * 3.14159) * 0.5 + 0.5;
                flow = pow(max(flow, 0.0001), 1.5 - _Progress * 0.8);

                // ── Fresnel rim (stronger at the progress front) ──────────────
                float3 N   = normalize(i.worldNormal);
                float3 V   = normalize(i.viewDir);
                float  rim = pow(1.0 - saturate(dot(N, V)), _RimPower);

                // ── Combine ───────────────────────────────────────────────────
                float3 col = gradCol;
                // Add the scrolling ring energy
                col += gradCol * flow * 0.5;
                // Add rim glow, stronger near the progress front
                float rimBoost = 1.0 + (1.0 - saturate(abs(i.uv.y - _Progress) / 0.1)) * 2.0;
                col += gradCol * rim * _RimIntensity * rimBoost;

                float alpha = progressFade * _Opacity * (0.6 + rim * 0.4);
                col *= _GlowIntensity * (1.0 + _Progress * 1.5);

                return half4(col * alpha, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}