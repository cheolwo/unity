using UnityEngine;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration
{
    [DisallowMultipleComponent]
    public sealed class 에셋연구표본View : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        [SerializeField] private string 원본Guid = string.Empty;
        [SerializeField] private int 전시칸번호;
        [SerializeField] private Renderer 선택표시Renderer = null!;
        private 에셋연구소Presenter presenter = null!;

        public string 원본Guid값 => 원본Guid;
        public int 전시칸번호값 => 전시칸번호;

        public void Configure(string sourceGuid, int slotNumber, Renderer selectionRenderer,
            에셋연구소Presenter owner)
        {
            원본Guid = sourceGuid;
            전시칸번호 = slotNumber;
            선택표시Renderer = selectionRenderer;
            presenter = owner;
            ApplySelection(false);
        }

        public void ApplySelection(bool selected)
        {
            if (선택표시Renderer == null) return;
            var color = selected ? new Color(.98f, .66f, .18f) : new Color(.23f, .31f, .27f);
            var properties = new MaterialPropertyBlock();
            선택표시Renderer.GetPropertyBlock(properties);
            properties.SetColor(BaseColorId, color);
            properties.SetColor(ColorId, color);
            선택표시Renderer.SetPropertyBlock(properties);
        }

        private void OnMouseDown()
        {
            if (presenter == null)
                presenter = Object.FindFirstObjectByType<에셋연구소Presenter>();
            presenter?.Select(원본Guid, 전시칸번호);
        }
    }
}
