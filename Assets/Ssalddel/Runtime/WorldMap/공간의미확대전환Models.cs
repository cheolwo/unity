using Ssalddel.Unity.Runtime.World;

namespace Ssalddel.Unity.Runtime.WorldMap
{
    /// <summary>
    /// 지구본의 지형 확대 뒤에 이어지는 읽기 전용 공간 선택 단계다.
    /// 각 단계는 운영·Simulation 상태나 Actor 위치를 변경하지 않는다.
    /// </summary>
    public enum 공간의미확대전환단계
    {
        Hidden = 0,
        Region = 1,
        AdministrativeArea = 2,
        StationArea = 3,
        LocalDiorama = 4,
    }

    public sealed class 공간의미확대전환흐름
    {
        public const string RegionDisplayName = "사가정 생활권 참조 범위";
        public const string AdministrativeAreaDisplayName = "면목제3·8동";
        public const string StationAreaDisplayName = "사가정역 생활권";
        public const string LocalDioramaDisplayName = "사가정역 1km 디오라마";

        public 공간의미확대전환단계 CurrentStage { get; private set; }
            = 공간의미확대전환단계.Hidden;

        public string CurrentStableId => CurrentStage switch
        {
            공간의미확대전환단계.Region => 사가정저밀도교통Fixture.RegionStableId,
            공간의미확대전환단계.AdministrativeArea =>
                사가정저밀도교통Fixture.AdministrativeAreaStableId,
            공간의미확대전환단계.StationArea or 공간의미확대전환단계.LocalDiorama =>
                StationDioramaProfileCatalog.SagajeongStationStableId,
            _ => string.Empty,
        };

        public string CurrentDisplayName => CurrentStage switch
        {
            공간의미확대전환단계.Region => RegionDisplayName,
            공간의미확대전환단계.AdministrativeArea => AdministrativeAreaDisplayName,
            공간의미확대전환단계.StationArea => StationAreaDisplayName,
            공간의미확대전환단계.LocalDiorama => LocalDioramaDisplayName,
            _ => "한반도 지형 관찰",
        };

        public string DiagnosticCode => CurrentStage switch
        {
            공간의미확대전환단계.Region => "CandidateScope30",
            공간의미확대전환단계.AdministrativeArea =>
                "HistoricalBoundaryCandidate_CurrentPublicationBlocked",
            공간의미확대전환단계.StationArea => "LocalPrivateReview_Ready",
            공간의미확대전환단계.LocalDiorama => "PreparedObservationOpened",
            _ => "TerrainHubLevelRequired",
        };

        public string TruthBoundary => CurrentStage switch
        {
            공간의미확대전환단계.Region =>
                "30개 행정동 후보 범위 · 현재 행정 경계 확정 아님",
            공간의미확대전환단계.AdministrativeArea =>
                "과거 경계 후보 · 현재 공개 경계 자료 결속 보류",
            공간의미확대전환단계.StationArea =>
                "사가정역 기준 1km · 비공개 검토용 동결 자료",
            공간의미확대전환단계.LocalDiorama =>
                "관찰 표현 전용 · 이동·운영·Simulation 권위 없음",
            _ => "한반도 거점 단계에서 지역 선택을 시작할 수 있습니다",
        };

        public bool CanAdvance => CurrentStage is 공간의미확대전환단계.Region
            or 공간의미확대전환단계.AdministrativeArea;

        public bool CanRetreat => CurrentStage is 공간의미확대전환단계.AdministrativeArea
            or 공간의미확대전환단계.StationArea;

        public bool CanOpenPreparedDiorama
            => CurrentStage == 공간의미확대전환단계.StationArea;

        public void SetTerrainHubLevel(bool visible)
        {
            if (!visible)
            {
                CurrentStage = 공간의미확대전환단계.Hidden;
                return;
            }

            if (CurrentStage == 공간의미확대전환단계.Hidden)
                CurrentStage = 공간의미확대전환단계.Region;
        }

        public bool TryAdvance()
        {
            if (CurrentStage == 공간의미확대전환단계.Region)
            {
                CurrentStage = 공간의미확대전환단계.AdministrativeArea;
                return true;
            }
            if (CurrentStage == 공간의미확대전환단계.AdministrativeArea)
            {
                CurrentStage = 공간의미확대전환단계.StationArea;
                return true;
            }
            return false;
        }

        public bool TryRetreat()
        {
            if (CurrentStage == 공간의미확대전환단계.StationArea)
            {
                CurrentStage = 공간의미확대전환단계.AdministrativeArea;
                return true;
            }
            if (CurrentStage == 공간의미확대전환단계.AdministrativeArea)
            {
                CurrentStage = 공간의미확대전환단계.Region;
                return true;
            }
            return false;
        }

        public bool MarkLocalDioramaOpened()
        {
            if (!CanOpenPreparedDiorama) return false;
            CurrentStage = 공간의미확대전환단계.LocalDiorama;
            return true;
        }

        public void RestoreStationArea()
            => CurrentStage = 공간의미확대전환단계.StationArea;
    }
}
