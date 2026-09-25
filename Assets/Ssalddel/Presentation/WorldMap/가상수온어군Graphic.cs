#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;

namespace Ssalddel.Unity.Presentation.WorldMap
{
    /// <summary>축척과 무관한 어군 기호. 새 이미지 자산이나 실제 종 모형을 사용하지 않는다.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class 가상수온어군Graphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Add(vh,new Vector2(-.45f,0),new Vector2(.05f,.30f),new Vector2(.48f,0));
            Add(vh,new Vector2(-.45f,0),new Vector2(.48f,0),new Vector2(.05f,-.30f));
            Add(vh,new Vector2(-.35f,0),new Vector2(-.65f,.27f),new Vector2(-.65f,-.27f));
        }
        private void Add(VertexHelper vh,Vector2 a,Vector2 b,Vector2 c)
        {
            var n=vh.currentVertCount; var size=rectTransform.rect.size;
            vh.AddVert(Vector2.Scale(a,size),color,Vector2.zero);
            vh.AddVert(Vector2.Scale(b,size),color,Vector2.zero);
            vh.AddVert(Vector2.Scale(c,size),color,Vector2.zero); vh.AddTriangle(n,n+1,n+2);
        }
    }
}
#endif
