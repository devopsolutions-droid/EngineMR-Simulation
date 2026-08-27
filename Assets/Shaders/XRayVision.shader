Shader "Custom/XRayVision"
{
    Properties
    {
        // ── Core ─────────────────────────────────────────────────────────────
        _RimColor           ("Rim Color",               Color)          = (0.0, 0.85, 1.0, 1.0)
        _DepthNearColor     ("Depth Near Color",        Color)          = (1.0, 0.55, 0.1, 1.0)
        _DepthFarColor      ("Depth Far Color",         Color)          = (0.0, 0.4,  1.0, 1.0)
        _RimPower           ("Rim Power",               Range(1, 8))    = 3.5
        _RimIntensity       ("Rim Intensity",           Range(0, 3))    = 1.8
        _BodyAlpha          ("Body Alpha",              Range(0, 0.4))  = 0.08

        // ── Depth coloring ────────────────────────────────────────────────────
        _DepthRange         ("Depth Range",             Range(0.5, 20)) = 6.0
        _DepthInfluence     ("Depth Color Influence",   Range(0, 1))    = 0.7

        // ── Scan lines ────────────────────────────────────────────────────────
        _ScanSpeed          ("Scan Speed",              Range(0, 4))    = 1.2
        _ScanDensity        ("Scan Density",            Range(5, 120))  = 35.0
        _ScanLineWidth      ("Scan Line Width",         Range(0.01,0.95)) = 0.25
        _ScanBrightness     ("Scan Brightness",         Range(0, 1))    = 0.55
        _ScanAlpha          ("Scan Alpha Boost",        Range(0, 0.3))  = 0.12

        // ── Circuit pulse lines ───────────────────────────────────────────────
        _CircuitColor       ("Circuit Color",           Color)          = (0.2, 1.0, 0.8, 1.0)
        _CircuitSpeed       ("Circuit Speed",           Range(0, 6))    = 2.5
        _CircuitDensity     ("Circuit Density",         Range(1, 40))   = 12.0
        _CircuitWidth       ("Circuit Line Width",      Range(0.01,0.3))= 0.06
        _CircuitIntensity   ("Circuit Intensity",       Range(0, 2))    = 1.2

        // ── Sweep entry effect ────────────────────────────────────────────────
        _SweepY             ("Sweep Y Position",        Float)          = -99.0
        _SweepWidth         ("Sweep Band Width",        Range(0.01, 2)) = 0.4
        _SweepIntensity     ("Sweep Intensity",         Range(0, 5))    = 3.5
        _SweepProgress      ("Sweep Progress (0-1)",    Range(0, 1))    = 0.0

        // ── Glitch flicker ────────────────────────────────────────────────────
        _GlitchIntensity    ("Glitch Intensity",        Range(0, 1))    = 0.0

        // ── Hover highlight ───────────────────────────────────────────────────
        _HoverIntensity     ("Hover Intensity",         Range(0, 1))    = 0.0

        // ── Outline ───────────────────────────────────────────────────────────
        _OutlineWidth       ("Outline Width (px)",      Float)          = 1.2

        // ── Per-part tint ──────────────────────────────────────────────────────
        _PartTint           ("Part Tint",               Color)          = (1, 1, 1, 1)
        _PartIntensity      ("Part Glow Intensity",     Range(0, 4))    = 1.0
        _PartAlpha          ("Part Body Alpha",         Range(0, 1))    = 1.0
    }

    SubShader
    {
        Tags { "Queue"="Transparent+100" "RenderType"="Transparent" "IgnoreProjector"="True" }
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        // ── Pass 0 : Body ─────────────────────────────────────────────────────
        Pass
        {
            Name "XRayBody"
            Cull Back

            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            fixed4 _RimColor;
            fixed4 _DepthNearColor;
            fixed4 _DepthFarColor;
            fixed4 _PartTint;
            float  _PartIntensity;
            float  _PartAlpha;
            float  _RimPower;
            float  _RimIntensity;
            float  _BodyAlpha;
            float  _DepthRange;
            float  _DepthInfluence;

            float  _ScanSpeed;
            float  _ScanDensity;
            float  _ScanLineWidth;
            float  _ScanBrightness;
            float  _ScanAlpha;

            fixed4 _CircuitColor;
            float  _CircuitSpeed;
            float  _CircuitDensity;
            float  _CircuitWidth;
            float  _CircuitIntensity;

            float  _SweepY;
            float  _SweepWidth;
            float  _SweepIntensity;
            float  _SweepProgress;

            float  _GlitchIntensity;
            float  _HoverIntensity;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos      : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 viewDir  : TEXCOORD1;
                float3 normal   : TEXCOORD2;
                float  depth    : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ── Simple hash for noise/glitch ──────────────────────────────────
            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos      = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normal   = UnityObjectToWorldNormal(v.normal);
                o.viewDir  = normalize(_WorldSpaceCameraPos - o.worldPos);
                // Camera-space depth for depth coloring
                o.depth    = length(_WorldSpaceCameraPos - o.worldPos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float3 N    = normalize(i.normal);
                float3 V    = normalize(i.viewDir);
                float  NdotV = saturate(dot(N, V));

                // ── 1. Fresnel rim ────────────────────────────────────────────
                float rim     = pow(1.0 - NdotV, _RimPower);
                float rimGlow = rim * _RimIntensity * _PartIntensity;    // Option 2: per-part glow

                // ── 2. Depth-based color ──────────────────────────────────────
                float depthT     = saturate(i.depth / _DepthRange);
                fixed3 depthCol  = lerp(_DepthNearColor.rgb, _DepthFarColor.rgb, depthT);
                fixed3 baseColor = lerp(_RimColor.rgb, depthCol, _DepthInfluence);

                // ── 3. Scan lines ─────────────────────────────────────────────
                float scrollY  = i.worldPos.y * _ScanDensity - _Time.y * _ScanSpeed;
                float band     = frac(scrollY);
                float scanLine = step(_ScanLineWidth, band);
                float scanBright = scanLine * _ScanBrightness;

                // ── 4. Circuit pulse lines (world-space XZ grid) ──────────────
                // Horizontal pulses on X axis
                float cx       = frac(i.worldPos.x * _CircuitDensity - _Time.y * _CircuitSpeed);
                float circuitX = 1.0 - smoothstep(0.0, _CircuitWidth, abs(cx - 0.5));
                // Vertical pulses on Z axis  
                float cz       = frac(i.worldPos.z * _CircuitDensity - _Time.y * _CircuitSpeed * 0.7);
                float circuitZ = 1.0 - smoothstep(0.0, _CircuitWidth, abs(cz - 0.5));
                // Combine — only show one at a time based on which is stronger
                float circuit  = max(circuitX, circuitZ) * _CircuitIntensity;
                // Animate pulse brightness with a travelling wave
                float pulse    = saturate(sin(i.worldPos.x * 3.0 + i.worldPos.z * 2.0 - _Time.y * _CircuitSpeed * 2.0) * 0.5 + 0.5);
                circuit       *= pulse;

                // ── 5. Sweep entry band ───────────────────────────────────────
                float distToSweep = abs(i.worldPos.y - _SweepY);
                float sweepGlow   = saturate(1.0 - distToSweep / _SweepWidth) * _SweepIntensity;
                // Parts above sweep (where sweep has already passed) are revealed
                float passedSweep = step(_SweepY, i.worldPos.y);

                // ── 6. Glitch flicker ─────────────────────────────────────────
                float glitchNoise = hash(float2(floor(_Time.y * 24.0), floor(i.worldPos.y * 8.0)));
                float glitch      = lerp(1.0, glitchNoise, _GlitchIntensity);

                // ── 7. Hover highlight ────────────────────────────────────────
                float hoverRim  = pow(1.0 - NdotV, 1.5) * _HoverIntensity * 3.0;
                fixed3 hoverCol = fixed3(1.0, 1.0, 1.0);

                // ── Compose final color ───────────────────────────────────────
                fixed3 col = baseColor;

                // Add scan line brightness
                col += baseColor * scanBright;

                // Add circuit lines
                col += _CircuitColor.rgb * circuit;

                // Add rim glow (Option 2: scaled by per-part intensity)
                col += baseColor * rimGlow;

                // Add hover white-hot rim
                col += hoverCol * hoverRim;

                // Add sweep glow
                col += fixed3(0.8, 0.95, 1.0) * sweepGlow;

                // ── 8. Per-part body tint (Option 1) ─────────────────────────────
                // Multiply final color by part tint so each part has a unique hue
                col *= _PartTint.rgb;

                // ── Compose alpha ─────────────────────────────────────────────
                float alpha = _BodyAlpha * _PartAlpha;                  // Option 3: per-part alpha                  // Option 3: per-part alpha
                alpha += rim * 0.5 * _PartIntensity;   // rim is more opaque (Option 2)
                alpha += scanLine * _ScanAlpha;         // scan lines slightly more opaque
                alpha += circuit * 0.15;                // circuit lines visible
                alpha += sweepGlow * 0.6;               // sweep band very bright
                alpha += hoverRim * 0.4;                // hover boost
                alpha  = saturate(alpha);

                // ── Depth-based alpha fade (Fix C) ────────────────────────────
                // Deeper parts get slightly more transparent, creating depth hierarchy
                float depthFade = 1.0 - saturate(i.depth / _DepthRange) * 0.5f;
                alpha *= depthFade;

                // Apply glitch
                alpha *= glitch;
                col   *= glitch;

                // During entry: reveal parts behind sweep line.
                // Once sweep is done (_SweepProgress >= 1), show everything.
                float sweepMask = passedSweep + (1.0 - passedSweep) * saturate(sweepGlow);
                alpha *= lerp(sweepMask, 1.0, step(0.999, _SweepProgress));

                return fixed4(col, alpha);
            }
            ENDCG
        }

        // ── Pass 1 : Outline shell ────────────────────────────────────────────
        Pass
        {
            Name "XRayOutline"
            Cull Front

            CGPROGRAM
            #pragma vertex   vertOutline
            #pragma fragment fragOutline
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            fixed4 _RimColor;
            fixed4 _PartTint;
            float  _PartIntensity;
            float  _HoverIntensity;
            float  _OutlineWidth;
            float  _GlitchIntensity;
            float  _SweepY;
            float  _SweepProgress;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos      : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            v2f vertOutline(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float4 clipPos     = UnityObjectToClipPos(v.vertex);
                float3 viewNormal  = normalize(mul((float3x3)UNITY_MATRIX_IT_MV, v.normal));
                float2 screenNorm  = normalize(float2(
                    viewNormal.x * UNITY_MATRIX_P[0][0],
                    viewNormal.y * UNITY_MATRIX_P[1][1]));
                // Option 4: Hover outline pulse — sine-wave modulation when hovered
                float hoverPulse   = 1.0 + _HoverIntensity * 0.25 * sin(_Time.y * 4.0);
                float outlineWidth = _OutlineWidth * hoverPulse;
                clipPos.xy        += screenNorm * (outlineWidth / _ScreenParams.xy) * clipPos.w;
                o.pos              = clipPos;
                o.worldPos         = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 fragOutline(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                // Apply glitch flicker to outline alpha
                float glitchNoise = hash(float2(floor(_Time.y * 24.0), floor(i.worldPos.y * 8.0)));
                float glitch      = lerp(1.0, glitchNoise, _GlitchIntensity);

                // Hide outline until sweep line passes over.
                // Once sweep is done, show outline on everything.
                float sweepDone   = step(0.999, _SweepProgress);
                float passedSweep = step(_SweepY, i.worldPos.y);
                float alpha       = lerp(passedSweep, 1.0, sweepDone) * glitch * lerp(0.5, 1.5, _PartIntensity * 0.5f);

                // Fix B: Multiply outline color by part tint so each part gets its own outline hue
                fixed3 outlineColor = _RimColor.rgb * _PartTint.rgb;
                return fixed4(outlineColor, alpha);
            }
            ENDCG
        }
    }

    FallBack Off
}
