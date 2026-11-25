// 날리는 잎 · 꽃잎(2026-11-24, SagajeongDispatchTrees.cs · 사용자: 「2.5D라도 폴리곤을 써서 바람에 흩날리는 … 나뭇잎」).
//   나무마다 잎 몇 장을 네모 폴리곤으로 둔다. 정점 위치는 쓰지 않고 셰이더가 시간으로 움직인다:
//   잎 덩어리에서 떨어져 → 바람 쪽으로 밀리며(돌풍이 지나가면 더 멀리) 빙글빙글 돌고 → 땅에 내려앉아 잠깐 누웠다가 사라지고 다시 떨어진다.
//   uv = 네모 모서리 · uv2 = (난수, 수종) · uv3 = 잎 덩어리 가운데 · 색 = (난수, 난수, 덩어리 반지름/4).
//   언제 몇 장이 날리는지(전역 값, SagajeongDispatchEnvironment.cs): _HongdalEnvD.z = 잎이 지는 철, .y = 벚꽃잎이 날리는 철.
//   크기는 화면 배율에 맞춘 표현값(_HongdalDriftSize, m)이다 — 실제 잎(5 cm)은 이 배율에서 보이지 않는다.
Shader "Hongdal/Drift"
{
    Properties
    {
        _Dim ("Dim", Float) = 1
        _Window ("Window", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "Queue" = "AlphaTest" "RenderType" = "TransparentCutout" }
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
            float4 _Window;
            float4 _HongdalEnvB;
            float4 _HongdalEnvC;
            float4 _HongdalEnvD;
            float4 _HongdalSunTint;
            float _HongdalNight;
            float4 _HongdalFog;
            float _HongdalDriftSize;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
                float3 centre : TEXCOORD2;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 colour : TEXCOORD1;
                float2 ground : TEXCOORD2;
                float depth : TEXCOORD3;
                float keep : TEXCOORD4;
            };

            float3 Rotate(float3 v, float3 axis, float angle)
            {
                float c = cos(angle), s = sin(angle);
                return v * c + cross(axis, v) * s + axis * dot(axis, v) * (1.0 - c);
            }

            float3 LeafColour(float sp, float r)
            {
                if (sp < 1.5) return float3(0.95, 0.80, 0.18);                                       // 은행: 샛노랑
                if (sp < 2.5) return lerp(float3(0.82, 0.34, 0.16), float3(0.70, 0.22, 0.12), r);    // 벚: 주홍
                if (sp < 3.5) return lerp(float3(0.72, 0.44, 0.18), float3(0.60, 0.34, 0.15), r);    // 느티
                if (sp < 4.5) return float3(0.66, 0.53, 0.26);                                       // 버즘
                if (sp < 6.5) return float3(0.70, 0.56, 0.22);
                if (sp < 7.5) return lerp(float3(0.88, 0.20, 0.12), float3(0.78, 0.12, 0.10), r);    // 단풍
                return float3(0.56, 0.38, 0.18);                                                     // 참나무
            }

            v2f vert(appdata v)
            {
                v2f o;
                float sp = v.uv2.y;
                float r1 = v.uv2.x, r2 = v.color.r, r3 = v.color.g;
                float radius = v.color.b * 4.0;
                float cherry = step(1.5, sp) * step(sp, 2.5);
                float petals = cherry * _HongdalEnvD.y;                       // 벚꽃잎
                float leaves = _HongdalEnvD.z;                                 // 낙엽
                float petalMode = step(leaves, petals);
                float amount = max(petals, leaves);
                float shown = step(r1, amount * 0.95);
                float size = _HongdalDriftSize * lerp(1.0, 0.7, petalMode) * (0.75 + 0.5 * r2) * shown;

                float fallSpeed = lerp(1.2, 0.65, petalMode) * (0.8 + 0.4 * r3);
                float height = max(v.centre.y + radius * (r2 - 0.3), 1.5);
                float flight = height / fallSpeed;
                float cycle = flight + 2.5;                                    // 땅에서 2.5초 누워 있다가 다시
                float t = _HongdalWindTime + r3 * 131.0 + r1 * 17.0;
                float age = frac(t / cycle) * cycle;
                float air = min(age, flight);
                float landed = step(flight, age);

                float3 rootWorld = mul(unity_ObjectToWorld, float4(v.centre, 1.0)).xyz;
                float2 dir = _HongdalWind.xy;
                float speed = _HongdalWind.z;
                float2 start = (float2(r2, r3) - 0.5) * 2.0 * radius;
                float g = HongdalGust(rootWorld.xz + dir * air * speed * 0.6);
                float2 drift = dir * speed * (0.35 + 0.75 * g) * air * lerp(1.0, 1.4, petalMode);
                float2 flutter = float2(sin(t * 2.1 + r2 * 31.0), cos(t * 1.7 + r3 * 23.0)) * lerp(0.5, 0.9, petalMode) * (1.0 - landed);
                float y = max(height - fallSpeed * air, 0.06);
                float3 local = float3(v.centre.x + start.x + drift.x + flutter.x, y, v.centre.z + start.y + drift.y + flutter.y);

                // 빙글빙글: 떨어지는 동안 기운 축으로 돌고, 땅에서는 납작하게 눕는다.
                float3 axis = normalize(float3(r2 - 0.5, 0.35 + r1, r3 - 0.5));
                float angle = (min(age, flight)) * (3.0 + 5.0 * r2) * (1.0 + g);
                float3 e1 = Rotate(float3(1, 0, 0), axis, angle);
                float3 e2 = Rotate(float3(0, 0, 1), axis, angle);
                e1 = lerp(e1, float3(cos(r1 * 6.28), 0, sin(r1 * 6.28)), landed);
                e2 = lerp(e2, float3(-sin(r1 * 6.28), 0, cos(r1 * 6.28)), landed);
                float2 corner = v.uv * 2.0 - 1.0;
                local += (e1 * corner.x + e2 * corner.y * 0.62) * size;
                float fadeGround = landed * saturate((age - flight) / 2.5);
                o.keep = (1.0 - fadeGround) * shown;

                float3 normal = normalize(cross(e1, e2));
                float light = 0.55 + 0.55 * abs(dot(normal, normalize(float3(-0.72, 0.60, -0.28))));     // 돌 때마다 밝아졌다 어두워진다
                float3 colour = petalMode > 0.5 ? lerp(float3(0.99, 0.86, 0.91), float3(0.97, 0.74, 0.84), r2) : LeafColour(sp, r2);
                colour *= light * lerp(float3(1, 1, 1), _HongdalSunTint.rgb, _HongdalEnvC.w);
                o.pos = UnityObjectToClipPos(float4(local, 1.0));
                o.uv = v.uv;
                o.colour = colour;
                o.ground = local.xz;
                o.depth = -UnityObjectToViewPos(float4(local, 1.0)).z;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = i.uv * 2.0 - 1.0;
                clip(1.0 - length(p * float2(1.0, 1.0)) - 0.02);          // 둥근 잎 모양
                clip(i.keep - frac(dot(floor(i.pos.xy), float2(0.37, 0.61))) * 0.999);    // 땅에서 점점이 사라진다
                float3 c = i.colour;
                c *= lerp(1.0, 0.32, _HongdalNight);
                c *= _Dim;
                float fog = saturate((i.depth - _HongdalFog.x) / max(1.0, _HongdalFog.y - _HongdalFog.x)) * _HongdalFog.z;
                c = lerp(c, float3(0.055, 0.086, 0.114), fog);
                float outside = _Window.z > 0.0 ? saturate((distance(i.ground, _Window.xy) - _Window.z) / max(1.0, _Window.w)) : 0.0;
                clip(0.999 - outside);
                return fixed4(c, 1);
            }
            ENDCG
        }
    }
}
