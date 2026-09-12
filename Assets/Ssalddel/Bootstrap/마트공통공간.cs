using UnityEngine;

namespace Ssalddel.Unity.Bootstrap
{
    /// <summary>로컬 생활과 서버 관찰이 공유하는 마트 내부. 업무 상태나 실행 권한은 소유하지 않는다.</summary>
    public static class 마트공통공간
    {
        public static Vector3 입고 => new(-9, .9f, -9);
        public static Vector3 검수 => new(-6, .9f, -11);
        public static Vector3 선반접근 => new(-8, .9f, -13.5f);
        public static Vector3 대기 => new(0, .9f, -12);
        public static Vector3 검수대 => new(-6, .6f, -9);
        public const float 통로Z = -12;
        public static Vector3 선반(int index) => new(-8 + index * 4, .6f, -15);

        // 모서리를 대각선으로 가로지르지 않고 선반 앞 통로를 경유한다.
        public static Vector3[] 이동경로(Vector3 from, Vector3 to) => new[] {
            new Vector3(from.x, .9f, 통로Z), new Vector3(to.x, .9f, 통로Z), to };
    }

    public sealed partial class 가상배달관찰Controller
    {
        private bool 마트내부생성됨;

        private void 마트내부조립()
        {
            if (마트내부생성됨) return;
            마트내부생성됨 = true;
            for (int i = 0; i < 2; i++)
            {
                var p = 마트공통공간.선반(i);
                var shelf = Block("적재대 " + (i + 1), p, new Vector3(2, 1.2f, 1.5f), new Color(.8f, .8f, .8f));
                Label(shelf, "재고 " + (i + 1), Color.black);
                shelfGoods[i] = Block("재고 상품 " + (i + 1), p + Vector3.up * .9f, Vector3.one * .7f, new Color(.8f, .6f, .35f));
            }
            Label(Block("포장대", new Vector3(0,.6f,-15), new Vector3(2,1.2f,1.5f), new Color(.8f,.8f,.8f)), "포장", Color.black);
            Label(Block("인계대", new Vector3(-11,.6f,-8), new Vector3(1.5f,1.2f,2), new Color(.8f,.8f,.8f)), "인계", Color.black);
            Label(Block("마트 검수대", 마트공통공간.검수대, new Vector3(2,1.2f,1.2f), Color.white), "검수", Color.black);
            Label(Block("마트 입고점", new Vector3(-9,.18f,-9), new Vector3(1.2f,.1f,1.2f), new Color(.7f,.7f,.6f)), "입고", Color.black);
            martWorker = Block("마트 작업자", new Vector3(0,.9f,-14), new Vector3(.6f,1.8f,.6f), Color.white);
            Label(martWorker, "작업자", Color.black);
            workerParcel = Block("작업자 상품", Vector3.zero, Vector3.one*.4f, new Color(.8f,.6f,.35f));
            handoffParcel = Block("인계 상품", new Vector3(-11,1.5f,-8), Vector3.one*.5f, new Color(.8f,.6f,.35f));
        }

        private void 마트동적표현숨김()
        {
            foreach (var item in new[] { martWorker, workerParcel, handoffParcel, shelfGoods[0], shelfGoods[1] })
                if (item != null) item.gameObject.SetActive(false);
        }
    }
}
