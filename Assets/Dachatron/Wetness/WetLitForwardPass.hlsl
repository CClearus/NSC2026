#ifndef UNIVERSAL_FORWARD_LIT_PASS_INCLUDED
#define UNIVERSAL_FORWARD_LIT_PASS_INCLUDED

#include "WetLitInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

#if defined(_PARALLAXMAP)
#define REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR
#endif

#if (defined(_NORMALMAP) || (defined(_PARALLAXMAP) && !defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR))) || defined(_DETAIL)
#define REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR
#endif

// keep this file in sync with LitGBufferPass.hlsl

struct Attributes
{
    float4 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
    float4 tangentOS    : TANGENT;
    float2 texcoord     : TEXCOORD0;
    float2 staticLightmapUV   : TEXCOORD1;
    float2 dynamicLightmapUV  : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float2 uv                       : TEXCOORD0;

#if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
    float3 positionWS               : TEXCOORD1;
#endif

    float3 normalWS                 : TEXCOORD2;
#if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR)
    half4 tangentWS                : TEXCOORD3;    // xyz: tangent, w: sign
#endif

#ifdef _ADDITIONAL_LIGHTS_VERTEX
    half4 fogFactorAndVertexLight   : TEXCOORD5; // x: fogFactor, yzw: vertex light
#else
    half  fogFactor                 : TEXCOORD5;
#endif

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    float4 shadowCoord              : TEXCOORD6;
#endif

#if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
    half3 viewDirTS                : TEXCOORD7;
#endif

    DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 8);
#ifdef DYNAMICLIGHTMAP_ON
    float2  dynamicLightmapUV : TEXCOORD9; // Dynamic lightmap UVs
#endif

#ifdef USE_APV_PROBE_OCCLUSION
    float4 probeOcclusion : TEXCOORD10;
#endif

    float4 positionCS               : SV_POSITION;

    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

void InitializeInputData(Varyings input, half3 normalTS, out InputData inputData)
{
    inputData = (InputData)0;

#if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
    inputData.positionWS = input.positionWS;
#endif

#if defined(DEBUG_DISPLAY)
    inputData.positionCS = input.positionCS;
#endif

    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
#if defined(_NORMALMAP) || defined(_DETAIL)
    float sgn = input.tangentWS.w;      // should be either +1 or -1
    float3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
    half3x3 tangentToWorld = half3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz);

    #if defined(_NORMALMAP)
    inputData.tangentToWorld = tangentToWorld;
    #endif
    inputData.normalWS = TransformTangentToWorld(normalTS, tangentToWorld);
#else
    inputData.normalWS = input.normalWS;
#endif

    inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);
    inputData.viewDirectionWS = viewDirWS;

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    inputData.shadowCoord = input.shadowCoord;
#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
#else
    inputData.shadowCoord = float4(0, 0, 0, 0);
#endif
#ifdef _ADDITIONAL_LIGHTS_VERTEX
    inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactorAndVertexLight.x);
    inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
#else
    inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
#endif
                           
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);  

    #if defined(DEBUG_DISPLAY)
    #if defined(DYNAMICLIGHTMAP_ON)
    inputData.dynamicLightmapUV = input.dynamicLightmapUV;
    #endif
    #if defined(LIGHTMAP_ON)
    inputData.staticLightmapUV = input.staticLightmapUV;
    #else
    inputData.vertexSH = input.vertexSH;
    #endif
    #if defined(USE_APV_PROBE_OCCLUSION)
    inputData.probeOcclusion = input.probeOcclusion;
    #endif
    #endif
}

void InitializeBakedGIData(Varyings input, inout InputData inputData)
{
    #if defined(_SCREEN_SPACE_IRRADIANCE)
    inputData.bakedGI = SAMPLE_GI(_ScreenSpaceIrradiance, input.positionCS.xy, inputData.normalWS);
    #elif defined(DYNAMICLIGHTMAP_ON)
    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.dynamicLightmapUV, input.vertexSH, inputData.normalWS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
    #elif !defined(LIGHTMAP_ON) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
    inputData.bakedGI = SAMPLE_GI(input.vertexSH,
        GetAbsolutePositionWS(inputData.positionWS),
        inputData.normalWS,
        inputData.viewDirectionWS,
        input.positionCS.xy,
        input.probeOcclusion,
        inputData.shadowMask);
    #else
    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, inputData.normalWS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
    #endif
}

///////////////////////////////////////////////////////////////////////////////
//                  Vertex and Fragment functions                            //
///////////////////////////////////////////////////////////////////////////////

