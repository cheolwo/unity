// 현실감 시험의 접지 그늘(SagajeongDispatchRealism.cs, 2026-11-24). 벽 밑변 양쪽 1.9 m 띠, 정점 알파가 벽 쪽 1 → 바깥 0.
// 곱하기로 그려 겹친 오목한 모서리 · 좁은 골목은 더 어둡다(실제 그늘처럼). _HongdalReal.z = 세기(낮 0.95 · 밤 0.55).
Shader "Hongdal/ContactShade"
{
    SubShader
    {
        Tags { "Queue" = "Geometry+12" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend DstColor Zero

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _Window;
            float4 _HongdalReal;
            float _HongdalClose;

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 ground : TEXCOORD0;
                float a : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.ground = v.vertex.xz;
                o.a = v.color.a;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float outside = _Window.z > 0.0 ? saturate((distance(i.ground, _Window.xy) - _Window.z) / max(1.0, _Window.w)) : 0.0;
                float a = i.a * i.a * 0.42 * _HongdalReal.z * (1.0 - outside);
                a = min(0.8, a * lerp(1.6, 1.0, _HongdalClose));      // 역둘레 B3(2026-11-24): 멀리서는 골목 그늘을 짙게. 건물 밑둘레가 어두운 선이 되어 덩어리가 갈린다
                return fixed4(1.0 - a, 1.0 - a * 0.92, 1.0 - a * 0.78, 1.0);
            }
            ENDCG
        }
    }
}
