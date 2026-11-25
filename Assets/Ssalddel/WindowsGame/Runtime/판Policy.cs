namespace Ssalddel.WindowsGame
{
    // 판(에디션) 가름 (2026-11-19 22:07 사용자 결정).
    //  - 스팀 첫 출시는 **구 지도가 바탕**이다. 동 단위 지도는 다음 업데이트(상위 판)에서 결합한다.
    //  - 그래서 기본 판에서는 구 지도를 휠로 끝까지 확대해도 동네 지도로 내려가지 않는다.
    //  - 상위 판을 만들 때 아래 값을 true로 바꾼다. 여기 한 곳만 바꾸면 된다.
    //  - 아직 옮기지 않은 것: 동네 지도에서만 되던 조작(배차 보기 · 가게 고르기 · 순회 등)을 구 지도로 옮기는 일.
    internal static class 판Policy
    {
        public static readonly bool 휠로동지도 = false;      // 기본 판 = false, 상위 판 = true
        // (22:26) 기본 판은 구 지도에서 시작하고, 동네 지도로 데려가는 길을 모두 닫는다. 상위 판 = true.
        // 닫는 자리는 한 곳이다: LoadMapLevel이 동네 지도 요청을 구 지도로 돌린다(SagajeongDispatchMap.cs).
        public static readonly bool 동지도 = false;
    }
}
