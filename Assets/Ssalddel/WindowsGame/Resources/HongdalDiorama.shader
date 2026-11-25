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
//   _HongdalDataMap · _HongdalDataRect · _HongdalData: 생활 자료 바닥 그림(길 속도 · 버스, 2026-11-23). _HongdalNight: 점등 시각에 맞춘 밤.
//     땅 · 길과 벽의 아래쪽만 밝힌다. 색은 거의 넣지 않는다(음식점 · 기사의 빛과 헷갈리지 않게). 세기가 0이면 전과 똑같이 그린다.
// 다시 구운 동 메시(품5, 2026-11-15, make_dong_modules.py). 모두 표현값이고 가까이에서만 보인다. 멀리서는 전과 똑같다.
//   uv2.y = -1: 지붕 테두리(난간 0.45 m). _HongdalClose가 0.75 아래면 지운다(0.75~0.95에서 점점이 나타난다).
//   uv2.y >= 1000 (벽): 근린생활시설. 1000을 빼면 전과 같은 창 씨앗이다. 가까이에서 1층에 넓은 유리와 간판 띠(글자 없음)를 그린다.
// 녹지(r221 계1, 2026-11-15, make_dong_modules.py): 땅 한 겹(높이 0.015 m), uv.x = 갈래(1 산림지 · 2 조경녹지 · 3 하천 · 4 경작지).
//   범위는 서울시 생태현황도, 색은 표현. 회색 동네에서도 색을 30 %만 뺀다(시안 3처럼 건물만 회색). 계절 색(계3)은 이 갈래 번호를 쓴다.
// 거리 겹(거리 풍경 보1 · 거1, 2026-11-15, make_dong_modules.py): uv.x 5 보도 · 6 횡단보도 줄 · 7 점자블록 · 8 연석선 · 9 시설물대.
//   정점 색은 그 아래 보이던 색(바닥 · 연석 띠 · 차도)이라 멀리서는 전과 똑같다. _HongdalClose가 0.35~0.75일 때 서서히 칠한다.
//   횡단보도 자리는 서울시 자료(OA-23081), 보도 범위는 계산, 블록 무늬 · 줄무늬 · 점자블록 모양 · 색은 표현.
// 역 세 겹(2026-11-19, 사용자 방향: 역 둘레는 정밀하게, 그 너머는 낮은 돋을새김처럼). 역 중심 보기(_Window가 있을 때)에만 쓴다.
//   _HongdalTier(전역 값): (역 핵심 반경 m, 역세권 반경 m, 세기, 바깥 건물 높이 배율). 세기가 0이면 전과 똑같이 그린다.
//   역 핵심: 창 불빛이 더 많이 켜지고 조금 밝다. 역세권: 그대로. 바깥: 건물이 낮아지고 색이 빠지며 창이 꺼진다. 반경과 배율은 표현값이다.
// 계절 바탕(E1 · 2026-11-23, SagajeongDispatchEnvironment.cs가 매 화면 알린다). _HongdalEnvC.w가 0이면 전과 똑같이 그린다.
//   _HongdalEnvA = (밤, 해 낮음, 해 높이 −1~1, 젖음) · _HongdalEnvB = (벚꽃, 잎, 단풍, 쌓인 눈) · _HongdalEnvC = (연두, 내리는 비, 언 하천, 켬)
//   _HongdalSunTint = 햇빛 색(낮과 밤을 끄면 흰색). 녹지 갈래(uv.x 1·2·4)가 철마다 맨 가지 → 연두 → 짙은 녹색 → 단풍 → 맨 가지로 바뀌고,
//   벚꽃 철에는 산림지 · 조경녹지에 분홍 점이 핀다. 하천(3)은 비에 흐려지고 한겨울에 언다. 눈은 땅 · 녹지 · 지붕에 쌓이고 차도에는 얇게.
//   날짜는 관측(서울 관측소 벚꽃 · 북한산 단풍 · 실제 적설 · 강우), 날짜 사이와 색 · 무늬는 표현 규칙이다.
//   2026-11-24: _HongdalEnvD = (쌓인 낙엽, 날리는 벚꽃잎, 날리는 잎, 이팝) · _HongdalEnvE = (진달래, 철쭉, 0, 0).
//   산림지에 진달래, 조경녹지에 철쭉 무리(규칙 날짜). 가까이에서 보도 · 시설물대에 낙엽 · 꽃잎이 점점이 깔리고 숲 바닥은 낙엽빛.
// 현실감 시험(2026-11-24, SagajeongDispatchRealism.cs). _HongdalReal.x가 0이면 전과 똑같이 그린다.
//   _HongdalSunDir = (해 쪽 단위 벡터, 빛 0~1). 면의 방향은 화면 미분(ddx · ddy)으로 얻는다(메시에 법선이 없다). 해 쪽 면은 햇빛 색, 반대쪽은 하늘빛 그늘.
//   uv3 = (건물 갈래 + 씨앗, 실제 높이 m): 1 주택 · 2 초록 지붕 주택 · 3 아파트 · 4 상가 · 5 공공 · 6 공장 · 창고. 구울 때 넣은 벽의 고정 빛을 덜어 내고
//   벽 재질(흰 · 베이지 타일 · 붉은 벽돌 · 갈색 벽돌 · 회색)과 지붕(콘크리트 · 흰 방수 · 초록 우레탄 · 골강판)을 고른다. 모두 표현이다.
//   창: 낮에는 하늘이 비친 유리, 불 켜진 창은 「창 속의 방」(interior mapping 약식, 바닥 · 뒷벽 · 천장 · 커튼). 아파트는 넓은 발코니 창과 층마다 난간 띠.
//   uv2.y = -3 옥상 소품(정점 색 그대로) · -5 옥탑 벽(그 건물의 벽 재질). 지붕 테두리(-1) 판정은 -1 하나만 본다.
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
            #define HONGDAL_TEX(n) sampler2D n
            #define HONGDAL_SAMPLE(n, uv) tex2D(n, uv)
            #include "HongdalDioramaCore.cginc"      // 본문(URP 시험 갈래와 같이 쓴다)
            ENDCG
        }
    }
}
