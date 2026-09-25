namespace Ssalddel.Unity.Runtime.WorldMap
{
    public enum 세계지도표현계층
    {
        Diorama = 0,
        Globe = 1,
        RegionalTerrain = 2,
        Region = 3,
        AdministrativeArea = 4,
        StationArea = 5,
    }

    /// <summary>
    /// 공식 Scene 안에서 세계 지구본과 지역 디오라마가 같은 입력·화면을 동시에
    /// 소비하지 않도록 하는 읽기 전용 표현 상태다. 운영·Simulation 상태가 아니다.
    /// </summary>
    public static class 세계지도표현상태
    {
        public static bool GlobeOverlayVisible { get; private set; }
        public static 세계지도표현계층 CurrentLayer { get; private set; }
            = 세계지도표현계층.Diorama;

        public static void SetGlobeOverlayVisible(bool value)
        {
            GlobeOverlayVisible = value;
            CurrentLayer = value
                ? 세계지도표현계층.Globe
                : 세계지도표현계층.Diorama;
        }

        public static void SetObservationLayer(세계지도표현계층 layer)
        {
            CurrentLayer = layer;
            GlobeOverlayVisible = layer != 세계지도표현계층.Diorama;
        }
    }
}
