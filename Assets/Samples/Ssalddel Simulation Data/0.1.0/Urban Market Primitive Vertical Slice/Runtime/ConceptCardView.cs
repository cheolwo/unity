using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Unity.PresentationContracts.LearningCards;
using UnityEngine;

namespace Ssalddel.Unity.Samples.UrbanMarket
{
    [Serializable]
    public sealed class ConceptCardKindMaterialBinding
    {
        public string CardKindCode = string.Empty;
        public Material Material = null!;
    }

    /// <summary>
    /// 업무 의미와 외부 시각 asset 사이의 교체 경계입니다.
    /// Synty prefab이나 material 이름을 Presentation model에 넣지 않습니다.
    /// </summary>
    public sealed class ConceptCardVisualSkin : MonoBehaviour
    {
        [SerializeField] private ConceptCardKindMaterialBinding[] kindMaterials =
            Array.Empty<ConceptCardKindMaterialBinding>();
        [SerializeField] private Material selectedMaterial = null!;

        public void Configure(
            ConceptCardKindMaterialBinding[] materials,
            Material selected)
        {
            kindMaterials = materials ?? Array.Empty<ConceptCardKindMaterialBinding>();
            selectedMaterial = selected;
        }

        public void Apply(Renderer target, string cardKindCode, bool selected)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            var material = selected
                ? selectedMaterial
                : kindMaterials.SingleOrDefault(value => value != null
                    && value.CardKindCode == cardKindCode)?.Material;
            if (material == null)
                throw new InvalidOperationException("ConceptCardSkinMaterialMissing:" + cardKindCode);
            target.sharedMaterial = material;
        }

        public bool ValidateWiring()
        {
            if (selectedMaterial == null || kindMaterials == null) return false;
            var known = new HashSet<string>(new[]
            {
                ConceptCardKindCodes.Concept,
                ConceptCardKindCodes.Status,
                ConceptCardKindCodes.Reason,
                ConceptCardKindCodes.Action,
            }, StringComparer.Ordinal);
            return kindMaterials.Length == known.Count
                && kindMaterials.All(value => value != null && value.Material != null
                    && known.Remove(value.CardKindCode))
                && known.Count == 0;
        }
    }

    public sealed class ConceptCardView : MonoBehaviour
    {
        [SerializeField] private GameObject visualRoot = null!;
        [SerializeField] private Renderer frameRenderer = null!;
        [SerializeField] private Collider selectionCollider = null!;
        [SerializeField] private TextMesh kindText = null!;
        [SerializeField] private TextMesh titleText = null!;
        [SerializeField] private TextMesh primaryValueText = null!;
        [SerializeField] private TextMesh summaryText = null!;
        [SerializeField] private TextMesh evidenceText = null!;
        [SerializeField] private TextMesh cautionText = null!;
        [SerializeField] private TextMesh actionText = null!;

        private ConceptCardPresentationModel? current;
        private ConceptCardVisualSkin? skin;

        public event Action<string>? Selected;

        public string PresentationStableId => current?.StableId.Value ?? string.Empty;
        public GameObject VisualRoot => visualRoot;

        public void Configure(
            GameObject visual,
            Renderer frame,
            Collider collider,
            TextMesh kind,
            TextMesh title,
            TextMesh primary,
            TextMesh summary,
            TextMesh evidence,
            TextMesh caution,
            TextMesh action)
        {
            visualRoot = visual;
            frameRenderer = frame;
            selectionCollider = collider;
            kindText = kind;
            titleText = title;
            primaryValueText = primary;
            summaryText = summary;
            evidenceText = evidence;
            cautionText = caution;
            actionText = action;
        }

        public void Apply(
            ConceptCardPresentationModel model,
            ConceptCardVisualSkin visualSkin,
            bool selected)
        {
            current = model ?? throw new ArgumentNullException(nameof(model));
            skin = visualSkin ?? throw new ArgumentNullException(nameof(visualSkin));
            kindText.text = model.CardKindCode + ModeSuffix(model.SimulationLabel);
            titleText.text = model.TitleText;
            primaryValueText.text = model.PrimaryValueText;
            summaryText.text = model.SummaryText;
            evidenceText.text = Join(model.EvidenceRows.Select(value =>
                value.LabelText + "  " + value.ValueText));
            cautionText.text = Join(model.Cautions.Select(value => "주의 · " + value));
            actionText.text = Join(model.ActionItems.Select(value =>
                (value.IsAvailable ? "가능 · " : "차단 · ") + value.LabelText));
            visualRoot.SetActive(true);
            skin.Apply(frameRenderer, model.CardKindCode, selected);
        }

        public void SetSelected(bool selected)
        {
            if (current == null || skin == null) return;
            skin.Apply(frameRenderer, current.CardKindCode, selected);
        }

        public void Hide()
        {
            current = null;
            visualRoot.SetActive(false);
        }

        public bool ValidateWiring()
            => visualRoot != null && frameRenderer != null && selectionCollider != null
                && kindText != null && titleText != null && primaryValueText != null
                && summaryText != null && evidenceText != null && cautionText != null
                && actionText != null;

        private void OnMouseDown()
        {
            if (current != null) Selected?.Invoke(current.StableId.Value);
        }

        private static string Join(IEnumerable<string> lines)
            => string.Join("\n", lines.Where(value => !string.IsNullOrWhiteSpace(value)));

        private static string ModeSuffix(string label)
            => string.IsNullOrWhiteSpace(label) ? string.Empty : " · " + label;
    }
}
