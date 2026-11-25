// HongdalDiorama.shader의 본문(2026-11-24에 떼어 냄). Built-in(CGPROGRAM + UnityCG)과 URP(S2 시험 갈래, HLSLPROGRAM + URP Core)가 같은 본문을 쓴다.
// 포함하는 쪽이 정한다: HONGDAL_TEX(이름) · HONGDAL_SAMPLE(이름, uv) · UnityObjectToClipPos · UnityObjectToViewPos(URP는 호환 함수),
// HONGDAL_SUN_VISIBILITY(월드 좌표) = 해가 보이는 정도 0~1(Built-in은 1, URP는 주 광원 그림자 맵). 셰이더 설명은 HongdalDiorama.shader 머리말.
#ifndef HONGDAL_SUN_VISIBILITY
#define HONGDAL_SUN_VISIBILITY(w) 1.0
#endif

float _Dim;
float _Grey;
float _Lit;
HONGDAL_TEX(_LightMap);
float4 _LightRect;
float4 _Window;
HONGDAL_TEX(_ActivityMap);
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
HONGDAL_TEX(_HongdalLampMap);
HONGDAL_TEX(_HongdalRoofTex);    // 현실감 G3: 옥상 재질 무늬(r 역청 · g 골판 밝기 · b 골판 기울기, Poly Haven CC0 bitumen · corrugated_iron 1K에서 만든 512 칸)
float4 _HongdalLampRect;
float _HongdalLamp;
float4 _HongdalTier;
HONGDAL_TEX(_HongdalDataMap);      // 생활 자료 바닥 그림(길 속도 · 버스, SagajeongDispatchStreetProps.cs). 메시 좌표로 읽는 색 그림
float4 _HongdalDataRect;
float _HongdalData;
float _HongdalNight;            // 0~1. 그날 점등 시각에 맞춘 밤(자료 창 「낮과 밤」). 켜진 창 · 가게 · 길가 불빛 · 빛만 남고 가라앉는다
float4 _HongdalEnvA;            // 계절 바탕(E1): 밤 · 해 낮음 · 해 높이 · 젖음
float4 _HongdalEnvB;            //   벚꽃 · 잎 · 단풍 · 쌓인 눈
float4 _HongdalEnvC;            //   연두 · 내리는 비 · 언 하천 · 켬(0이면 전과 같다)
float4 _HongdalSunTint;         //   햇빛 색
float4 _HongdalEnvD;            //   쌓인 낙엽 · 날리는 벚꽃잎 · 날리는 잎 · 이팝
float4 _HongdalEnvE;            //   진달래 · 철쭉
float4 _HongdalSunDir;          // 현실감 시험: 해 쪽 단위 벡터 · 빛
float4 _HongdalReal;            //   켬 · 그림자 진하기 · 접지 그늘 · 0

struct appdata
{
    float4 vertex : POSITION;
    float4 color : COLOR;
    float2 uv : TEXCOORD0;
    float2 uv2 : TEXCOORD1;
    float2 uv3 : TEXCOORD2;         // 현실감 시험: 건물 갈래 + 씨앗, 실제 높이 m (없으면 0)
};

struct v2f
{
    float4 pos : SV_POSITION;
    float4 color : COLOR;
    float2 uv : TEXCOORD0;
    float2 uv2 : TEXCOORD1;
    float2 ground : TEXCOORD2;
    float3 world : TEXCOORD3;
    float depth : TEXCOORD4;
    float2 bld : TEXCOORD5;
};

v2f vert(appdata v)
{
    v2f o;
    v.vertex.y += step(0.5, _HongdalReal.x) * step(v.uv3.x, 0.5) * v.uv3.y;      // 현실감 · 가 등급: 보도 겹을 올린다(횡단보도 끝은 덜, SagajeongDispatchRealism.cs)
    if (_Window.z > 0.0 && _HongdalTier.z > 0.0)
    {
        float away = smoothstep(_HongdalTier.y * 0.94, _HongdalTier.y * 1.12, distance(v.vertex.xz, _Window.xy));
        v.vertex.y *= lerp(1.0, _HongdalTier.w, away * _HongdalTier.z);      // 역세권 너머의 건물은 낮게
    }
    o.pos = UnityObjectToClipPos(v.vertex);
    o.color = v.color;
    o.uv = v.uv;
    o.uv2 = v.uv2;
    o.ground = v.vertex.xz;
    o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
    o.depth = -UnityObjectToViewPos(v.vertex).z;
    o.bld = v.uv3;
    return o;
}

float hash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

