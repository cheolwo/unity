// 현실감 시험의 해 그림자(SagajeongDispatchRealism.cs, 2026-11-24). 건물 메시의 벽(uv2.x = 1)이나 나무 메시 전체(_Mode = 1)를
// 해 반대쪽 땅(높이 0.11 m, 미리 구운 그림자와 같은 높이)으로 눕혀 그린다. 높이 h의 점은 해 방향의 반대로 h / tan(해 높이)만큼 간다.
// 벽마다 눕힌 사각형을 모으면 건물의 그림자가 되고, 스텐실로 겹친 곳이 두 번 어두워지지 않게 한다(미리 구운 그림자와 같다).
// _HongdalSunDir = (해 쪽 단위 벡터 xyz, 빛 0~1) · _HongdalReal = (켬, 그림자 진하기, 접지 그늘 세기, 0). 해가 7°보다 낮으면 길이를 거기서 멈춘다.
Shader "Hongdal/SunShadow"
{
    Properties
    {
        _Mode ("Mode (0 walls, 1 all)", Float) = 0
        _Alpha ("Alpha", Float) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Geometry+10" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha
            Stencil
            {
                Ref 1
                Comp NotEqual
                Pass Replace
            }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float _Mode;
            float _Alpha;
            float4 _Window;
            float4 _HongdalTier;
            float4 _HongdalSunDir;
            float4 _HongdalReal;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv2 : TEXCOORD1;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 ground : TEXCOORD0;
                float fade : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                float keep = _Mode > 0.5 ? 1.0 : step(0.5, v.uv2.x);
                float3 p = v.vertex.xyz;
                float fade = 1.0;
                if (_Window.z > 0.0 && _HongdalTier.z > 0.0)
                {
                    float away = smoothstep(_HongdalTier.y * 0.94, _HongdalTier.y * 1.12, distance(p.xz, _Window.xy));
                    p.y *= lerp(1.0, _HongdalTier.w, away * _HongdalTier.z);       // 역세권 너머는 낮아진 높이로
                    fade = 1.0 - 0.7 * _HongdalTier.z * away;
                }
                float up = max(_HongdalSunDir.y, 0.14);
                p.xz -= _HongdalSunDir.xz / up * max(p.y, 0.0);
                p.y = 0.195 + 0.002 * _Mode;                 // 올린 보도(최고 0.184 m) 위
                o.pos = keep > 0.5 ? UnityObjectToClipPos(float4(p, 1.0)) : float4(2.0, 2.0, 2.0, 1.0);
                o.ground = p.xz;
                o.fade = fade;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float outside = _Window.z > 0.0 ? saturate((distance(i.ground, _Window.xy) - _Window.z) / max(1.0, _Window.w)) : 0.0;
                float a = _HongdalReal.y * _Alpha * i.fade * (1.0 - outside);
                clip(a - 0.003);
                return fixed4(0.04, 0.07, 0.15, a);          // 하늘빛이 도는 그늘
            }
            ENDCG
        }
    }
}
