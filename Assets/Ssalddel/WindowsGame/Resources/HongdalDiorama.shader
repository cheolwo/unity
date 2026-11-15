// 2.5D 지도 시제품의 바탕 메시(지면, 도로, 건물)용. 조명 없이 구운 정점 색을 그대로 쓰고,
// 벽에는 UV(가로 m, 높이 m)로 창 격자를 그린다. uv2.x = 1 이면 벽, uv2.y 는 창 불빛 난수 씨앗.
// _Dim: 행정동 모듈에서 고르지 않은 이웃 동을 어둡게 할 때 렌더러마다 낮춘다(기본 1 = 그대로).
// _Grey: 회색 동네(r143). 1이면 색을 빼고 대비를 낮춘다. 밝기 순서는 남겨 도로·건물·땅이 구별되게 한다(기본 0 = 그대로).
// _Window: 역 중심 보기의 둥근 창(가운데 동·북, 반경, 흐려지는 폭, m). 창 밖은 바탕색으로 흐려지다 끊긴다. 반경이 0이면 창이 없다.
// _LightMap, _LightRect: 관리 건물의 빛. 메시 좌표(x 동, z 북, m)로 읽는 밝기 그림이다. 빛이 닿는 곳은 회색으로 빼지 않고 조금 더 밝게 둔다.
// _Lit: 불 켜진 창의 비율(기본 0.2 = 전과 같다). 내가 관리하는 건물은 더 많이 켠다. 회색 동네의 바탕 건물은 창 불빛을 끈다.
// _ActivityMap, _ActivityRect, _ActivityOn: 기사와 가게의 빛(전역 값). 메시 좌표로 읽는 네 겹의 그림이다.
//   R = 기사가 지나간 자취(연둣빛), G = 음식점(주황빛), B = 전달지(하늘빛), A = 자주 다닌 길의 온기.
//   빛이 닿은 땅 · 길 · 지붕 · 벽은 회색에서 깨어나고 그 빛의 색을 띤다. _ActivityOn이 0이면 전과 똑같이 그린다.
//   빛으로 보는 동안(_ActivityOn = 1) 회색 동네의 빛이 닿지 않은 곳은 더 어둡게 둔다(밤). 빛이 닿아야 밝아진다.
// _ActivityBeat: 빛의 숨(전역 값, 초). 빛이 밝아졌다 줄어든다. 겹마다 박자가 다르고 자리에 따라 조금씩 엇갈린다.
//   화면 쪽의 빛 한 점(SagajeongDispatchRoam.cs의 LightBeat)과 같은 식이다. 고치면 함께 고친다.
// 가까이 보기(전역 값, SagajeongDispatchCloseView.cs). 멀리서는 모두 0이라 전과 똑같이 그린다.
//   _HongdalClose: 0~1. 벽마다 켜진 창의 비율 · 창마다 빛의 색과 밝기가 달라지고, 벽이 땅 쪽으로 어두워진다.
//   _HongdalFog: (시작 깊이, 끝 깊이, 세기). 화면 가운데보다 먼 땅이 바탕색으로 옅어진다.
//   _HongdalPick · _HongdalPickColor: 고른 곳(가운데 x · z, 반경, 세기)과 그 색. 그 건물이 조금 밝아진다.
//   _HongdalReveal · _HongdalRevealDir · _HongdalRevealOwn: 비침 창. 고른 곳(x, y, z)을 가리는 앞 건물을 반경 w로 둥글게 비운다.
//     고른 건물 자신(가운데에서 Own 안)은 비우지 않는다. 땅과 길은 비우지 않는다.
//   _HongdalLampMap · _HongdalLampRect · _HongdalLamp: 길가 불빛(표현, SagajeongDispatchStreetLamps.cs). 메시 좌표로 읽는 밝기 그림과 세기.
//     땅 · 길과 벽의 아래쪽만 밝힌다. 색은 거의 넣지 않는다(음식점 · 기사의 빛과 헷갈리지 않게). 세기가 0이면 전과 똑같이 그린다.
// 다시 구운 동 메시(품5, 2026-11-15, make_dong_modules.py). 모두 표현값이고 가까이에서만 보인다. 멀리서는 전과 똑같다.
//   uv2.y = -1: 지붕 테두리(난간 0.45 m). _HongdalClose가 0.75 아래면 지운다(0.75~0.95에서 점점이 나타난다).
//   uv2.y >= 1000 (벽): 근린생활시설. 1000을 빼면 전과 같은 창 씨앗이다. 가까이에서 1층에 넓은 유리와 간판 띠(글자 없음)를 그린다.
// 녹지(r221 계1, 2026-11-15, make_dong_modules.py): 땅 한 겹(높이 0.015 m), uv.x = 갈래(1 산림지 · 2 조경녹지 · 3 하천 · 4 경작지).
//   범위는 서울시 생태현황도, 색은 표현. 회색 동네에서도 색을 30 %만 뺀다(시안 3처럼 건물만 회색). 계절 색(계3)은 이 갈래 번호를 쓴다.
// 거리 겹(거리 풍경 보1 · 거1, 2026-11-15, make_dong_modules.py): uv.x 5 보도 · 6 횡단보도 줄 · 7 점자블록 · 8 연석선 · 9 시설물대.
//   정점 색은 그 아래 보이던 색(바닥 · 연석 띠 · 차도)이라 멀리서는 전과 똑같다. _HongdalClose가 0.35~0.75일 때 서서히 칠한다.
//   횡단보도 자리는 서울시 자료(OA-23081), 보도 범위는 계산, 블록 무늬 · 줄무늬 · 점자블록 모양 · 색은 표현.
Shader "Hongdal/Diorama"
{
    Properties
    {
        _Dim ("Dim", Float) = 1
        _Grey ("Grey", Range(0, 1)) = 0
        _Lit ("Lit windows", Range(0, 1)) = 0.2
        _LightMap ("Light map", 2D) = "black" {}
        _LightRect ("Light rect", Vector) = (0, 0, 0, 0)
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

            float _Dim;
            float _Grey;
            float _Lit;
            sampler2D _LightMap;
            float4 _LightRect;
            float4 _Window;
            sampler2D _ActivityMap;
            float4 _ActivityRect;
            float _ActivityOn;
            float _ActivityBeat;
            float _ActivityPulse;
            float _HongdalClose;
            float4 _HongdalFog;
            float4 _HongdalPick;
            float4 _HongdalPickColor;
            float4 _HongdalReveal;
            float4 _HongdalRevealDir;
            float _HongdalRevealOwn;
            sampler2D _HongdalLampMap;
            float4 _HongdalLampRect;
            float _HongdalLamp;

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
                float2 ground : TEXCOORD2;
                float3 world : TEXCOORD3;
                float depth : TEXCOORD4;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                o.uv2 = v.uv2;
                o.ground = v.vertex.xz;
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.depth = -UnityObjectToViewPos(v.vertex).z;
                return o;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // 4×4 순서 무늬(0 이상 1 미만). 비침 창의 가장자리를 점점이 비운다.
            float bayer2(float2 a)
            {
                a = floor(a);
                return frac(a.x / 2.0 + a.y * a.y * 0.75);
            }

            float bayer4(float2 a)
            {
                return bayer2(0.5 * a) * 0.25 + bayer2(a);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                if (i.uv2.y < -0.5)
                    clip((_HongdalClose - 0.75) / 0.2 - bayer4(i.pos.xy) - 0.01);      // 지붕 테두리: 가까이에서만
                if (_HongdalReveal.w > 0.0 && (i.uv2.x > 0.5 || i.world.y > 0.5))
                {
                    float3 toward = lerp(normalize(_HongdalReveal.xyz - _WorldSpaceCameraPos), _HongdalRevealDir.xyz, unity_OrthoParams.w);
                    float3 off = i.world - _HongdalReveal.xyz;
                    float ahead = dot(off, toward);
                    float side = length(off - toward * ahead);
                    if (ahead < -2.0 && distance(i.world.xz, _HongdalReveal.xz) > _HongdalRevealOwn)
                        clip(smoothstep(_HongdalReveal.w * 0.55, _HongdalReveal.w, side) - bayer4(i.pos.xy) - 0.03);
                }
                float3 c = i.color.rgb;
                c *= lerp(1.0, lerp(0.60, 1.0, smoothstep(0.0, 7.0, i.uv.y)), _HongdalClose * i.uv2.x);      // 벽은 땅 쪽으로 어두워진다 (uv.y = 높이 m)
                float streetTag = (1.0 - i.uv2.x) * step(-0.5, i.uv2.y) * step(i.world.y, 0.2) * i.uv.x;      // 거리 겹 표시 (보1 · 거1)
                if (streetTag > 4.5)
                {
                    float closeMix = smoothstep(0.35, 0.75, _HongdalClose);
                    float2 paveCell = floor(i.ground / 0.6);
                    float jitter = hash21(paveCell);
                    float2 paveEdge = abs(frac(i.ground / 0.6) - 0.5);
                    float joint = smoothstep(0.40, 0.48, max(paveEdge.x, paveEdge.y));
                    float3 paint;
                    if (streetTag < 5.5) paint = float3(0.27, 0.29, 0.31) * (0.93 + 0.12 * jitter) * (1.0 - 0.12 * joint);       // 보도 블록(표현)
                    else if (streetTag < 6.5) paint = float3(0.78, 0.80, 0.82);                                                  // 횡단보도 줄
                    else if (streetTag < 7.5)
                    {
                        float2 bump = abs(frac(i.ground / 0.15) - 0.5);
                        paint = float3(0.78, 0.62, 0.20) * (1.0 - 0.22 * step(length(bump), 0.22));                              // 점자블록
                    }
                    else if (streetTag < 8.5) paint = float3(0.52, 0.55, 0.58);                                                  // 연석선
                    else paint = float3(0.24, 0.26, 0.28) * (0.95 + 0.08 * jitter) * (1.0 - 0.10 * joint);                       // 시설물대
                    c = lerp(c, paint, closeMix);
                }
                float shop = step(999.5, i.uv2.y) * i.uv2.x;                    // 근린생활시설의 벽
                float seed = i.uv2.y - 1000.0 * shop;                             // 창 불빛 씨앗 (전과 같다)
                float front = shop * saturate((_HongdalClose - 0.3) / 0.5) * step(i.uv.y, 3.0);     // 1층 가게: 가까이에서만
                float2 cell = float2(i.uv.x / 2.6, i.uv.y / 3.0);
                float2 f = frac(cell);
                float win = step(0.26, f.x) * step(f.x, 0.74) * step(0.30, f.y) * step(f.y, 0.72) * i.uv2.x * (1.0 - front);
                float glow = tex2D(_LightMap, (i.ground - _LightRect.xy) * _LightRect.zw).r;
                float4 act = tex2D(_ActivityMap, (i.ground - _ActivityRect.xy) * _ActivityRect.zw) * _ActivityOn;
                float sway = dot(i.ground, float2(0.02, 0.014));        // 자리에 따라 숨이 엇갈린다 (약 260 m에 한 바퀴)
                act.r *= 1.0 + 0.32 * sin(_ActivityBeat * 4.2 + sway);          // 기사: 빠르게
                act.g *= 1.0 + 0.12 * sin(_ActivityBeat * 2.3 + sway * 0.7);    // 음식점: 천천히, 잔잔하게(빛 한 점이 빛마다 엇갈려 숨 쉰다)
                act.b *= 1.0 + 0.14 * sin(_ActivityBeat * 3.1 + sway * 1.3);    // 전달지: 잔잔하게
                act.a *= 1.0 + 0.16 * sin(_ActivityBeat * 1.3 + sway * 0.5);    // 온기: 아주 천천히
                act.rgb *= 1.0 + _ActivityPulse;        // 고른 곡이 나오는 동안: 저음에 맞춰 함께 밝아진다 (곡이 없으면 0)
                glow = max(glow, saturate(max(max(act.r, act.g), act.b) + 0.6 * act.a));      // 빛이 닿은 곳은 회색에서 깨어난다
                float dull = _Grey * (1.0 - glow);      // 0 = 그대로(회색 동네가 아니거나 내 건물 곁, 빛이 닿은 곳), 1 = 회색
                float nature = step(0.5, i.uv.x) * step(i.uv.x, 4.5) * step(i.world.y, 0.05) * (1.0 - i.uv2.x);      // 녹지 한 겹 (계1). 5~9는 거리 겹
                dull *= 1.0 - 0.7 * nature;
                // 가까이에서는 벽마다 켜진 창의 비율이 다르고, 창마다 빛의 색과 밝기가 조금씩 다르다. 멀리서는 전과 같다.
                float ratio = _Lit * lerp(1.0, 0.5 + 1.4 * hash21(float2(seed, 7.3)), _HongdalClose);
                float lit = step(1.0 - ratio, hash21(floor(cell) + seed)) * (1.0 - dull);
                float3 warm = lerp(float3(1.0, 0.84, 0.55), float3(0.86, 0.93, 1.0), step(0.82, hash21(floor(cell) * 1.3 + seed)) * _HongdalClose);
                warm *= lerp(1.0, 0.74 + 0.46 * hash21(floor(cell) * 2.1 + seed * 3.0), _HongdalClose);
                float3 pane = lerp(c * 0.45 + float3(0.09, 0.13, 0.18) * 0.55, warm, lit);
                c = lerp(c, pane, win);
                if (front > 0.0)
                {
                    // 1층 가게 불빛(표현): 3.2 m마다 기둥이 있는 넓은 유리, 그 위에 간판 띠. 6.4 m 한 칸이 한 가게, 넷 중 셋이 켜져 있다.
                    float bay = floor(i.uv.x / 6.4);
                    float open = step(0.25, hash21(float2(bay, seed * 0.37 + 11.0))) * (1.0 - dull);
                    float gx = frac(i.uv.x / 3.2);
                    float glass = step(0.07, gx) * step(gx, 0.93) * step(0.3, i.uv.y) * step(i.uv.y, 2.25);
                    float sx = frac(i.uv.x / 6.4);
                    float band = step(0.03, sx) * step(sx, 0.97) * step(2.42, i.uv.y) * step(i.uv.y, 2.9);
                    float3 shopLight = float3(1.0, 0.85, 0.6) * (0.82 + 0.3 * hash21(float2(bay, seed)));
                    float3 glassColor = lerp(float3(0.10, 0.14, 0.18), shopLight, open);
                    float3 bandColor = lerp(float3(0.95, 0.93, 0.88), float3(0.80, 0.86, 0.95), hash21(float2(bay * 1.7, seed + 3.0))) * (0.55 + 0.4 * open);
                    c = lerp(c, glassColor, glass * front);
                    c = lerp(c, bandColor, band * front);
                }
                float tone = saturate(dot(c, float3(0.299, 0.587, 0.114)));
                float3 grey = lerp(float3(0.055, 0.07, 0.085), float3(0.56, 0.59, 0.62), tone);
                c = lerp(c * (1.0 + 0.3 * glow * _Grey), grey * (1.0 - 0.42 * _ActivityOn), dull);
                float ride = act.r * (0.3 + 0.7 * act.r);       // 기사가 있는 자리가 가장 밝고, 자취는 뒤로 갈수록 빨리 옅어진다
                float3 light = ride * float3(0.78, 0.95, 0.30) + act.g * float3(1.00, 0.60, 0.22) + act.b * float3(0.30, 0.82, 1.00)
                    + act.a * float3(0.26, 0.30, 0.12);
                c = c * (1.0 + 0.8 * light) + 0.16 * light;
                float lamp = tex2D(_HongdalLampMap, (i.ground - _HongdalLampRect.xy) * _HongdalLampRect.zw).r * _HongdalLamp;
                lamp *= lerp(step(i.world.y, 0.6), 0.6 * (1.0 - smoothstep(0.5, 5.5, i.uv.y)), i.uv2.x);      // 땅 · 길, 벽은 아래쪽만
                c = c * (1.0 + 0.35 * lamp) + float3(1.0, 0.80, 0.50) * (0.24 * lamp);                          // 길가 불빛(표현): 옅은 따뜻한 흰빛
                c *= _Dim;
                float pickRadius = max(_HongdalPick.z, 0.01);
                float pick = _HongdalPick.w * saturate((pickRadius - distance(i.world.xz, _HongdalPick.xy)) / (pickRadius * 0.25));
                c = lerp(c, c * 1.18 + _HongdalPickColor.rgb * 0.16, pick);        // 고른 곳
                float fog = saturate((i.depth - _HongdalFog.x) / max(1.0, _HongdalFog.y - _HongdalFog.x)) * _HongdalFog.z;
                c = lerp(c, float3(0.055, 0.086, 0.114), fog);                      // 먼 땅은 바탕색으로
                float outside = _Window.z > 0.0 ? saturate((distance(i.ground, _Window.xy) - _Window.z) / max(1.0, _Window.w)) : 0.0;
                clip(0.999 - outside);
                c = lerp(c, float3(0.055, 0.086, 0.114), outside * outside * (3.0 - 2.0 * outside) * 0.92);     // 바탕색(DongBackdrop)으로
                return fixed4(c, 1);
            }
            ENDCG
        }
    }
}
