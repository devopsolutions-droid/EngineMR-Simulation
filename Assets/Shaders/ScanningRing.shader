Shader "Custom/ScanningRing"
{
    Properties
    {
        // ── Ring appearance ───────────────────────────────────────────────────
        _RingColor      ("Ring Color",       Color)     = (0.0, 0.85, 1.0, 1.0)
        _GlowColor      ("Glow Color",       Color)     = (0.5, 1.0,  1.0, 1.0)
        _GlowWidth      ("Glow Width",       Range(0, 0.5)) = 0.15

        // ── Animation ─────────────────────────────────────────────────────────
        _PulseSpeed     ("Pulse Speed",      Float)     = 4.0
        _PulseIntensity ("Pulse Intensity",  Range(0, 1)) = 0.3

        // ── Fade ──────────────────────────────────────────────────────────────
        _FadeAlpha      ("Fade Alpha",       Range(0, 1)) = 1.0
    }

    SubShader
    {
        Tags { "Queue"="Transparent+150" "RenderType"="Transparent" "IgnoreProjector"="True" }
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off

        Pass
        {
            Name "ScanningRingPass"
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            // ── Properties ───────────────────────────────────────────────────────
            fixed4 _RingColor;
            fixed4 _GlowColor;
            float  _GlowWidth;
            float  _PulseSpeed;
            float  _PulseIntensity;
            float  _FadeAlpha;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos      : SV_POSITION;
                float2 uv       : TEXCOORD0;
                float3 viewDir  : TEXCOORD1;
                float3 normal   : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                // Apply a subtle breathing pulse to the ring size
                float pulse = 1.0 + _PulseIntensity * 0.03 * sin(_Time.y * _PulseSpeed);
                float3 scaledPos = v.vertex.xyz * pulse;

                o.pos     = UnityObjectToClipPos(float4(scaledPos, 1.0));
                o.uv      = v.uv;
                o.normal  = UnityObjectToWorldNormal(v.normal);
                o.viewDir = normalize(_WorldSpaceCameraPos - mul(unity_ObjectToWorld, float4(scaledPos, 1.0)).xyz);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // ── Ring shape ──────────────────────────────────────────────────
                // uv.x = 0 at inner edge, 1 at outer edge
                // Create a smooth ring profile: bright center, glowing edges
                float distFromCenter = abs(i.uv.x - 0.5) * 2.0;  // 0 at center, 1 at edges
                float ringProfile    = 1.0 - smoothstep(0.0, 1.0, distFromCenter);

                // Inner edge glow
                float innerEdge = 1.0 - smoothstep(0.0, _GlowWidth, i.uv.x);
                // Outer edge glow
                float outerEdge = 1.0 - smoothstep(1.0 - _GlowWidth, 1.0, i.uv.x);

                // ── Fresnel-style rim on the ring surface ──────────────────────
                float3 N = normalize(i.normal);
                float3 V = normalize(i.viewDir);
                float rim = 1.0 - saturate(dot(N, V));
                rim = pow(rim, 2.0) * 0.6;

                // ── Pulse glow ──────────────────────────────────────────────────
                float pulse = 0.5 + 0.5 * sin(_Time.y * _PulseSpeed);
                float pulseGlow = pulse * _PulseIntensity;

                // ── Compose ────────────────────────────────────────────────────
                fixed3 baseCol  = _RingColor.rgb;
                fixed3 glowCol  = _GlowColor.rgb;
                fixed3 col      = baseCol * ringProfile;

                // Add edge glow
                col += glowCol * (innerEdge + outerEdge) * 0.8;

                // Add rim
                col += baseCol * rim;

                // Add pulse glow
                col += glowCol * pulseGlow * 0.5;

                // ── Alpha ──────────────────────────────────────────────────────
                float alpha = ringProfile * _FadeAlpha * 0.9;
                alpha += innerEdge * _FadeAlpha * 0.5;
                alpha += outerEdge * _FadeAlpha * 0.5;
                alpha += rim * _FadeAlpha * 0.3;
                alpha = saturate(alpha);

                return fixed4(col, alpha);
            }
            ENDCG
        }
    }
}