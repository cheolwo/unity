// 동 디오라마의 나무(E5 · 2026-11-23, SagajeongDispatchTrees.cs). 자리 · 수종 · 크기는 표현이다.
//   uv = (0 줄기 · 1 잎, 수종) · uv2 = (난수, 자리 갈래 0 길가 · 1 산 · 2 공원) · uv3 = 잎 덩어리 가운데.
//   수종: 1 은행 · 2 벚 · 3 느티 · 4 버즘 · 6 그 밖 · 7 단풍 · 8 소나무 · 9 참나무.
//   철(전역 값, SagajeongDispatchEnvironment.cs): _HongdalEnvB = (벚꽃, 잎, 단풍, 쌓인 눈) · _HongdalEnvC = (연두, 비, 언 하천, 켬).
//   잎이 지면 덩어리가 줄어 맨 가지 빛이 되고, 벚나무는 잎보다 먼저 분홍으로 핀다. 소나무는 늘 푸르다. 눈은 위쪽 면에 쌓인다.
//   밤 · 길가 불빛 · 안개 · 회색 동네 · 역 창은 동 바탕 셰이더(HongdalDiorama)와 같은 식이다.
//   바람(2026-11-24, HongdalWind.cginc): 잎 덩어리와 줄기 윗부분이 바람 쪽으로 기울고 떨린다. 돌풍 띠가 지나가면 차례로 크게 기운다.
//   이팝나무(5): 5월에 흰 꽃(_HongdalEnvD.w).
Shader "Hongdal/Tree"
{
    Properties
    {
        _Dim ("Dim", Float) = 1
        _Grey ("Grey", Range(0, 1)) = 0
        _Window ("Window", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "Queue" = "Geometry" "RenderType" = "Opaque" }
        Pass
        {
            Cull Off
            ZWrite On
            ZTest LEqual

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "HongdalWind.cginc"

            float _Dim;
            float _Grey;
            float4 _Window;
            float4 _HongdalEnvB;
            float4 _HongdalEnvC;
            float4 _HongdalEnvD;
            float4 _HongdalSunTint;
            float _HongdalNight;
            sampler2D _HongdalLampMap;
            float4 _HongdalLampRect;
            float _HongdalLamp;
            float4 _HongdalFog;
            float4 _HongdalSunDir;          // 현실감 시험(SagajeongDispatchRealism.cs): 해 쪽 단위 벡터 · 빛
            float4 _HongdalReal;            //   켬(0이면 전과 같다)
            float _HongdalRaise;            //   가 등급 동: 올린 보도 높이(m, 렌더러마다). 길가 나무(uv2.y 0)만 그만큼 올라선다

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
                float3 centre : TEXCOORD2;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 colour : TEXCOORD0;
                float2 ground : TEXCOORD1;
                float depth : TEXCOORD2;
            };

            float3 SummerOf(float sp)
            {
                if (sp < 1.5) return float3(0.30, 0.47, 0.20);      // 은행
                if (sp < 2.5) return float3(0.24, 0.39, 0.21);      // 벚
                if (sp < 3.5) return float3(0.23, 0.40, 0.22);      // 느티
                if (sp < 4.5) return float3(0.30, 0.45, 0.22);      // 버즘
                if (sp < 5.5) return float3(0.27, 0.43, 0.24);      // 이팝
                if (sp < 6.5) return float3(0.26, 0.42, 0.22);      // 그 밖
                if (sp < 7.5) return float3(0.28, 0.44, 0.20);      // 단풍
                if (sp < 8.5) return float3(0.13, 0.27, 0.18);      // 소나무
                return float3(0.22, 0.36, 0.18);                     // 참나무
            }

            float3 TurnOf(float sp)
            {
                if (sp < 1.5) return float3(0.93, 0.78, 0.16);      // 은행: 샛노랑
                if (sp < 2.5) return float3(0.76, 0.32, 0.16);      // 벚: 주홍
                if (sp < 3.5) return float3(0.68, 0.42, 0.18);      // 느티: 갈색 주황
                if (sp < 4.5) return float3(0.62, 0.50, 0.24);      // 버즘: 누런 갈색
                if (sp < 5.5) return float3(0.66, 0.52, 0.22);      // 이팝: 누렇게
                if (sp < 6.5) return float3(0.62, 0.48, 0.20);
                if (sp < 7.5) return float3(0.82, 0.20, 0.12);      // 단풍: 빨강
                if (sp < 8.5) return float3(0.15, 0.27, 0.18);      // 소나무: 그대로
                return float3(0.52, 0.36, 0.18);                     // 참나무: 갈색
            }

            v2f vert(appdata v)
            {
                v2f o;
                float on = _HongdalEnvC.w;
                float crown = v.uv.x;
                float sp = v.uv.y;
                float rnd = v.uv2.x;
                float pine = step(7.5, sp) * step(sp, 8.5);
                float leaf = on > 0.0 ? _HongdalEnvB.y : 1.0;
                float autumn = on > 0.0 ? _HongdalEnvB.z : 0.0;
                float bloom = on > 0.0 ? _HongdalEnvB.x : 0.0;
                float freshness = on > 0.0 ? _HongdalEnvC.x : 0.0;
                float snow = _HongdalEnvB.w;
                // 같은 철에도 나무마다 조금씩 늦고 빠르다. 은행은 늦게 물들고, 벚은 일찍 물든다.
                float lead = sp < 1.5 ? -0.18 : (sp > 1.5 && sp < 2.5 ? 0.15 : 0.0);
                float leafT = saturate(leaf * 1.3 - 0.3 * rnd);
                float turnT = saturate((autumn + lead) * 1.45 - 0.45 * rnd);
                float bloomT = (sp > 1.5 && sp < 2.5) ? saturate(bloom * 1.4 - 0.4 * rnd) : 0.0;
                float3 colour = lerp(SummerOf(sp), float3(0.50, 0.68, 0.28), freshness * 0.8 * (1.0 - pine));
                colour = lerp(colour, TurnOf(sp), turnT * (1.0 - pine));
                colour = lerp(float3(0.47, 0.42, 0.37), colour, lerp(leafT, 1.0, pine));        // 잎이 지면 맨 가지 빛(옅은 회갈색)
                colour = lerp(colour, float3(0.97, 0.79, 0.87), bloomT);                         // 벚꽃
                float ipapT = (sp > 4.5 && sp < 5.5) ? saturate(_HongdalEnvD.w * 1.4 - 0.4 * rnd) : 0.0;
                colour = lerp(colour, float3(0.95, 0.96, 0.91), ipapT * 0.85);                   // 이팝: 흰 꽃
                float fill = lerp(lerp(0.55, 1.0, max(leafT, bloomT)), 1.0, pine);
                float3 p = v.vertex.xyz;
                if (crown > 0.5) p = v.centre + (p - v.centre) * fill;
                else colour = float3(0.21, 0.17, 0.14);                                          // 줄기
                float2 rootXZ = mul(unity_ObjectToWorld, float4(v.centre.x, 0.0, v.centre.z, 1.0)).xz;
                p += HongdalSway(rootXZ, saturate(p.y / 7.0), rnd) * lerp(0.55, 1.0, max(leafT, pine));       // 바람: 잎이 없으면 덜 흔들린다
                p.y += _HongdalRaise * step(v.uv2.y, 0.5) * step(0.5, _HongdalReal.x);
                float up = saturate(v.normal.y * 1.6 + 0.1);
                colour = lerp(colour, float3(0.76, 0.80, 0.85), snow * up * step(0.5, crown) * lerp(0.6, 0.9, pine));
                float3 toLight = normalize(float3(-0.72, 0.60, -0.28));
                float lightK = 0.62 + 0.46 * saturate(dot(normalize(v.normal), toLight));
                if (_HongdalReal.x > 0.5)       // 현실감 시험: 그날 그 시각의 해 쪽이 밝다. 해가 없으면 고르게.
                    lightK = lerp(0.86, (0.60 + 0.62 * saturate(dot(normalize(v.normal), _HongdalSunDir.xyz))) / 0.98, _HongdalSunDir.w);
                colour *= lightK;
                colour *= lerp(float3(1, 1, 1), _HongdalSunTint.rgb, on);
                o.pos = UnityObjectToClipPos(float4(p, 1.0));
                o.colour = colour;
                o.ground = p.xz;
                o.depth = -UnityObjectToViewPos(float4(p, 1.0)).z;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 c = i.colour;
                float lamp = tex2D(_HongdalLampMap, (i.ground - _HongdalLampRect.xy) * _HongdalLampRect.zw).r * _HongdalLamp;
                c = c * (1.0 + 0.4 * lamp) + float3(1.0, 0.80, 0.50) * (0.12 * lamp);
                float keep = saturate(1.6 * lamp);
                c *= lerp(1.0, lerp(0.28, 1.05, keep), _HongdalNight);
                c += float3(0.004, 0.012, 0.035) * _HongdalNight * (1.0 - keep);
                float tone = dot(c, float3(0.299, 0.587, 0.114));
                c = lerp(c, tone * float3(0.92, 0.96, 1.0), 0.3 * _Grey);
                c *= _Dim;
                float fog = saturate((i.depth - _HongdalFog.x) / max(1.0, _HongdalFog.y - _HongdalFog.x)) * _HongdalFog.z;
                c = lerp(c, float3(0.055, 0.086, 0.114), fog);
                float outside = _Window.z > 0.0 ? saturate((distance(i.ground, _Window.xy) - _Window.z) / max(1.0, _Window.w)) : 0.0;
                clip(0.999 - outside);
                c = lerp(c, float3(0.055, 0.086, 0.114), outside * outside * (3.0 - 2.0 * outside) * 0.92);
                return fixed4(c, 1);
            }
            ENDCG
        }
        // HONGDAL_URP_USEPASS — URP 시험 갈래(S2)가 빌드 사본에서만 이 줄을 그림자 패스(UsePass "Hongdal/UrpCaster/SHADOWCASTER")로 바꾼다.
    }
}
