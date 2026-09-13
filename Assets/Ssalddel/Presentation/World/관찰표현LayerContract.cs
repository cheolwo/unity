namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>
    /// 서로 다른 관찰 화면이 같은 Unity layer를 점유해 카메라 표현을 섞지 않도록 하는
    /// 프로젝트 공통 표현 계약이다. 이 layer는 업무·충돌·Simulation 권위를 갖지 않는다.
    /// </summary>
    public static class 관찰표현LayerContract
    {
        public const int 오프라인생활Layer = 29;
        public const int 사가정운영관찰Layer = 30;

        public const int 오프라인생활Mask = 1 << 오프라인생활Layer;
        public const int 사가정운영관찰Mask = 1 << 사가정운영관찰Layer;
    }
}