// Used in Standard (Physically Based) shader
Varyings LitPassVertex(Attributes input)
{
    Varyings output = (Varyings)0;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);

    // normalWS and tangentWS already normalize.
    // this is required to avoid skewing the direction during interpolation
    // also required for per-vertex lighting and SH evaluation
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

    half3 vertexLight = VertexLighting(vertexInput.positionWS, normalInput.normalWS);

    half fogFactor = 0;
    #if !defined(_FOG_FRAGMENT)
        fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
    #endif

    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);

    // already normalized from normal transform to WS.
    output.normalWS = normalInput.normalWS;
#if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR) || defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
    real sign = input.tangentOS.w * GetOddNegativeScale();
    half4 tangentWS = half4(normalInput.tangentWS.xyz, sign);
#endif
#if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR)
    output.tangentWS = tangentWS;
#endif

#if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(vertexInput.positionWS);
    half3 viewDirTS = GetViewDirectionTangentSpace(tangentWS, output.normalWS, viewDirWS);
    output.viewDirTS = viewDirTS;
#endif

    OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
#ifdef DYNAMICLIGHTMAP_ON
    output.dynamicLightmapUV = input.dynamicLightmapUV.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
#endif
    OUTPUT_SH4(vertexInput.positionWS, output.normalWS.xyz, GetWorldSpaceNormalizeViewDir(vertexInput.positionWS), output.vertexSH, output.probeOcclusion);
#ifdef _ADDITIONAL_LIGHTS_VERTEX
    output.fogFactorAndVertexLight = half4(fogFactor, vertexLight);
#else
    output.fogFactor = fogFactor;
#endif

#if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
    output.positionWS = vertexInput.positionWS;
#endif

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    output.shadowCoord = GetShadowCoord(vertexInput);
#endif

    output.positionCS = vertexInput.positionCS;

    return output;
}

// Used in Standard (Physically Based) shader

// ---------------------------------------------------------------- Wetness
float2 WetHash22(float2 p)
{
    p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
    return -1.0 + 2.0 * frac(sin(p) * 43758.5453123);
}

// Smooth gradient (Perlin-style) noise with a quintic fade: no grid-aligned blockiness. Returns ~0..1.
float WetGradNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
    float a = dot(WetHash22(i), f);
    float b = dot(WetHash22(i + float2(1, 0)), f - float2(1, 0));
    float c = dot(WetHash22(i + float2(0, 1)), f - float2(0, 1));
    float d = dot(WetHash22(i + float2(1, 1)), f - float2(1, 1));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y) * 0.7 + 0.5;
}

float WetFbm(float2 p)
{
    float v = 0.0;
    float amp = 1.0;
    float total = 0.0;
    int layers = (int)round(_WetNoiseOctaves);
    [loop] for (int k = 0; k < 6; k++)
    {
        if (k >= layers) break;
        v += amp * WetGradNoise(p);
        total += amp;
        p = p * _WetNoiseLacunarity + 17.3;
        amp *= _WetNoiseGain;
    }
    return v / total;
}

// Final 0..1 value that decides where puddles are. High = wet first.
float WetPuddleNoise(float2 noiseUV)
{
    float2 p = noiseUV * _WetNoiseScale;

    // Domain warp bends the edges so shapes look poured instead of blobby.
    float2 warp = float2(WetGradNoise(p * 0.7 + 13.1), WetGradNoise(p * 0.7 + 71.7)) - 0.5;
    p += warp * _WetNoiseWarp * 2.0;

    float n = WetFbm(p);

    // Optional tiling noise texture, blended in.
    float tex = SAMPLE_TEXTURE2D(_WetNoiseTex, sampler_WetNoiseTex, p * _WetNoiseTex_ST.xy + _WetNoiseTex_ST.zw).r;
    n = lerp(n, tex, _WetNoiseTexStrength);

    // Fine layer breaks the edges up into small droplets.
    float fine = WetGradNoise(noiseUV * _WetDetailScale * _WetNoiseScale + 5.2);
    n += (fine - 0.5) * _WetDetailAmount;

    return saturate((n - 0.5) * _WetNoiseContrast + 0.5);
}

// 0..1 wetness from the placed Wet Zones (unit box / unit sphere in each zone's local space).
float WetZoneMask(float3 positionWS, float n)
{
    float m = 0.0;
    int count = (int)_WetZoneCount;
    for (int i = 0; i < count && i < WET_MAX_ZONES; i++)
    {
        float3 p = mul(_WetZoneWorldToLocal[i], float4(positionWS, 1.0)).xyz;
        float4 prm = _WetZoneParams[i]; // x: 0 box / 1 ellipsoid, y: softness, z: strength, w: edge noise
        float d = prm.x < 0.5 ? max(abs(p.x), max(abs(p.y), abs(p.z))) * 2.0 : length(p) * 2.0;
        float inside = 1.0 - d + (n - 0.5) * prm.w;
        m = max(m, saturate(inside / max(prm.y, 0.001)) * prm.z);
    }
    return m;
}

