Shader "Custom/Outline"
{
    Properties
    {
        _OutlineColor   ("Outline Color", Color)   = (1, 0.08, 0.08, 1)
        _OutlineWidth   ("Outline Width (px)", Float) = 3.0
        _PulseSpeed     ("Pulse Speed", Float)       = 3.0
        _PulseIntensity ("Pulse Intensity", Range(0, 1)) = 0.25
        _GlowIntensity  ("Glow Intensity", Range(1, 5))  = 1.5
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+1" }

        // ── Pass 1: Render outline by expanding silhouette ──────────────────
        Pass
        {
            Name "Outline"

            // Render back faces expanded outward
            Cull Front
            ZWrite On
            ZTest LEqual
            ColorMask RGB

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            fixed4 _OutlineColor;
            float  _OutlineWidth;
            float  _PulseSpeed;
            float  _PulseIntensity;
            float  _GlowIntensity;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float4 clipPos = UnityObjectToClipPos(v.vertex);
                float3 viewNormal = normalize(mul((float3x3)UNITY_MATRIX_IT_MV, v.normal));

                // Expand in screen space for a solid, smooth outline
                float2 screenNormal = normalize(float2(
                    viewNormal.x * UNITY_MATRIX_P[0][0],
                    viewNormal.y * UNITY_MATRIX_P[1][1]
                ));

                // ── Pulse modulation ────────────────────────────────────────────
                // Sine-wave breathing effect on the outline width while hovering
                float pulse = 1.0 + _PulseIntensity * sin(_Time.y * _PulseSpeed);
                float width = _OutlineWidth * pulse;

                float2 ndcOffset = screenNormal * (width / _ScreenParams.xy);
                clipPos.xy += ndcOffset * clipPos.w;

                o.pos = clipPos;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                // Apply glow intensity to make the outline feel luminous
                return fixed4(_OutlineColor.rgb * _GlowIntensity, 1.0);
            }
            ENDCG
        }
    }

    FallBack Off
}