// 부드러운 값 무늬(0~1). 계절 바탕의 얼룩(잎이 먼저 나고 늦게 지는 자리, 눈이 덜 쌓인 자리).
float vnoise(float2 p)
{
    float2 cell = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float h00 = hash21(cell);
    float h10 = hash21(cell + float2(1.0, 0.0));
    float h01 = hash21(cell + float2(0.0, 1.0));
    float h11 = hash21(cell + float2(1.0, 1.0));
    return lerp(lerp(h00, h10, f.x), lerp(h01, h11, f.x), f.y);
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

// 역둘레 B1(2026-11-24): 상자로 거른 줄무늬(주기 1, [lo, hi] 안이 1). 화면 한 점이 여러 칸을 덮으면 그 안의 평균(덮인 비율)이 되어
// 창 무늬가 자글거리지 않고 벽 결로 녹는다. w = 화면 한 점이 덮는 칸 수(fwidth, 분기 밖에서 잰다).
float PulseInt(float x, float lo, float hi)
{
    return floor(x) * (hi - lo) + clamp(frac(x) - lo, 0.0, hi - lo);
}

float FilteredPulse(float x, float w, float lo, float hi)
{
    w = max(w, 1e-4);
    return saturate((PulseInt(x + 0.5 * w, lo, hi) - PulseInt(x - 0.5 * w, lo, hi)) / w);
}

// 역둘레 B3: 멀리서의 지붕 색(가안 「건축 모형」). 갈래마다 한 색, 건물마다 조금씩 밝기가 다르다. 잔 얼룩 없이 덩어리로 읽힌다(표현).
float3 RoofFamily(float kind, float s)
{
    float3 m;
    if (kind > 1.5 && kind < 2.5) m = float3(0.47, 0.62, 0.52);                                       // 초록 지붕: 옅은 풀빛
    else if (kind > 2.5 && kind < 3.5) m = float3(0.86, 0.87, 0.88);                                  // 아파트: 흰 덩어리
    else if (kind > 3.5 && kind < 4.5) m = float3(0.84, 0.82, 0.78);                                  // 상가: 따뜻한 밝은 회색
    else if (kind > 4.5 && kind < 5.5) m = float3(0.80, 0.82, 0.78);                                  // 공공
    else if (kind > 5.5) m = float3(0.62, 0.69, 0.74);                                                // 공장 · 창고: 푸른 회색
    else m = lerp(float3(0.80, 0.78, 0.74), float3(0.72, 0.71, 0.69), step(0.55, s));                 // 주택: 회백 두 가지
    return m * (0.95 + 0.10 * frac(s * 7.13));
}

// 현실감 시험: 벽 재질(표현). 갈래와 씨앗으로 고르고, 자리마다 옅은 얼룩.
float3 WallMaterial(float kind, float s, float3 world, float keep)
{
    float3 m;
    if (kind > 2.5 && kind < 3.5) m = lerp(float3(0.80, 0.79, 0.76), float3(0.72, 0.73, 0.74), step(0.7, s));                      // 아파트: 흰 · 옅은 회색
    else if (kind > 3.5 && kind < 4.5) m = s < 0.4 ? float3(0.70, 0.71, 0.72) : (s < 0.7 ? float3(0.74, 0.67, 0.57) : float3(0.78, 0.77, 0.74));   // 상가
    else if (kind > 4.5 && kind < 5.5) m = float3(0.75, 0.74, 0.70);                                                               // 공공
    else if (kind > 5.5) m = float3(0.64, 0.67, 0.68);                                                                             // 공장 · 창고
    else m = s < 0.30 ? float3(0.79, 0.77, 0.73) : (s < 0.50 ? float3(0.74, 0.65, 0.52) : (s < 0.78 ? float3(0.58, 0.33, 0.26) :
             (s < 0.90 ? float3(0.47, 0.34, 0.27) : float3(0.64, 0.66, 0.68))));                                                  // 주택: 흰 · 베이지 타일 · 붉은 벽돌 · 갈색 벽돌 · 회색
    float stain = vnoise(float2((world.x + world.z) / 1.7, world.y / 5.0) + s * 37.0);
    return m * (0.92 + 0.12 * lerp(0.5, stain, keep));      // 역둘레 B3: 얼룩이 화면 한두 점보다 잘아지면 평균으로(모래알 방지)
}

// 현실감 시험: 지붕(표현). 초록 우레탄 · 콘크리트 · 흰 방수 · 골강판.
float3 RoofMaterial(float kind, float s, float3 baked, float2 ground, float keep)
{
    float n = vnoise(ground / 3.0 + s * 11.0);
    float3 m;
    if (kind > 1.5 && kind < 2.5) m = float3(0.30, 0.53, 0.41);
    else if (kind > 5.5) m = float3(0.50, 0.58, 0.64) * (0.93 + 0.1 * step(0.5, frac(dot(ground, float2(0.6, 0.8)) / 0.9)));
    else if (kind > 2.5 && kind < 3.5) m = float3(0.72, 0.73, 0.74);
    else m = lerp(float3(0.62, 0.63, 0.63), float3(0.77, 0.77, 0.76), step(0.62, s));
    m *= 0.90 + 0.14 * lerp(0.5, n, keep);
    return lerp(baked, m, 0.75);
}

// 현실감 시험: 창 속의 방(interior mapping 약식, van Dongen 2008). 창 안의 자리 pf에서 보는 방향으로 상자 방 안을 따라가
// 처음 닿는 면(뒷벽 · 옆벽 · 바닥 · 천장)의 색을 쓴다. 방마다 빛 색 · 가구 · 커튼이 다르다(표현).
float3 RoomColour(float2 pf, float3 nrm, float3 viewDir, float r, float3 warm)
{
    float3 t = normalize(cross(float3(0.0, 1.0, 0.0), nrm) + float3(1e-5, 0.0, 0.0));
    float3 d = float3(dot(viewDir, t), viewDir.y, max(-dot(viewDir, nrm), 0.05));
    float3 o = float3(pf, 0.0);
    float3 size = float3(1.0, 1.0, 1.3);
    float3 safeD = d + (step(0.0, d) * 2.0 - 1.0) * 1e-4;
    float3 tt = (step(0.0, d) * size - o) / safeD;
    float tm = min(min(tt.x, tt.y), tt.z);
    float3 h = o + d * tm;
    float3 light = warm * lerp(0.92, 1.08, r);
    float3 col;
    if (tm == tt.z)
    {
        col = light * 0.80 * (0.88 + 0.12 * h.y);                                                 // 뒷벽
        col *= 1.0 - 0.5 * step(0.45, r) * step(h.y, 0.36) * step(abs(h.x - frac(r * 7.3)), 0.26);   // 가구
    }
    else if (tm == tt.y) col = d.y < 0.0 ? light * float3(0.66, 0.50, 0.37) : light * 1.06;        // 바닥(나무빛) · 천장
    else col = light * 0.64;                                                                    // 옆벽
    col *= lerp(1.0, 0.72, saturate(h.z / size.z));
    float curtain = step(r, 0.3) * step(r < 0.15 ? pf.x : 1.0 - pf.x, 0.3);
    return lerp(col, light * float3(0.96, 0.88, 0.76), curtain);
}

float4 frag(v2f i) : SV_Target
{
    if (i.uv2.y < -0.5 && i.uv2.y > -1.5)
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
    // 현실감 시험: 면의 방향(화면 미분)과 건물 꼬리표(uv3). realOn이 0이면 전과 같다.
    float realOn = step(0.5, _HongdalReal.x);
    float3 nrm = normalize(cross(ddy(i.world), ddx(i.world)) + float3(0.0, 1e-6, 0.0));
    float3 toCam = unity_OrthoParams.w > 0.5 ? UNITY_MATRIX_V[2].xyz : normalize(_WorldSpaceCameraPos - i.world);
    nrm = dot(nrm, toCam) < 0.0 ? -nrm : nrm;
    float3 viewDir = -toCam;
    float kind = floor(i.bld.x + 0.0005);
    float bseed = frac(i.bld.x);
    float isRim = step(i.uv2.y, -0.5) * step(-1.5, i.uv2.y);
    float isProp = step(i.uv2.y, -2.5);
    float topWall = step(i.uv2.y, -4.5) * step(-5.5, i.uv2.y);                                   // 옥탑 벽
    // G3 옥상 무늬: 갈래와 상관없이 먼저 읽어 둔다(분기 안에서 읽으면 밉 고르기가 흔들린다). 지붕의 uv = 건물 긴 쪽 기준 m(SagajeongDispatchRealism.cs)
    float2 roofShift = float2(bseed * 37.0, bseed * 91.0);
    float4 roofFine = HONGDAL_SAMPLE(_HongdalRoofTex, (i.uv + roofShift) / 9.0);
    float4 roofWide = HONGDAL_SAMPLE(_HongdalRoofTex, (i.uv + roofShift * 0.37) / 23.0);
    float4 roofRib = HONGDAL_SAMPLE(_HongdalRoofTex, (i.uv + roofShift) / 1.12);
    // 역둘레 B1~B3(2026-11-24 11:24 사용자: 「한번 진행해봐」 · 가안 「건축 모형」): 멀어질수록 잔 무늬를 거르고 덩어리 · 윤곽 · 명암으로 읽히게.
    //  farK 0 = 가까이(가로 200 m, 전과 같다) · 0.3 = 중간(560 m) · 1 = 멀리(900 m 넘게). 현실감을 끄면 0. 화면 미분은 분기 밖에서 미리 잰다.
    float farK = realOn * saturate((0.8 - _HongdalClose) / 0.8);
    float2 stainW = fwidth(float2((i.world.x + i.world.z) / 1.7, i.world.y / 5.0));
    float keepWall = 1.0 - saturate((max(stainW.x, stainW.y) - 0.25) / 0.5);
    float2 roofNoiseW = fwidth(i.ground / 3.0);
    float keepRoof = 1.0 - saturate((max(roofNoiseW.x, roofNoiseW.y) - 0.25) / 0.5);
    if (realOn > 0.0 && kind > 0.5)
    {
        float wallLike = saturate(i.uv2.x + isRim + topWall) * (1.0 - step(0.5, abs(nrm.y)));
        if (wallLike > 0.5)
        {
            float2 side = normalize(nrm.xz + float2(1e-5, 0.0));
            float baked = topWall > 0.5 ? 1.0 : 0.60 + 0.40 * max(0.0, dot(side, float2(-0.931, -0.364)));   // 구울 때 넣은 고정 빛을 덜어 낸다
            c = lerp(c / baked, WallMaterial(kind, bseed, i.world, keepWall), 0.78);
        }
        else if (nrm.y > 0.5 && isProp < 0.5 && i.world.y > 0.5 && i.uv2.x < 0.5)
        {
            c = RoofMaterial(kind, bseed, c, i.ground, keepRoof);
            c = lerp(c, RoofFamily(kind, bseed), 0.75 * farK);                                       // 역둘레 B3: 멀리서는 갈래 색 하나로(건물마다 조금씩)
            float detail;
            if (kind > 5.5) detail = lerp(1.0, (0.72 + 0.56 * roofRib.b) * (0.8 + 0.4 * roofRib.g), 0.6);      // 공장 · 창고: 골판 1.12 m, 긴 쪽을 따라 골이 선다
            else detail = 1.0 + ((roofFine.r - 0.5) * 0.45 + (roofWide.r - 0.5) * 0.8) * (kind > 1.5 && kind < 2.5 ? 0.30 : 0.40);   // 평지붕: 얼룩 · 해짐(g1e: 역청 결이 줄무늬로 읽혀 잔 무늬를 더 낮춤)
            c *= lerp(1.0, detail, _HongdalReal.w);
        }
    }
    // 계절 바탕 (E1). 창 · 가게 · 빛보다 먼저, 바탕 색에만 입힌다.
    float envWet = 0.0;
    if (_HongdalEnvC.w > 0.0)
    {
        float isWall = i.uv2.x;
        float roofish = (1.0 - isWall) * step(0.5, i.world.y) * lerp(1.0, step(0.5, nrm.y), realOn);   // 지붕 · 지붕 테두리(현실감: 윗면만)
        float flatMask = (1.0 - isWall) * step(i.world.y, 0.2);                              // 땅 · 녹지 · 골목 · 길
        float kindTag = i.uv.x * step(i.world.y, 0.05) * (1.0 - isWall);
        float riverMask = step(2.5, kindTag) * step(kindTag, 3.5);
        float vegMask = step(0.5, kindTag) * step(kindTag, 4.5) * (1.0 - riverMask);    // 산림지 · 조경녹지 · 경작지
        float n1 = vnoise(i.ground / 4.0);
        float n2 = vnoise(i.ground / 13.0 + 19.7);
        float n3 = hash21(floor(i.ground / 1.6) + 5.3);
        float pine = step(0.66, vnoise(i.ground / 7.0 + 41.0)) * step(kindTag, 1.5);              // 산림지의 소나무 자리(표현): 늘 푸르다
        if (vegMask > 0.0)
        {
            float tone = dot(c, float3(0.299, 0.587, 0.114));
            float shade = saturate(tone / 0.26) * (0.86 + 0.28 * n1);
            float field = step(3.5, kindTag);
            float3 bare = lerp(float3(0.25, 0.21, 0.17), float3(0.31, 0.27, 0.18), field) * shade;          // 맨 가지 · 마른 풀 · 빈 밭
            float3 freshC = float3(0.36, 0.52, 0.21) * shade;                                                // 연두
            float3 deep = lerp(c * float3(0.92, 1.12, 0.95), float3(0.12, 0.30, 0.17) * shade, 0.5);       // 짙은 녹색
            float3 leafC = lerp(deep, freshC, _HongdalEnvC.x);
            float hue = vnoise(i.ground / 6.0 + 7.7);
            float3 autumnC = hue < 0.38 ? float3(0.62, 0.51, 0.15) : (hue < 0.70 ? float3(0.60, 0.33, 0.13) : float3(0.50, 0.17, 0.12));   // 은행 · 주황 · 붉은
            autumnC = lerp(autumnC, float3(0.58, 0.48, 0.21), field) * shade * 1.05;                                // 경작지: 누런 들
            leafC = lerp(leafC, autumnC, saturate(_HongdalEnvB.z * 1.5 - 0.5 * n1));                                 // 얼룩지며 물든다
            float3 veg = lerp(bare, leafC, saturate(_HongdalEnvB.y * 1.35 - 0.35 * (1.0 - n1)));                    // 얼룩지며 나고 진다
            veg = lerp(veg, float3(0.11, 0.25, 0.17) * shade, pine * 0.85);
            float dot16 = step(length(frac(i.ground / 1.6) - 0.5), 0.40);                                                 // 네모 칸이 아니라 둥근 점으로
            float dot11 = step(length(frac(i.ground / 1.1) - 0.5), 0.38);
            float speck = step(1.0 - 0.55 * saturate(_HongdalEnvB.x), n3) * step(0.55, n2) * step(kindTag, 2.5) * (1.0 - pine) * dot16;   // 벚꽃: 몇 군데에 모여 핀다
            veg = lerp(veg, float3(0.96, 0.76, 0.84) * (0.82 + 0.22 * n1), speck * saturate(_HongdalEnvB.x * 2.0));
            float n4 = hash21(floor(i.ground / 1.1) + 13.7);
            float azalea = step(1.0 - 0.40 * _HongdalEnvE.x, n4) * step(0.45, n1) * step(kindTag, 1.5) * (1.0 - pine) * dot11;        // 진달래(산)
            veg = lerp(veg, float3(0.84, 0.46, 0.72) * (0.85 + 0.2 * n2), azalea);
            float royal = step(1.0 - 0.55 * _HongdalEnvE.y, n4) * step(0.40, n2) * step(1.5, kindTag) * step(kindTag, 2.5) * dot11;    // 철쭉(공원 · 화단)
            veg = lerp(veg, lerp(float3(0.88, 0.26, 0.52), float3(0.92, 0.55, 0.70), step(0.7, n1)), royal);
            float3 litterC = n2 < 0.4 ? float3(0.58, 0.46, 0.16) : (n2 < 0.75 ? float3(0.52, 0.30, 0.13) : float3(0.40, 0.25, 0.14));
            veg = lerp(veg, litterC * shade * 1.2, _HongdalEnvD.x * 0.55 * (1.0 - _HongdalEnvB.y) * (1.0 - pine));            // 숲 바닥 낙엽
            c = lerp(c, veg, vegMask);
        }
        if (riverMask > 0.0)
        {
            float3 water = lerp(c, c * float3(0.92, 0.86, 0.70) + float3(0.05, 0.04, 0.01), saturate(_HongdalEnvC.y + 0.5 * _HongdalEnvA.w) * 0.8);   // 비: 흐린 물
            water = lerp(water, float3(0.58, 0.66, 0.72) * (0.9 + 0.15 * n1), _HongdalEnvC.z * 0.8);                                           // 한겨울: 언 물
            c = lerp(c, water, riverMask);
        }
        envWet = _HongdalEnvA.w * flatMask * (1.0 - riverMask);
        float puddle = smoothstep(0.60, 0.78, vnoise(i.ground / 2.2 + 3.1)) * envWet;
        c *= 1.0 - 0.28 * envWet;                                                       // 젖은 땅은 어둡다
        // 가까이: 보도 · 시설물대에 낙엽과 꽃잎이 점점이(위치는 표현)
        float walk = (step(4.5, streetTag) * step(streetTag, 5.5) + step(8.5, streetTag)) * saturate((_HongdalClose - 0.2) / 0.4);
        if (walk > 0.0)
        {
            float bit = hash21(floor(i.ground / 0.42) + 9.1) * step(length(frac(i.ground / 0.42) - 0.5), 0.42);
            float hue = hash21(floor(i.ground / 0.42) + 2.3);
            float3 fallen = hue < 0.4 ? float3(0.86, 0.70, 0.18) : (hue < 0.75 ? float3(0.74, 0.38, 0.14) : float3(0.55, 0.32, 0.16));
            c = lerp(c, fallen, step(1.0 - 0.32 * _HongdalEnvD.x, bit) * walk * (1.0 - _HongdalEnvB.w));
            c = lerp(c, float3(0.97, 0.82, 0.88), step(1.0 - 0.22 * _HongdalEnvD.y, hash21(floor(i.ground / 0.3) + 4.4)) * walk);
        }
        c = lerp(c, float3(0.20, 0.26, 0.32), puddle * 0.55);                           // 웅덩이에 하늘빛
        float snow = _HongdalEnvB.w;
        float roadY = step(0.045, i.world.y) * step(i.world.y, 0.08) * (1.0 - isWall); // 차도 겹(0.06 m)
        float take = flatMask * (1.0 - 0.65 * roadY) * (1.0 - riverMask * (1.0 - _HongdalEnvC.z)) + 0.85 * roofish;
        float cover = saturate((snow * 1.25 - 0.45 * n1 - 0.18 * n3 + 0.15) * 2.2) * saturate(take) * step(0.001, snow);
        float3 snowC = float3(0.71, 0.75, 0.80) * (0.93 + 0.07 * n3) * lerp(1.0, 0.90, roofish);   // 눈(바탕보다 너무 밝지 않게, 지붕은 조금 더 낮게)
        snowC = lerp(snowC, float3(0.50, 0.53, 0.56), roadY * 0.6);                      // 차도: 치운 뒤의 진눈깨비
        c = lerp(c, snowC, cover);
        c *= realOn > 0.0 ? float3(1.0, 1.0, 1.0) : _HongdalSunTint.rgb;                 // 햇빛 색(현실감: 아래에서 면마다)
    }
    if (realOn > 0.0)
    {
        // 현실감 시험: 해 쪽 면은 햇빛 색, 반대쪽은 하늘빛 그늘. 해가 없으면(밤 · 해 질 녘) 전처럼 햇빛 색만.
        float diff = saturate(dot(nrm, _HongdalSunDir.xyz)) * HONGDAL_SUN_VISIBILITY(i.world);     // URP: 실제 그림자 맵
        float3 sunC = _HongdalEnvC.w > 0.0 ? _HongdalSunTint.rgb : float3(1.0, 1.0, 1.0);
        float amb = 0.56 + farK * step(0.5, kind) * lerp(-0.11, 0.06, step(0.5, nrm.y));          // 역둘레 B2: 멀리서 건물은 벽 그늘을 깊게 · 윗면을 밝게(모형처럼 면이 갈린다)
        float3 lightC = amb * float3(0.90, 0.95, 1.06) + 0.48 * diff * sunC;
        float flatTag = i.uv.x * step(i.world.y, 0.05) * (1.0 - i.uv2.x);
        float pave = (1.0 - i.uv2.x) * step(i.world.y, 0.2) * (1.0 - step(0.5, flatTag) * step(flatTag, 4.5));   // 길 · 땅(녹지 · 물 빼고)
        float3 paveGrey = lerp(c, dot(c, float3(0.299, 0.587, 0.114)).xxx * float3(0.97, 0.99, 1.02), 0.6) * 1.35 + 0.025;
        c = lerp(c, paveGrey, pave * _HongdalSunDir.w);                                          // 낮의 길은 밝은 회색(시안처럼). 그래야 그늘이 보인다
        c *= 1.0 - 0.1 * _HongdalClose;                                                           // 가까이: 노출을 조금 낮춘다
        c *= lerp(sunC, lightC, _HongdalSunDir.w);
        c *= lerp(1.0, lerp(0.70, 1.0, smoothstep(0.0, 3.5, i.uv.y)), i.uv2.x * (1.0 - _HongdalClose));    // 벽 밑 접지 그늘
    }
    float shop = step(999.5, i.uv2.y) * i.uv2.x;                    // 근린생활시설의 벽
    float seed = i.uv2.y - 1000.0 * shop;                             // 창 불빛 씨앗 (전과 같다)
    float front = shop * saturate((_HongdalClose - 0.3) / 0.5) * step(i.uv.y, 3.0);     // 1층 가게: 가까이에서만
    float2 cell = float2(i.uv.x / 2.6, i.uv.y / 3.0);
    float2 f = frac(cell);
    float tierOn = step(0.001, _Window.z) * _HongdalTier.z;
    float tierAt = distance(i.ground, _Window.xy);
    float tierCore = tierOn * (1.0 - smoothstep(_HongdalTier.x * 0.8, _HongdalTier.x * 1.15, tierAt));      // 역 핵심
    float tierFar = tierOn * smoothstep(_HongdalTier.y * 0.94, _HongdalTier.y * 1.12, tierAt);               // 역세권 너머
    float apt = realOn * step(2.5, kind) * step(kind, 3.5);                                       // 현실감: 아파트는 넓은 발코니 창
    float2 winLo = lerp(float2(0.26, 0.30), float2(0.08, 0.30), apt);
    float2 winHi = lerp(float2(0.74, 0.72), float2(0.92, 0.84), apt);
    float2 cellW = fwidth(cell);                                                                  // 화면 한 점이 덮는 창 칸 수
    float winBlur = realOn * saturate((max(cellW.x, cellW.y) - 0.5) / 0.5);                       // 역둘레 B1: 0 = 창 하나하나 · 1 = 창 무늬가 벽 결로 녹는다(창이 화면 한 점 아래로 작아질 때. 1차 0.3은 중간 거리 아파트 창까지 녹였다)
    float winShape = step(winLo.x, f.x) * step(f.x, winHi.x) * step(winLo.y, f.y) * step(f.y, winHi.y);
    if (realOn > 0.0) winShape = FilteredPulse(cell.x, cellW.x, winLo.x, winHi.x) * FilteredPulse(cell.y, cellW.y, winLo.y, winHi.y);   // 상자로 거른 창(덮인 비율)
    float win = winShape * i.uv2.x * (1.0 - front) * (1.0 - 0.85 * tierFar);
    float glow = HONGDAL_SAMPLE(_LightMap, (i.ground - _LightRect.xy) * _LightRect.zw).r;
    float4 act = HONGDAL_SAMPLE(_ActivityMap, (i.ground - _ActivityRect.xy) * _ActivityRect.zw) * _ActivityOn;
    float sway = dot(i.ground, float2(0.02, 0.014));        // 자리에 따라 숨이 엇갈린다 (약 260 m에 한 바퀴)
    act.r *= 1.0 + 0.32 * sin(_ActivityBeat * 4.2 + sway);          // 기사: 빠르게
    act.g *= 1.0 + 0.12 * sin(_ActivityBeat * 2.3 + sway * 0.7);    // 음식점: 천천히, 잔잔하게(빛 한 점이 빛마다 엇갈려 숨 쉰다)
    act.b *= 1.0 + 0.14 * sin(_ActivityBeat * 3.1 + sway * 1.3);    // 전달지: 잔잔하게
    act.a *= 1.0 + 0.16 * sin(_ActivityBeat * 1.3 + sway * 0.5);    // 온기: 아주 천천히
    act.rgb *= 1.0 + _ActivityPulse;        // 고른 곡이 나오는 동안: 저음에 맞춰 함께 밝아진다 (곡이 없으면 0)
    glow = max(glow, saturate(max(max(act.r, act.g), act.b) + 0.6 * act.a));      // 빛이 닿은 곳은 회색에서 깨어난다
    glow = max(glow, 0.45 * tierCore);      // 역 핵심은 회색에서 반쯤 깨어 있다(표현값. 나중에 역 단계에 묶는다)
    float dull = _Grey * (1.0 - glow);      // 0 = 그대로(회색 동네가 아니거나 내 건물 곁, 빛이 닿은 곳), 1 = 회색
    float nature = step(0.5, i.uv.x) * step(i.uv.x, 4.5) * step(i.world.y, 0.05) * (1.0 - i.uv2.x);      // 녹지 한 겹 (계1). 5~9는 거리 겹
    dull *= 1.0 - 0.7 * nature;
    // 가까이에서는 벽마다 켜진 창의 비율이 다르고, 창마다 빛의 색과 밝기가 조금씩 다르다. 멀리서는 전과 같다.
    float ratio = _Lit * lerp(1.0, 0.5 + 1.4 * hash21(float2(seed, 7.3)), _HongdalClose) * (1.0 + 1.5 * tierCore);
    float lit = step(1.0 - ratio, hash21(floor(cell) + seed)) * (1.0 - dull) * (1.0 - tierFar);
    float litAvg = saturate(ratio) * (1.0 - dull) * (1.0 - tierFar);                              // 역둘레 B1: 멀리서는 켜진 창의 평균
    float3 warm = lerp(float3(1.0, 0.84, 0.55), float3(0.86, 0.93, 1.0), step(0.82, hash21(floor(cell) * 1.3 + seed)) * _HongdalClose);
    warm *= lerp(1.0, 0.74 + 0.46 * hash21(floor(cell) * 2.1 + seed * 3.0), _HongdalClose);
    float3 pane = lerp(c * 0.45 + float3(0.09, 0.13, 0.18) * 0.55, warm, lit);
    if (realOn > 0.0 && win > 0.0)
    {
        float2 pf = saturate((f - winLo) / (winHi - winLo));                                    // 창 안의 자리 0~1
        float glint = hash21(floor(cell) * 1.3 + seed * 0.7);
        float3 glass = lerp(float3(0.17, 0.22, 0.28), float3(0.44, 0.54, 0.64), saturate(0.25 + 0.75 * pf.y)) * (0.82 + 0.3 * glint);   // 하늘이 비친 유리
        glass *= lerp(0.55, 1.0, _HongdalSunDir.w);
        float3 room = RoomColour(pf, nrm, viewDir, hash21(floor(cell) * 1.9 + seed + 3.1), warm);
        pane = lerp(lerp(c * 0.25, glass, 0.8), room, lit);
        float frameK = (1.0 - step(0.07, pf.x) * step(pf.x, 0.93) * step(0.08, pf.y) * step(pf.y, 0.92)) * saturate((_HongdalClose - 0.15) / 0.4) * (1.0 - 0.7 * lit);
        pane = lerp(pane, lerp(float3(0.78, 0.79, 0.78), c, 0.3), frameK * 0.85);               // 창틀(가까이)
        float3 paneAvg = lerp(lerp(c * 0.25, float3(0.33, 0.41, 0.49) * lerp(0.55, 1.0, _HongdalSunDir.w), 0.8), warm * 0.72, litAvg);
        pane = lerp(pane, paneAvg, winBlur);                                                     // 역둘레 B1: 멀리서는 칸마다 다른 창 대신 평균
    }
    c = lerp(c, pane, win);
    if (apt > 0.0) c = lerp(c, c * 1.1 + 0.04, step(f.y, 0.14) * i.uv2.x * (1.0 - front));       // 아파트: 층마다 난간 띠
    // 역둘레 B1: 층 띠. 주택 · 상가 · 공공 벽에 층마다 바닥판 그늘(0.2 m). 걸러서 멀리서는 옅게 녹는다
    float slab = realOn * (1.0 - apt) * step(0.5, kind) * i.uv2.x * (1.0 - front) * step(2.5, i.uv.y) * FilteredPulse(cell.y, cellW.y, 0.0, 0.07);
    c *= 1.0 - 0.16 * slab;
    // 역둘레 B2: 처마 띠. 벽 맨 위에 밝은 갓돌 한 줄, 그 밑에 옅은 그늘 한 줄(uv.y = 높이 m, bld.y = 실제 높이 m).
    //  멀어져도 화면 한 점보다 가늘어지지 않아 건물마다 윗선이 남는다(윤곽)
    float eaveW = max(fwidth(i.uv.y), 1e-3);
    float capH = max(0.30, 0.9 * eaveW);
    float eaveOn = realOn * step(0.5, kind) * i.uv2.x * step(2.0, i.bld.y);
    float cap = saturate((i.uv.y - (i.bld.y - capH)) / eaveW + 0.5) * eaveOn;
    float under = saturate((i.uv.y - (i.bld.y - 2.0 * capH)) / eaveW + 0.5) * (1.0 - cap) * eaveOn;
    c = lerp(c, c * 1.22 + 0.045, cap);
    c *= 1.0 - 0.22 * under;
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
    float roofTop = step(0.5, nrm.y) * step(0.5, i.world.y) * (1.0 - i.uv2.x) * step(0.5, kind);   // 건물 윗면
    float3 greyHi = lerp(float3(0.56, 0.59, 0.62), float3(0.74, 0.76, 0.78), farK * roofTop);     // 역둘레 B2 · 가안 「건축 모형」: 멀리서 회색 동네의 지붕은 회백 덩어리
    float3 grey = lerp(float3(0.055, 0.07, 0.085), greyHi, tone);
    c = lerp(c * (1.0 + 0.3 * glow * _Grey), grey * (1.0 - 0.42 * _ActivityOn), dull);
    float ride = act.r * (0.3 + 0.7 * act.r);       // 기사가 있는 자리가 가장 밝고, 자취는 뒤로 갈수록 빨리 옅어진다
    float3 light = ride * float3(0.78, 0.95, 0.30) + act.g * float3(1.00, 0.60, 0.22) + act.b * float3(0.30, 0.82, 1.00)
        + act.a * float3(0.26, 0.30, 0.12);
    light *= lerp(1.0, lerp(0.55, 0.35, _HongdalNight), realOn * step(0.6, i.world.y));      // 현실감: 업무 빛은 바닥에 고이고 벽 · 지붕은 덜 밝힌다(낮 55% · 밤 35%. g1d 근접 밤이 하얗게, g2p 낮 카페 둘레 벽이 크림색으로 떴다)
    c = c * (1.0 + 0.8 * light) + 0.16 * light;
    float lamp = HONGDAL_SAMPLE(_HongdalLampMap, (i.ground - _HongdalLampRect.xy) * _HongdalLampRect.zw).r * _HongdalLamp;
    lamp *= lerp(step(i.world.y, 0.6), 0.6 * (1.0 - smoothstep(0.5, 5.5, i.uv.y)), i.uv2.x);      // 땅 · 길, 벽은 아래쪽만
    c = c * (1.0 + 0.35 * lamp) + float3(1.0, 0.80, 0.50) * (0.24 * lamp);                          // 길가 불빛(표현): 옅은 따뜻한 흰빛
    c += float3(1.0, 0.80, 0.50) * (0.22 * lamp * envWet);                                          // 젖은 길에 번지는 불빛
    float4 dataTint = HONGDAL_SAMPLE(_HongdalDataMap, (i.ground - _HongdalDataRect.xy) * _HongdalDataRect.zw) * _HongdalData;
    float dataGround = step(i.world.y, 0.6) * (1.0 - i.uv2.x) * _HongdalData;                      // 땅 · 길에만(벽 · 지붕은 아님)
    c = lerp(c, dataTint.rgb, dataTint.a * dataGround * 0.62) + dataTint.rgb * (dataTint.a * dataGround * (0.10 + 0.22 * _HongdalNight));   // 길 바닥이 그 시각의 막힘 색으로 물든다
    float lampHead = step(i.uv2.y, -6.5) * step(-7.5, i.uv2.y) * step(0.01, _HongdalLamp);               // 조사된 보안등 등머리(G4): 켜지면 빛난다
    c = lerp(c, float3(1.0, 0.94, 0.80) * (1.05 + 1.4 * _HongdalNight), lampHead * saturate(0.35 + _HongdalNight));
    float tailLight = step(i.uv2.y, -8.5) * step(-9.5, i.uv2.y);                                      // 차 뒷등(R4): 밤에 붉게
    c = lerp(c, float3(1.0, 0.10, 0.06) * (0.8 + 2.2 * _HongdalNight), tailLight * saturate(0.2 + _HongdalNight));
    float nightKeep = saturate(2.0 * (lampHead + tailLight) + lit * (1.0 - winBlur) * win + 0.9 * front + 1.6 * lamp + 1.2 * max(max(light.r, light.g), light.b) + 0.7 * dataTint.a * dataGround);
    c *= lerp(1.0, lerp(0.30, 1.12, nightKeep), _HongdalNight);                                    // 밤: 불빛만 남고 가라앉는다
    c += float3(0.004, 0.012, 0.035) * _HongdalNight * (1.0 - nightKeep);                          // 밤의 푸른 기운
    c += warm * (0.85 * litAvg * win * winBlur * _HongdalNight);                                   // 역둘레 B1: 멀리서 녹은 창은 켜진 몫만큼 벽이 따뜻하게 빛난다(유리는 어둡게 둔다)
    float tierTone = dot(c, float3(0.299, 0.587, 0.114));
    c = lerp(c, lerp(float3(0.045, 0.06, 0.075), float3(0.30, 0.33, 0.36), saturate(tierTone)), tierFar * 0.9);      // 바깥: 색을 빼고 가라앉힌다
    c = c * (1.0 + 0.34 * tierCore * (1.0 - 0.75 * realOn)) + float3(0.030, 0.024, 0.012) * tierCore * (1.0 - 0.75 * realOn);   // 현실감: 덜 부풀린다                                          // 역 핵심: 조금 밝고 따뜻하게
    c *= _Dim;
    float pickRadius = max(_HongdalPick.z, 0.01);
    float pick = _HongdalPick.w * saturate((pickRadius - distance(i.world.xz, _HongdalPick.xy)) / (pickRadius * 0.25));
    c = lerp(c, c * 1.18 + _HongdalPickColor.rgb * 0.16, pick);        // 고른 곳
    float fog = saturate((i.depth - _HongdalFog.x) / max(1.0, _HongdalFog.y - _HongdalFog.x)) * _HongdalFog.z;
    c = lerp(c, float3(0.055, 0.086, 0.114), fog);                      // 먼 땅은 바탕색으로
    float outside = _Window.z > 0.0 ? saturate((distance(i.ground, _Window.xy) - _Window.z) / max(1.0, _Window.w)) : 0.0;
    clip(0.999 - outside);
    c = lerp(c, float3(0.055, 0.086, 0.114), outside * outside * (3.0 - 2.0 * outside) * 0.92);     // 바탕색(DongBackdrop)으로
    return float4(c, 1);
}
