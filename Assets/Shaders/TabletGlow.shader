Shader "Custom/TabletGlow"
{
    Properties
    {
        _GlowColor  ("Glow Color",   Color)  = (1, 0.78, 0.1, 1)
        _GlowPower  ("Glow Power",   Range(0, 1)) = 1.0
        _EdgeSoft   ("Edge Softness",Range(0.01, 0.5)) = 0.12
    }

    SubShader
    {
        // Render after opaque geometry, blend additively so it always glows on top
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }

        Pass
        {
            Name "TabletGlow"
            Blend One One          // Additive — pure glow, no dark fringe
            ZWrite Off
            ZTest LEqual
            Cull Off               // Visible from both sides

            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            fixed4 _GlowColor;
            float  _GlowPower;
            float  _EdgeSoft;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                // Distance from each edge in UV space (0 = edge, 0.5 = center)
                float2 edgeDist = min(i.uv, 1.0 - i.uv); // 0..0.5 from each edge

                // Remap so the glow is strongest at the edge and fades inward
                float edgeMask = 1.0 - smoothstep(0.0, _EdgeSoft, min(edgeDist.x, edgeDist.y));

                // Apply overall power (driven by the pulse script)
                float alpha = edgeMask * _GlowPower;

                return fixed4(_GlowColor.rgb * alpha, alpha);
            }
            ENDCG
        }
    }
}
