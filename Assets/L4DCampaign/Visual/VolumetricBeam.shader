// 손전등/조명 빛줄기 (가벼운 가짜 볼류메트릭)
// 원뿔 메시에 더하기(Additive)로 그린다. 옆에서 볼수록 진하고, 끝으로 갈수록 옅어지며,
// 벽/바닥에 닿는 곳과 카메라 바로 앞은 부드럽게 사라진다. (카메라 깊이 텍스처 필요)
Shader "L4D/Volumetric Beam"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.95, 0.85, 0.06)
        _Intensity ("Intensity", Float) = 1
        _EdgePower ("Edge Power", Range(0.5, 6)) = 1.6
        _LengthPower ("Length Falloff", Range(0.5, 6)) = 1.8
        _DepthFade ("Depth Fade (m)", Float) = 1.2
        _NearFade ("Camera Near Fade (m)", Float) = 1.5
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+20" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Beam"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                half _EdgePower;
                half _LengthPower;
                float _DepthFade;
                float _NearFade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float4 screenPos : TEXCOORD3;
                float eyeDepth : TEXCOORD4;
                float fogFactor : TEXCOORD5;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.screenPos = ComputeScreenPos(positions.positionCS);
                output.eyeDepth = -positions.positionVS.z;
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 viewDir = normalize(_WorldSpaceCameraPos - input.positionWS);
                // 옆에서 볼수록 진하게, 빛 방향으로 볼 때(1인칭)도 완전히 사라지지 않게 바닥값을 둔다.
                half edge = lerp(0.35, 1.0, pow(saturate(abs(dot(normalize(input.normalWS), viewDir))), _EdgePower));
                half along = pow(saturate(1.0 - input.uv.y), _LengthPower);

                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                half soft = saturate((sceneDepth - input.eyeDepth) / max(0.01, _DepthFade));
                half nearFade = saturate((input.eyeDepth - 0.2) / max(0.01, _NearFade));

                half3 color = _Color.rgb * (_Color.a * _Intensity * edge * along * soft * nearFade);
                color = MixFogColor(color, half3(0, 0, 0), input.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
