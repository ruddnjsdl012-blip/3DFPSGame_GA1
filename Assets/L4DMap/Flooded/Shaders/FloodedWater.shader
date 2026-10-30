// Flooded Grounds의 물(FG_PBR_Water)을 URP에서 그리기 위한 반투명 물 셰이더.
// 월드 좌표로 노멀 맵 두 장을 서로 다른 방향으로 흘려 잔물결을 만든다.
// 이 셰이더에 컴파일 오류가 있으면 맵 만들기가 자동으로 URP/Lit(반투명)으로 바꿔 쓴다.
Shader "L4D/Flooded Water"
{
    Properties
    {
        _ShallowColor ("Shallow Color", Color) = (0.22, 0.27, 0.24, 0.72)
        _DeepColor ("Deep Color", Color) = (0.07, 0.10, 0.09, 0.92)
        [Normal] _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalScale ("Normal Scale", Range(0, 2)) = 0.6
        _Tiling ("World Tiling", Float) = 0.06
        _Speed ("Flow Speed", Float) = 0.02
        _Smoothness ("Smoothness", Range(0, 1)) = 0.93
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 4
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile _ _FORWARD_PLUS _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog

            // 반투명이므로 화면 공간 그림자 대신 일반 그림자 맵을 쓴다.
            #define _SURFACE_TYPE_TRANSPARENT 1
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                float4 _NormalMap_ST;
                half _NormalScale;
                float _Tiling;
                float _Speed;
                half _Smoothness;
                half _FresnelPower;
            CBUFFER_END

            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float fogFactor : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float t = _Time.y * _Speed;
                float2 uv = input.positionWS.xz * _Tiling;
                half3 n1 = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv + float2(t, t * 0.6)), _NormalScale);
                half3 n2 = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv * 1.7 + float2(-t * 0.8, t * 0.35)), _NormalScale);
                half3 nTS = normalize(half3(n1.xy + n2.xy, n1.z * n2.z));
                // 수평면: 탄젠트 = +X, 바이탄젠트 = +Z, 노멀 = +Y
                float3 normalWS = normalize(float3(nTS.x, nTS.z, nTS.y));

                float3 viewDir = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half fresnel = pow(1.0 - saturate(dot(normalWS, viewDir)), _FresnelPower);
                half4 baseColor = lerp(_ShallowColor, _DeepColor, fresnel);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = viewDir;
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = input.fogFactor;
            #if defined(_ADDITIONAL_LIGHTS_VERTEX)
                inputData.vertexLighting = VertexLighting(input.positionWS, normalWS);
            #endif
                inputData.bakedGI = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = baseColor.rgb;
                surface.alpha = saturate(baseColor.a + fresnel * 0.25);
                surface.metallic = 0;
                surface.smoothness = _Smoothness;
                surface.normalTS = half3(0, 0, 1);
                surface.occlusion = 1;

                half4 color = UniversalFragmentPBR(inputData, surface);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = surface.alpha;
                return color;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
