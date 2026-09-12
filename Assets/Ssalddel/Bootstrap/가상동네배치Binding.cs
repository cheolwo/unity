using Ssalddel.Simulation.Contracts;
using UnityEngine;

namespace Ssalddel.Unity.Bootstrap
{
    public sealed partial class 가상배달관찰Controller
    {
        // 배치 JSON에서 생성한 공통 XZ 정의를 읽는다. 이 코드를 작성하는 것만으로 Scene을 바꾸지 않는다.
        private Transform 배치도형(string id,string title,float y,float height,Color color)
        {
            var p=가상동네배치기준.시설(id);
            return Block(title,new Vector3((float)p.x,y,(float)p.z),new Vector3((float)p.width,height,(float)p.depth),color);
        }
        private void 배치건물(string id,string title,Color color)
        {
            var p=가상동네배치기준.시설(id);
            Building(title,(float)p.x,(float)p.z,color);
        }
    }
}
