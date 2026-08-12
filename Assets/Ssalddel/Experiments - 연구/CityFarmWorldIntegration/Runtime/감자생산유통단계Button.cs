using UnityEngine;
using UnityEngine.UI;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration
{
    public sealed class 감자생산유통단계Button : MonoBehaviour
    {
        [SerializeField] private 감자생산유통통합Presenter presenter;
        [SerializeField] private int stageIndex;
        [SerializeField] private Button button;
        [SerializeField] private Image buttonImage;

        private bool listenersBound;

        public int StageIndex => stageIndex;

        public void Configure(감자생산유통통합Presenter targetPresenter, int index)
        {
            presenter = targetPresenter;
            stageIndex = index;
            button = GetComponent<Button>();
            buttonImage = GetComponent<Image>();
            BindListener();
        }

        public void SetSelected(bool selected)
        {
            if (buttonImage == null)
                buttonImage = GetComponent<Image>();
            if (buttonImage != null)
                buttonImage.color = selected
                    ? new Color(.24f, .48f, .18f, 1f)
                    : new Color(.08f, .17f, .19f, 1f);
        }

        private void Awake() => BindListener();

        private void BindListener()
        {
            if (listenersBound || presenter == null || button == null)
                return;

            button.onClick.AddListener(() => presenter.SelectStage(stageIndex));
            listenersBound = true;
        }
    }
}
