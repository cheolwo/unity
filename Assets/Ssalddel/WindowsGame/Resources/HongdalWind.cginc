// 바람(2026-11-24, 사용자: 「바람에 흩날리는 비 · 나뭇잎」). 동 디오라마의 나무 · 날리는 잎 · 비 · 눈이 함께 쓴다.
//   _HongdalWind = (바람이 가는 쪽 x 동, z 북, 풍속 m/s, 돌풍 0~1) · _HongdalWindTime = 흐르는 초.
//   값은 SagajeongDispatchEnvironment.cs가 매 화면 알린다(서울 평년 풍속 × 날마다 흔들림, 방향은 규칙).
//   돌풍은 바람을 따라 흘러가는 넓은 띠다: 숲 위로 물결이 지나가듯 나무가 차례로 기운다.
#ifndef HONGDAL_WIND_INCLUDED
#define HONGDAL_WIND_INCLUDED

float4 _HongdalWind;
float _HongdalWindTime;

float HongdalGust(float2 xz)
{
    float2 dir = _HongdalWind.xy;
    float speed = max(_HongdalWind.z, 0.3);
    float along = dot(xz, dir);
    float across = dot(xz, float2(-dir.y, dir.x));
    float t = _HongdalWindTime;
    float w1 = sin(along * 0.06 - t * speed * 0.06 * 1.4 + sin(across * 0.025) * 1.7) * 0.5 + 0.5;
    float w2 = sin(along * 0.15 - t * speed * 0.15 * 1.2 + across * 0.04) * 0.5 + 0.5;
    return saturate((w1 * 0.7 + w2 * 0.3) * (0.55 + 0.9 * _HongdalWind.w) - 0.15);
}

// 나무 흔들림(m): 바람 쪽으로 기울고(풍속 · 돌풍만큼) 제자리에서 떨린다. h = 밑동 0 ~ 꼭대기 1, phase = 나무마다 다른 0~1.
float3 HongdalSway(float2 xz, float h, float phase)
{
    float speed = _HongdalWind.z;
    float2 dir = _HongdalWind.xy;
    float g = HongdalGust(xz);
    float lean = speed * (0.14 + 0.30 * g) * h * h;                                        // 표현: 눈에 보이게 실제보다 크게
    float flutter = sin(_HongdalWindTime * (1.7 + 0.35 * speed) + phase * 6.2832) * (0.10 + 0.06 * speed) * h * (0.4 + g);
    float side = sin(_HongdalWindTime * (2.3 + 0.2 * speed) + phase * 17.0) * 0.5 * flutter;
    return float3(dir.x * (lean + flutter) - dir.y * side, 0.0, dir.y * (lean + flutter) + dir.x * side);
}

#endif
