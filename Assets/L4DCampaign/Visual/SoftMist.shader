// 낮게 깔리는 안개 (파티클용)
// 벽/바닥과 닿는 곳은 부드럽게(소프트 파티클), 카메라 바로 앞은 사라지게 해서 화면을 가리지 않는다.
// 색은 하늘 환경광과 해/달 빛을 받아, 밤이 되면 같이 어두워진다.
Shader "L4D/Soft Mist"
{
    Properties
    {
        _MainTex ("Puff (A)", 2D) = "white" {}
        _Color ("Color", Color) = (0.8, 0.82, 0.85, 0.25)
        _LightAmount ("Light Amount", Range(0, 2)) = 1
        _DepthFade ("Depth Fade (m)", Float) = 2.5
        _NearFade ("Camera Near Fade (m)", Float) = 4
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Mist"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half _LightAmount;
                float _DepthFade;
                float _NearFade;
            CBUFFER_END

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float eyeDepth : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.color = input.color;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.screenPos = ComputeScreenPos(positions.positionCS);
                output.eyeDepth = -positions.positionVS.z;
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half puff = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                half soft = saturate((sceneDepth - input.eyeDepth) / max(0.01, _DepthFade));
                half nearFade = saturate((input.eyeDepth - 0.5) / max(0.01, _NearFade));

                Light mainLight = GetMainLight();
                half3 light = SampleSH(half3(0, 1, 0)) + mainLight.color * 0.35;
                half3 color = _Color.rgb * input.color.rgb * lerp(half3(1, 1, 1), light, _LightAmount);
                half alpha = puff * _Color.a * input.color.a * soft * nearFade;
                color = MixFog(color, input.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
