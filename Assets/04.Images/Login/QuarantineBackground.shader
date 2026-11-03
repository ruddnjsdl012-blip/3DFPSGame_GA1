Shader "Flooded/Login/Quarantine Background"
{
    Properties
    {
        [PerRendererData] _MainTex ("Background", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Intensity ("Atmosphere intensity", Range(0,2)) = 1
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            sampler2D _MainTex;
            float4 _Color;
            float _Intensity;
            v2f vert(appdata v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color*_Color; return o; }
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float noise(float2 p)
            {
                float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);
            }
            float3 sampleScene(float2 uv)
            {
                float2 fringe=(uv-.5)*.0018;
                return float3(tex2D(_MainTex,uv+fringe).r,tex2D(_MainTex,uv).g,tex2D(_MainTex,uv-fringe).b);
            }
            fixed4 frag(v2f i):SV_Target
            {
                float2 uv=i.uv;
                float t=_Time.y;
                float3 src=sampleScene(uv);
                float luma=dot(src,float3(.2126,.7152,.0722));
                float3 c=lerp(luma.xxx,src,.68)*float3(.78,.99,1.08);
                c=max(0,(c-.17)*1.24+.15);
                // Broad highlight diffusion, applied to the actual image behind overlay UI.
                float3 glow=0;
                glow+=max(0,sampleScene(uv+float2(.008,0))-.48);
                glow+=max(0,sampleScene(uv-float2(.008,0))-.48);
                glow+=max(0,sampleScene(uv+float2(0,.012))-.48);
                glow+=max(0,sampleScene(uv-float2(0,.012))-.48);
                c+=glow*.14;
                float fog=noise(uv*float2(5,3)+float2(t*.027,-t*.009));
                fog*=noise(uv*9+float2(-t*.018,t*.012));
                c+=float3(.15,.22,.23)*fog*(1-smoothstep(.05,.85,uv.y))*_Intensity;
                // Emergency beacon spills from the truck into drifting ground haze.
                float beacon=pow(saturate(sin(t*2.1)),7);
                float redPool=exp(-dot((uv-float2(.39,.44))*float2(3.8,5),(uv-float2(.39,.44))*float2(3.8,5)));
                c+=float3(.34,.018,.006)*redPool*beacon*(.3+fog)*_Intensity;
                // Three depths of slanted rain; no particle objects or per-frame allocations.
                float rain=0;
                [unroll] for(int layer=0;layer<3;layer++)
                {
                    float scale=1+layer*.63;
                    float2 p=float2(uv.x+uv.y*.16,uv.y)*float2(190,22)*scale;
                    p.y+=t*(12+layer*5);
                    float2 cell=floor(p); float2 f=frac(p);
                    float rnd=hash(cell);
                    rain+=(1-smoothstep(.015,.055,abs(f.x-.5)))*smoothstep(.0,.15,f.y)*(1-smoothstep(.4,.95,f.y))*step(.79,rnd)/scale;
                }
                c+=rain*float3(.22,.3,.34)*_Intensity;
                // Slow airborne embers with individually seeded speeds and brightness.
                float2 ashUV=uv*float2(80,44)+float2(t*.8,-t*1.6);
                float2 cell=floor(ashUV), f=frac(ashUV);
                float seed=hash(cell);
                float ember=(1-smoothstep(.015,.13,length((f-.5)*float2(1,1.5))))*step(.965,seed);
                c+=ember*float3(1,.29,.055)*( .5+.5*sin(t*3+seed*40))*_Intensity;
                float flash=pow(saturate(sin(t*.39+sin(t*.19)*2)),90)*.12;
                c+=flash*float3(.65,.78,1)*smoothstep(.1,.95,uv.y)*_Intensity;
                float vignette=1-smoothstep(.18,.85,length((uv-.5)*float2(.96,.83)));
                c*=lerp(.46,1,vignette);
                c+=(hash(uv*_ScreenParams.xy+floor(t*24))-.5)*.038*_Intensity;
                return fixed4(saturate(c)*i.color.rgb,i.color.a);
            }
            ENDCG
        }
    }
}
