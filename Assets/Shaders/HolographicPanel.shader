Shader "Custom/HolographicPanel"
{
    Properties
    {
        _MainTex    ("Texture",        2D)    = "white" {}
        _Color      ("Panel Color",    Color) = (0.05, 0.08, 0.15, 0.75)
        _EdgeColor  ("Edge Glow Color",Color) = (0.0, 0.8, 1.0, 1.0)
        _EdgeWidth  ("Edge Width",     Range(0.0, 0.1)) = 0.015
        _GlowPower  ("Glow Intensity", Range(0.0, 5.0)) = 2.0
        _ScanSpeed  ("Scan Line Speed",Range(0.0, 2.0)) = 0.3
        _ScanDensity("Scan Density",   Range(0.0,100.0))= 40.0
        _ScanAlpha  ("Scan Alpha",     Range(0.0, 0.3)) = 0.08
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f    { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            sampler2D _MainTex;
            float4    _MainTex_ST;
            float4    _Color;
            float4    _EdgeColor;
            float     _EdgeWidth;
            float     _GlowPower;
            float     _ScanSpeed;
            float     _ScanDensity;
            float     _ScanAlpha;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;

                // Base panel color
                fixed4 col = _Color;

                // Edge glow — distance from each edge
                float edgeX = min(uv.x, 1.0 - uv.x);
                float edgeY = min(uv.y, 1.0 - uv.y);
                float edge  = min(edgeX, edgeY);
                float glow  = pow(saturate(1.0 - edge / _EdgeWidth), _GlowPower);
                col.rgb = lerp(col.rgb, _EdgeColor.rgb, glow * _EdgeColor.a);
                col.a   = lerp(col.a,   1.0,            glow * 0.6);

                // Subtle horizontal scan lines
                float scan = sin((uv.y + _Time.y * _ScanSpeed) * _ScanDensity) * 0.5 + 0.5;
                col.rgb += scan * _ScanAlpha;

                // Texture overlay (for card backgrounds etc.)
                fixed4 tex = tex2D(_MainTex, uv);
                col.rgb = lerp(col.rgb, tex.rgb, tex.a * 0.15);

                return col;
            }
            ENDCG
        }
    }
    FallBack "Transparent/Diffuse"
}
