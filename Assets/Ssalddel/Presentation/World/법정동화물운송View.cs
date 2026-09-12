namespace Ssalddel.Unity.Presentation.World
{
    // 기존 저장 Scene과 .meta GUID를 유지하는 화물 전용 호환 구성 요소입니다.
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E7,
        "플레이어 입력·화면·피드백과 플레이 경험 표현을 조율한다.",
        Boundary = "Unity 표현은 권위 상태 변경이나 실제 플레이 완료를 대신하지 않는다.")]
    public sealed class 법정동화물운송View : Npc물류RouteFollower
    {
    }
}
