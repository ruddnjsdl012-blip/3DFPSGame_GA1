// Flooded Grounds 에셋의 FG_PBR_TopBlend / FG_Triplanar_BumpSpec 머티리얼을 URP에서 그리기 위한 셰이더.
// (원본 셰이더는 Built-in 전용이라 URP에서는 분홍색으로 보인다)
//  - 바탕 텍스처 + 위를 향한 면에 이끼(Top) 텍스처를 섞는다.
//  - _Triplanar = 1 이면 UV 대신 월드 좌표로 텍스처를 입힌다. (UV가 없는 바위/절벽용)
// Flooded 맵 만들기(FloodedMapBuilder)가 원본 머티리얼의 텍스처를 옮겨 담는다.
// 이 셰이더에 컴파일 오류가 있으면 맵 만들기가 자동으로 URP/Lit으로 바꿔 쓴다.
Shader "L4D/Flooded TopBlend"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Float) = 1
        _SmoothMap ("Smoothness Map (A)", 2D) = "white" {}
        _Smoothness ("Smoothness", Range(0,1)) = 0.3
        _SmoothFromAlbedo ("Smoothness From Base Alpha", Float) = 0
        _Metallic ("Metallic", Range(0,1)) = 0
        _TopMap ("Top Map", 2D) = "white" {}
        _TopColor ("Top Color", Color) = (1,1,1,1)
        [Normal] _TopBumpMap ("Top Normal Map", 2D) = "bump" {}
        _TopAmount ("Top Amount", Range(0,1)) = 0
        _TopThreshold ("Top Threshold", Range(-1,1)) = 0.35
        _TopSharpness ("Top Sharpness", Range(1,32)) = 6
        _TopSmoothness ("Top Smoothness", Range(0,1)) = 0.15
        _Triplanar ("Triplanar", Float) = 0
        _TriplanarScale ("Triplanar Scale", Float) = 0.25
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
        _AlphaClip ("Alpha Clip", Float) = 0
        _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            float4 _BumpMap_ST;
            half _BumpScale;
            float4 _SmoothMap_ST;
            half _Smoothness;
            half _SmoothFromAlbedo;
            half _Metallic;
            float4 _TopMap_ST;
            half4 _TopColor;
            float4 _TopBumpMap_ST;
            half _TopAmount;
            half _TopThreshold;
            half _TopSharpness;
            half _TopSmoothness;
            half _Triplanar;
            float _TriplanarScale;
            half _Cutoff;
            half _AlphaClip;
            half _Cull;
        CBUFFER_END

        TEXTURE2D(_BaseMap);    SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap);    SAMPLER(sampler_BumpMap);
        TEXTURE2D(_SmoothMap);  SAMPLER(sampler_SmoothMap);
        TEXTURE2D(_TopMap);     SAMPLER(sampler_TopMap);
        TEXTURE2D(_TopBumpMap); SAMPLER(sampler_TopBumpMap);

        // UV 또는 월드 좌표(삼면 투영)로 바탕 텍스처를 읽는다.
        half4 SampleBase(float2 uv, float3 positionWS, float3 normalWS)
        {
            if (_Triplanar > 0.5)
            {
                float3 w = pow(abs(normalWS), 4.0);
                w /= max(w.x + w.y + w.z, 1e-4);
                float3 p = positionWS * _TriplanarScale;
                half4 cx = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, p.zy);
                half4 cy = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, p.xz);
                half4 cz = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, p.xy);
                return cx * w.x + cy * w.y + cz * w.z;
            }
            return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
        }

        half BaseAlpha(float2 uv)
        {
            return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv * _BaseMap_ST.xy + _BaseMap_ST.zw).a * _BaseColor.a;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile _ _FORWARD_PLUS _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 tangentWS : TEXCOORD3;
                float fogFactor : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = normals.normalWS;
                real tangentSign = input.tangentOS.w * GetOddNegativeScale();
                output.tangentWS = float4(normals.tangentWS, tangentSign);
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input, FRONT_FACE_TYPE isFrontFace : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 geomNormal = normalize(input.normalWS) * IS_FRONT_VFACE(isFrontFace, 1.0, -1.0);
                float2 uvBase = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                half4 baseColor = SampleBase(uvBase, input.positionWS, geomNormal) * _BaseColor;
                if (_AlphaClip > 0.5)
                {
                    clip(baseColor.a - _Cutoff);
                }

                // 노멀 맵 (삼면 투영일 때는 메시 노멀만 쓴다)
                float3 normalWS = geomNormal;
                float3 tangentWS = SafeNormalize(input.tangentWS.xyz);
                float3 bitangentWS = cross(geomNormal, tangentWS) * input.tangentWS.w;
                float3x3 tbn = float3x3(tangentWS, bitangentWS, geomNormal);
                if (_Triplanar < 0.5)
                {
                    float2 uvBump = input.uv * _BumpMap_ST.xy + _BumpMap_ST.zw;
                    half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvBump), _BumpScale);
                    normalWS = normalize(TransformTangentToWorld(normalTS, tbn));
                }

                // 거칠기: 별도 맵의 A 채널 또는 바탕 텍스처의 A 채널
                half smoothMask = SAMPLE_TEXTURE2D(_SmoothMap, sampler_SmoothMap, input.uv * _SmoothMap_ST.xy + _SmoothMap_ST.zw).a;
                half smoothness = (_SmoothFromAlbedo > 0.5 ? baseColor.a : smoothMask) * _Smoothness;
                half3 albedo = baseColor.rgb;

                // 위를 향한 면에 이끼(Top)를 덮는다.
                if (_TopAmount > 0.001)
                {
                    float2 uvTop = (_Triplanar > 0.5 ? input.positionWS.xz * _TriplanarScale : input.uv) * _TopMap_ST.xy + _TopMap_ST.zw;
                    half4 topColor = SAMPLE_TEXTURE2D(_TopMap, sampler_TopMap, uvTop) * _TopColor;
                    half3 topNormalTS = UnpackNormal(SAMPLE_TEXTURE2D(_TopBumpMap, sampler_TopBumpMap, uvTop));
                    half mask = saturate((normalWS.y - _TopThreshold) * _TopSharpness) * _TopAmount;
                    albedo = lerp(albedo, topColor.rgb, mask);
                    smoothness = lerp(smoothness, _TopSmoothness, mask);
                    if (_Triplanar < 0.5)
                    {
                        float3 topNormalWS = normalize(TransformTangentToWorld(topNormalTS, tbn));
                        normalWS = normalize(lerp(normalWS, topNormalWS, mask));
                    }
                }

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
            #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                inputData.shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
            #else
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
            #endif
                inputData.fogCoord = input.fogFactor;
            #if defined(_ADDITIONAL_LIGHTS_VERTEX)
                inputData.vertexLighting = VertexLighting(input.positionWS, normalWS);
            #else
                inputData.vertexLighting = half3(0, 0, 0);
            #endif
                inputData.bakedGI = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo;
                surface.alpha = 1;
                surface.metallic = _Metallic;
                surface.specular = half3(0, 0, 0);
                surface.smoothness = smoothness;
                surface.normalTS = half3(0, 0, 1);
                surface.occlusion = 1;
                surface.emission = half3(0, 0, 0);

                half4 color = UniversalFragmentPBR(inputData, surface);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = 1;
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            ShadowVaryings ShadowVert(ShadowAttributes input)
            {
                ShadowVaryings output = (ShadowVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                output.positionCS = positionCS;
                output.uv = input.uv;
                return output;
            }

            half4 ShadowFrag(ShadowVaryings input) : SV_Target
            {
                if (_AlphaClip > 0.5)
                {
                    clip(BaseAlpha(input.uv) - _Cutoff);
                }
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            DepthVaryings DepthVert(DepthAttributes input)
            {
                DepthVaryings output = (DepthVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half DepthFrag(DepthVaryings input) : SV_Target
            {
                if (_AlphaClip > 0.5)
                {
                    clip(BaseAlpha(input.uv) - _Cutoff);
                }
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex NormalsVert
            #pragma fragment NormalsFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

            struct NormalsAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct NormalsVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            NormalsVaryings NormalsVert(NormalsAttributes input)
            {
                NormalsVaryings output = (NormalsVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                return output;
            }

            half4 NormalsFrag(NormalsVaryings input) : SV_Target
            {
                if (_AlphaClip > 0.5)
                {
                    clip(BaseAlpha(input.uv) - _Cutoff);
                }
                float3 normalWS = normalize(input.normalWS);
            #if defined(_GBUFFER_NORMALS_OCT)
                float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
                float2 remapped = saturate(octNormalWS * 0.5 + 0.5);
                return half4(PackFloat2To888(remapped), 0.0);
            #else
                return half4(normalWS, 0.0);
            #endif
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
