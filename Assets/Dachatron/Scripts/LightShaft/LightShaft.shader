Shader "Dachatron/LightShaft"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.95, 0.8, 1)
        _Intensity ("Intensity", Range(0, 5)) = 0.5
        _EdgeSoftness ("Side Edge Softness", Range(0.1, 8)) = 2
        _StartFade ("Start Fade (0-1 of length)", Range(0, 1)) = 0.1
        _EndFade ("End Fade (0-1 of length)", Range(0, 1)) = 0.6
        _DepthFade ("Depth Fade Distance", Range(0.01, 5)) = 0.5
        _RayStrength ("Ray Streak Strength", Range(0, 1)) = 0.6
        _RayScale ("Ray Streak Scale", Range(1, 100)) = 25
        _RaySpeed ("Ray Streak Speed", Range(0, 2)) = 0.15
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "LightShaft"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                half _EdgeSoftness;
                half _StartFade;
                half _EndFade;
                half _DepthFade;
                half _RayStrength;
                half _RayScale;
                half _RaySpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Hash(float n) { return frac(sin(n) * 43758.5453); }

            float Noise1D(float x)
            {
                float i = floor(x);
                float f = frac(x);
                f = f * f * (3 - 2 * f);
                return lerp(Hash(i), Hash(i + 1), f);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(positionWS);
                OUT.positionOS = IN.positionOS.xyz;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewDirWS = GetWorldSpaceViewDir(positionWS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                // Shaft runs along local Z: z01 = 0 at the start (hole), 1 at the end.
                float z01 = saturate(IN.positionOS.z + 0.5);
                half startA = _StartFade > 0.0001 ? smoothstep(0, _StartFade, z01) : 1;
                half endA = _EndFade > 0.0001 ? 1 - smoothstep(1 - _EndFade, 1, z01) : 1;

                // Soft sides: faces seen edge-on fade out, faces seen head-on stay bright.
                float3 n = normalize(IN.normalWS);
                float3 v = normalize(IN.viewDirWS);
                half side = pow(saturate(abs(dot(n, v))), _EdgeSoftness);

                // Streaks that run along the shaft and drift slowly sideways.
                float u = (IN.positionOS.x + IN.positionOS.y) * _RayScale;
                float t = _Time.y * _RaySpeed * _RayScale;
                float streak = Noise1D(u + t) * 0.6 + Noise1D(u * 2.3 - t * 0.7) * 0.4;
                half rays = lerp(1, streak * 1.6, _RayStrength);

                // Soft intersection with scene geometry.
                float2 screenUV = IN.positionCS.xy / _ScaledScreenParams.xy;
                float sceneEye = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                half depthA = saturate((sceneEye - IN.positionCS.w) / _DepthFade);

                half a = startA * endA * side * rays * depthA * _Intensity * _Color.a;
                return half4(_Color.rgb, a);
            }
            ENDHLSL
        }
    }
}