float ApplyWetness(float2 uv, float3 positionWS, float3 normalWS, inout SurfaceData surfaceData)
{
    // Floors use world XZ, walls use (horizontal, height) so patches don't streak.
    float2 noiseUV = abs(normalWS.y) > 0.5 ? positionWS.xz : float2(positionWS.x + positionWS.z, positionWS.y);
    float n = WetPuddleNoise(noiseUV);

    // Coverage: _Wetness 0 -> nothing wet, 1 -> everything wet.
    float t = lerp(1.0 + _WetSoftness, -_WetSoftness, _Wetness);
    float wet = smoothstep(t - _WetSoftness, t + _WetSoftness, n);

    // Optional painted mask (UV space) replaces the procedural patches.
    float mask = SAMPLE_TEXTURE2D(_WetMaskMap, sampler_WetMaskMap, uv * _WetMaskMap_ST.xy + _WetMaskMap_ST.zw).r;
    wet = lerp(wet, mask * saturate(_Wetness * 2.0), _WetMaskStrength);

    wet = lerp(wet, WetZoneMask(positionWS, n), _WetZoneMode);

    // Puddles only gather on surfaces that face up.
    float upMask = smoothstep(_WetUpFacingMin, 1.0, saturate(normalWS.y));
    wet *= lerp(1.0, upMask, _WetUpFacingOnly);
    wet *= 1.0 - saturate(_GlobalDryness);

    surfaceData.albedo *= lerp(1.0, 1.0 - _WetDarken, wet);
    surfaceData.smoothness = lerp(surfaceData.smoothness, _WetSmoothness, wet);
    surfaceData.metallic = lerp(surfaceData.metallic, 0.0, wet * 0.5);
    surfaceData.normalTS = normalize(lerp(surfaceData.normalTS, half3(0, 0, 1), wet * _WetFlatten));
    return wet;
}

// Live mirror reflection, only where the surface is wet, faces up and sits on the reflected floor plane.
half3 ApplyPlanarReflection(half3 color, float wet, float3 positionWS, float3 normalWS, float4 positionCS)
{
    if (_PlanarReflectionReady < 0.5) return color;

    float onPlane = saturate(1.0 - abs(positionWS.y - _PlanarReflectionHeight) * 10.0) * saturate((normalWS.y - 0.8) * 5.0);
    float a = wet * onPlane * _WetPlanarStrength;
    if (a <= 0.001) return color;

    float2 rp = positionWS.xz * 6.0;
    float tt = _Time.y * 0.5;
    float2 ripple = float2(WetGradNoise(rp + tt), WetGradNoise(rp * 1.3 - tt + 7.1)) - 0.5;

    // the reflection camera renders mirrored left/right, so flip x back
    float2 suv = GetNormalizedScreenSpaceUV(positionCS);
    float2 ruv = float2(1.0 - suv.x, _WetPlanarFlipY > 0.5 ? 1.0 - suv.y : suv.y) + ripple * _WetPlanarRipple;
    half3 refl = SAMPLE_TEXTURE2D(_PlanarReflectionTex, sampler_PlanarReflectionTex, ruv).rgb;

    float3 V = GetWorldSpaceNormalizeViewDir(positionWS);
    float fres = lerp(_WetPlanarMin, 1.0, pow(1.0 - saturate(dot(V, normalWS)), _WetPlanarFresnel));
    return lerp(color, refl, saturate(a * fres));
}

void LitPassFragment(
    Varyings input
    , out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

#if defined(_PARALLAXMAP)
#if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
    half3 viewDirTS = input.viewDirTS;
#else
    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    half3 viewDirTS = GetViewDirectionTangentSpace(input.tangentWS, input.normalWS, viewDirWS);
#endif
    ApplyPerPixelDisplacement(viewDirTS, input.uv);
#endif

    SurfaceData surfaceData;
    InitializeStandardLitSurfaceData(input.uv, surfaceData);
    float wetMask = ApplyWetness(input.uv, input.positionWS, input.normalWS, surfaceData);

#ifdef LOD_FADE_CROSSFADE
    LODFadeCrossFade(input.positionCS);
#endif

    InputData inputData;
    InitializeInputData(input, surfaceData.normalTS, inputData);
    SETUP_DEBUG_TEXTURE_DATA(inputData, UNDO_TRANSFORM_TEX(input.uv, _BaseMap));

#if defined(_DBUFFER)
    ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
#endif

    InitializeBakedGIData(input, inputData);

    half4 color = UniversalFragmentPBR(inputData, surfaceData);
    color.rgb = ApplyPlanarReflection(color.rgb, wetMask, input.positionWS, inputData.normalWS, input.positionCS);
    color.rgb = MixFog(color.rgb, inputData.fogCoord);
    color.a = OutputAlpha(color.a, IsSurfaceTypeTransparent());

    outColor = color;

#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = EncodeMeshRenderingLayer();
#endif
}

#endif
