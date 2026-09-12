using UnityEngine;

namespace Ssalddel.Unity.Bootstrap
{
    /// <summary>사용자가 지정한 역할별 색상. 나머지 오행 의미는 정의하지 않는다.</summary>
    public static class 가상동네역할색상
    {
        public static Color 음식점 => new Color(.9f, .08f, .08f);
        public static Color 주문자 => new Color(.08f, .3f, .95f);
        public static Color 창고 => Color.white;
        public static Color 배송기사 => new Color(.035f, .035f, .035f);
    }
}
