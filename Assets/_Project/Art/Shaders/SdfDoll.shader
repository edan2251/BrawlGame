// CR-004 단계 S2: 외형 후보 R3. 부위(테이퍼 캡슐) 여러 개를 거리장(SDF)으로 매끄럽게 합쳐(smooth min) 한 번에 그린다.
// 물체 좌표 = 인형 좌표(원점 = 판정 상자 중심). 채움색과 잉크 외곽선 띠, 경계는 fwidth로 부드럽게.
// 부위 수는 최대 12개: _A[i] = (a.x, a.y, b.x, b.y), _R[i] = (a 반지름, b 반지름).
Shader "Knockbox/SdfDoll"
{
    Properties
    {
        _Fill ("Fill", Color) = (1, 0.83, 0.4, 1)
        _Ink ("Ink", Color) = (0.01, 0.01, 0.02, 1)
        _Outline ("Outline", Float) = 0.05
        _Blend ("Blend", Float) = 0.06
        _Count ("Count", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Tags { "LightMode" = "Universal2D" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            half4 _Fill;
            half4 _Ink;
            float _Outline;
            float _Blend;
            float _Count;
            CBUFFER_END
            float4 _A[12];
            float4 _R[12];

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 local : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.local = v.positionOS.xy;
                return o;
            }

            float Capsule(float2 p, float2 a, float2 b, float ra, float rb)
            {
                float2 pa = p - a;
                float2 ba = b - a;
                float h = saturate(dot(pa, ba) / max(dot(ba, ba), 1e-6));
                return length(pa - ba * h) - lerp(ra, rb, h);
            }

            float SmoothMin(float a, float b, float k)
            {
                float h = saturate(0.5 + 0.5 * (b - a) / k);
                return lerp(b, a, h) - k * h * (1.0 - h);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float d = 1e3;
                int count = (int)_Count;
                [loop] for (int n = 0; n < 12; n++)
                {
                    if (n >= count) break;
                    float s = Capsule(i.local, _A[n].xy, _A[n].zw, _R[n].x, _R[n].y);
                    d = n == 0 ? s : SmoothMin(d, s, _Blend);
                }
                float w = max(fwidth(d), 1e-4);
                float shape = 1.0 - smoothstep(-w, w, d);
                float inner = 1.0 - smoothstep(-w, w, d + _Outline);
                half4 c = lerp(_Ink, _Fill, inner);
                c.a *= shape;
                return c;
            }
            ENDHLSL
        }
    }
}
