// Weather radar map for a UI RawImage: grey background with range rings, rain cells coloured
// light blue -> blue -> purple -> red, and the player as a glowing red dot in the centre that flares on each beep.
// Rain drifts with the wind and evolves (_WeatherTime from WeatherMapUI, in radar frames), sampled in bins with stepped
// colour levels and hard edges like real radar. _Offset can scroll it with the player.
Shader "Dachatron/WeatherRadar"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite", 2D) = "white" {}
        _Seed("Seed", Float) = 3.7
        _MapScale("Map Size (noise units across)", Float) = 2.6
        _Coverage("Rain Coverage", Range(0, 1)) = 0.45
        _Intensity("Storm Intensity", Range(0, 2)) = 1
        _Offset("Map Offset", Vector) = (0, 0, 0, 0)
        _DotSize("Player Dot Size", Range(0.005, 0.05)) = 0.014
        _Border("Border Width", Range(0, 0.03)) = 0.006
        [HideInInspector] _PingPhase("Ping Phase", Range(0, 1)) = 1

        // motion
        _WindAngle("Wind Direction (degrees)", Range(0, 360)) = 25
        _WindSpeed("Wind Speed (map units / sec)", Float) = 0.012
        _Evolution("Storm Evolution Speed", Float) = 0.015
        _Swirl("Swirl Speed (rad / sec)", Float) = 0.01
        _CellSpeed("Core Churn Speed", Float) = 0.03
        [HideInInspector] _WeatherTime("Weather Time", Float) = 0

        // radar look
        _Warp("Shape Warp (higher = more smeared)", Range(0, 3)) = 0.6
        _EdgeGrain("Edge Grain", Range(0, 0.2)) = 0.07
        _Bins("Radar Resolution (bins across)", Float) = 240
        _Levels("Colour Levels", Float) = 9
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float _Seed, _MapScale, _Coverage, _Intensity, _DotSize, _Border, _PingPhase;
            float _WindAngle, _WindSpeed, _Evolution, _Swirl, _CellSpeed, _WeatherTime;
            float _Warp, _EdgeGrain, _Bins, _Levels;

            float2x2 Rot(float a)
            {
                float s = sin(a), c = cos(a);
                return float2x2(c, -s, s, c);
            }
            float4 _Offset;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            float2 hash22(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return -1.0 + 2.0 * frac(sin(p) * 43758.5453123);
            }

            // smooth gradient noise, ~0..1
            float gnoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
                float a = dot(hash22(i), f);
                float b = dot(hash22(i + float2(1, 0)), f - float2(1, 0));
                float c = dot(hash22(i + float2(0, 1)), f - float2(0, 1));
                float d = dot(hash22(i + float2(1, 1)), f - float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y) * 0.7 + 0.5;
            }

            float fbm(float2 p)
            {
                float v = 0.0, amp = 0.5, total = 0.0;
                for (int k = 0; k < 5; k++)
                {
                    v += amp * gnoise(p);
                    total += amp;
                    p = p * 2.03 + 17.3;
                    amp *= 0.5;
                }
                return v / total;
            }

            // radar ramp: light blue -> blue -> indigo -> purple -> red/pink core
            float3 RadarColor(float t)
            {
                float3 c0 = float3(0.72, 0.88, 1.00);
                float3 c1 = float3(0.42, 0.62, 1.00);
                float3 c2 = float3(0.45, 0.40, 0.95);
                float3 c3 = float3(0.70, 0.33, 0.82);
                float3 c4 = float3(0.98, 0.34, 0.45);
                t = saturate(t) * 4.0;
                if (t < 1.0) return lerp(c0, c1, t);
                if (t < 2.0) return lerp(c1, c2, t - 1.0);
                if (t < 3.0) return lerp(c2, c3, t - 2.0);
                return lerp(c3, c4, t - 3.0);
            }

            // plain grey with faint range rings and a crosshair through the player
            float3 Background(float2 uv)
            {
                float3 col = float3(0.17, 0.18, 0.19);
                float d = length(uv - 0.5);
                float aa = fwidth(d) * 1.2;
                float rings = 0.0;
                for (int k = 1; k <= 3; k++)
                    rings = max(rings, 1.0 - smoothstep(0.0, aa, abs(d - 0.15 * k)));
                float2 c = abs(uv - 0.5);
                float2 w = fwidth(uv) * 0.8;
                float crosshair = max(1.0 - smoothstep(0.0, w.x, c.x), 1.0 - smoothstep(0.0, w.y, c.y));
                return col + 0.06 * max(rings, crosshair * 0.6);
            }

            // x: rain intensity (0..1, drives the colour), y: how much rain shows over the terrain
            float2 Rain(float2 p)
            {
                float t = _WeatherTime;
                float windA = radians(_WindAngle);
                float2 windDir = float2(cos(windA), sin(windA));

                // the whole system is carried along by the wind
                float2 q = (p - windDir * _WindSpeed * t) * 0.9 + _Seed * 7.1;

                // domain warp so storms get ragged, poured shapes. The warp field itself drifts and slowly
                // turns, so the shapes morph and curl over time instead of just sliding.
                float2 wq = q * 0.5 + _Evolution * t * float2(0.6, -0.4);
                float2 w = float2(fbm(wq + 1.7), fbm(wq + 9.2)) - 0.5;
                w = mul(Rot(t * _Swirl), w);
                q += w * _Warp;

                // stretched along the wind, like real bands
                float2 qw = mul(Rot(-windA), q);
                float2 qs = float2(qw.x * 0.8, qw.y * 1.25);
                float big = fbm(qs + _Evolution * t * float2(0.3, 0.2));                 // storm systems grow / decay
                float cells = fbm(qs * 3.1 + 4.3 + _CellSpeed * t * float2(1.0, 0.35));  // cores churn faster
                float r = big + (cells - 0.5) * 0.35;

                // fine grain right at the threshold: crisp, broken-up edges like real returns, not smooth blobs
                r += (gnoise(q * 14.0 + _CellSpeed * t * 3.0) - 0.5) * _EdgeGrain;

                float edge = lerp(0.75, 0.35, _Coverage);
                float rain = saturate((r - edge) / (1.0 - edge) * 2.2);

                // scattered small showers around the storms
                float speck = step(0.8, gnoise(q * 9.0 + 2.0 + _CellSpeed * t * 2.0)) * smoothstep(edge - 0.12, edge - 0.02, r);
                rain = max(rain, speck * 0.15);

                // hot cores
                float core = pow(saturate((cells - 0.55) * 2.5), 1.5) * smoothstep(0.15, 0.5, rain);
                float intensity = saturate((rain * 0.9 + core * 0.8) * _Intensity);

                // radar shows fixed intensity levels, not a smooth gradient
                float levels = max(2.0, _Levels);
                intensity = ceil(intensity * levels) / levels;

                // hard edge: a bin either has a return or it doesn't
                float alpha = step(0.001, rain) * lerp(0.75, 0.92, intensity);
                return float2(intensity, alpha);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // radar data comes in bins: sample the weather once per bin so edges are crisp, slightly blocky
                float bins = max(16.0, _Bins);
                float2 binUV = (floor(i.uv * bins) + 0.5) / bins;
                float2 p = (binUV - 0.5) * _MapScale + _Offset.xy;

                float3 col = Background(i.uv);
                float2 rain = Rain(p);
                col = lerp(col, RadarColor(rain.x), rain.y);

                float d = length(i.uv - 0.5);
                float aa = fwidth(d);
                float ph = _PingPhase;   // 0 -> 1 once per ping, driven by WeatherMapUI (in sync with the beep)

                // red glow around the dot: always a little, flaring up on each beep and fading out
                float flash = pow(1.0 - ph, 3.0);
                float glowSize = _DotSize * _DotSize * lerp(6.0, 16.0, flash);
                float glow = exp(-d * d / glowSize) * (0.3 + 0.9 * flash);
                col += float3(0.95, 0.12, 0.10) * glow * 0.7;

                // player: solid red dot, a touch brighter on the beep
                float playerDot = 1.0 - smoothstep(_DotSize, _DotSize + aa, d);
                col = lerp(col, lerp(float3(0.85, 0.06, 0.06), float3(1.0, 0.25, 0.2), flash), playerDot);

                // thin frame
                float2 e = min(i.uv, 1.0 - i.uv);
                float frame = step(min(e.x, e.y), _Border);
                col = lerp(col, float3(0.75, 0.8, 0.85), frame);

                return fixed4(col, i.color.a);
            }
            ENDCG
        }
    }
}
