// 날씨 알갱이(E3 · 2026-11-23, SagajeongDispatchWeatherParticles.cs). 파티클 모듈 없이 정점 셰이더가 시간으로 움직인다.
//   메시는 네모 N개(정점 위치는 쓰지 않는다). uv = 네모의 모서리(0~1), uv2 = 난수 둘, 색 = 난수 둘.
//   _Area = (한 변 m, 높이 m, 떨어지는 속도 m/s, 줄 길이 m) · _Shape = (굵기 m, 바람 x m/s, 흔들림 m, 0 비 · 1 눈) · _Tint = 색 · 알파
//   _WeatherTime = 흐르는 초(멈춘 화면에서도 같은 값으로 그린다). 땅 아래로 내려가면 다시 위에서 떨어진다. 건물 뒤로 지나가면 가려진다.
//   바람(2026-11-24, HongdalWind.cginc): 실제로 떨어지는 빠르기(_Shape2.x, 비 약 8 m/s · 눈 약 1 m/s)에 견준 바람 세기만큼 비껴 내리고,
//   돌풍 띠가 지나가는 곳은 더 눕는다. 눈은 돌풍에 휘돌며 흩날린다.
Shader "Hongdal/Particle"
{
    Properties
    {
        _Area ("Area", Vector) = (300, 100, 60, 6)
        _Shape ("Shape", Vector) = (0.6, -4, 0, 0)
        _Tint ("Tint", Color) = (1, 1, 1, 0.5)
        _WeatherTime ("Time", Float) = 0
        _Shape2 ("Shape2", Vector) = (8, 0, 0, 0)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "HongdalWind.cginc"

            float4 _Area;
            float4 _Shape;
            float4 _Tint;
            float _WeatherTime;
            float4 _Shape2;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float2 uv2 : TEXCOORD1; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float fade : TEXCOORD1; };

            v2f vert(appdata v)
            {
                v2f o;
                float side = _Area.x, top = _Area.y, speed = _Area.z, len = _Area.w;
                float snow = _Shape.w;
                float a = v.uv2.x, b = v.uv2.y, c = v.color.r, d = v.color.g;
                float rate = speed * (0.85 + 0.3 * d);
                float cycle = _WeatherTime * rate / top + c;
                float fall = frac(cycle);
                float3 p = float3((a - 0.5) * side, top * (1.0 - fall), (b - 0.5) * side);
                float2 dir = _HongdalWind.xy;
                float3 anchor = mul(unity_ObjectToWorld, float4(p.x, 0.0, p.z, 1.0)).xyz;
                float g = HongdalGust(anchor.xz);
                float slant = min(_HongdalWind.z * (0.55 + 0.9 * g) / max(_Shape2.x, 0.3), lerp(0.9, 2.2, snow));   // 바람 ÷ 떨어지는 빠르기
                float across = top * fall * slant;
                p.xz += dir * (across - 0.5 * top * slant);                                       // 바람에 비껴 떨어진다(가운데가 화면에 머물게)
                p.x += snow * _Shape.z * (1.0 + 1.5 * g) * sin(_WeatherTime * (0.6 + d) + c * 40.0);     // 눈: 돌풍에 휘돈다
                p.z += snow * _Shape.z * (1.0 + 1.5 * g) * 0.6 * cos(_WeatherTime * (0.5 + c) + d * 33.0);
                float3 velocity = normalize(float3(dir.x * slant, -1.0, dir.y * slant));
                float3 world = mul(unity_ObjectToWorld, float4(p, 1.0)).xyz;
                float3 view = mul(UNITY_MATRIX_V, float4(world, 1.0)).xyz;
                float2 corner = v.uv * 2.0 - 1.0;
                if (snow < 0.5)
                {
                    float3 tail = mul((float3x3)UNITY_MATRIX_V, velocity);
                    float2 along = normalize(tail.xy + float2(0.0001, 0.0));
                    float2 across = float2(-along.y, along.x);
                    view.xy += along * (corner.y * 0.5 * len) + across * (corner.x * 0.5 * _Shape.x);
                }
                else
                {
                    view.xy += corner * 0.5 * _Shape.x * (0.6 + 0.8 * d);
                }
                o.pos = mul(UNITY_MATRIX_P, float4(view, 1.0));
                o.uv = v.uv;
                o.fade = smoothstep(0.0, 0.08, fall) * (1.0 - smoothstep(0.9, 1.0, fall));
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = i.uv * 2.0 - 1.0;
                float a = _Shape.w < 0.5
                    ? pow(saturate(1.0 - abs(p.x)) * saturate(1.0 - abs(p.y)), 0.7)      // 빗줄기: 가운데가 진하고 끝이 옅다
                    : pow(saturate(1.0 - length(p)), 1.6);                                // 눈송이: 둥글고 부드럽다
                return fixed4(_Tint.rgb, _Tint.a * a * i.fade);
            }
            ENDCG
        }
    }
}
