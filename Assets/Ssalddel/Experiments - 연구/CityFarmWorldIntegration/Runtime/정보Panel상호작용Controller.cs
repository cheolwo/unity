using UnityEngine;
using UnityEngine.UI;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration
{
    public enum 정보Panel표시상태
    {
        펼침,
        접힘,
        닫힘,
    }

    public sealed class 정보Panel상호작용Controller : MonoBehaviour
    {
        [SerializeField] private GameObject contentPanel;
        [SerializeField] private GameObject expandedControls;
        [SerializeField] private GameObject expandTab;
        [SerializeField] private GameObject reopenTab;
        [SerializeField] private Button collapseButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button expandButton;
        [SerializeField] private Button reopenButton;

        private bool listenersBound;

        public 정보Panel표시상태 CurrentState { get; private set; } = 정보Panel표시상태.펼침;

        public void Configure(
            GameObject panel,
            GameObject controls,
            GameObject collapsedTab,
            GameObject closedTab,
            Button collapse,
            Button close,
            Button expand,
            Button reopen)
        {
            contentPanel = panel;
            expandedControls = controls;
            expandTab = collapsedTab;
            reopenTab = closedTab;
            collapseButton = collapse;
            closeButton = close;
            expandButton = expand;
            reopenButton = reopen;
            BindListeners();
            ShowExpanded();
        }

        private void Awake()
        {
            BindListeners();
            ApplyState();
        }

        public void ShowExpanded()
        {
            CurrentState = 정보Panel표시상태.펼침;
            ApplyState();
        }

        public void Collapse()
        {
            CurrentState = 정보Panel표시상태.접힘;
            ApplyState();
        }

        public void Close()
        {
            CurrentState = 정보Panel표시상태.닫힘;
            ApplyState();
        }

        public void Reopen() => ShowExpanded();

        public bool ValidateWiring()
            => contentPanel != null
               && expandedControls != null
               && expandTab != null
               && reopenTab != null
               && collapseButton != null
               && closeButton != null
               && expandButton != null
               && reopenButton != null;

        private void BindListeners()
        {
            if (listenersBound || collapseButton == null || closeButton == null
                || expandButton == null || reopenButton == null)
                return;

            collapseButton.onClick.AddListener(Collapse);
            closeButton.onClick.AddListener(Close);
            expandButton.onClick.AddListener(ShowExpanded);
            reopenButton.onClick.AddListener(Reopen);
            listenersBound = true;
        }

        private void ApplyState()
        {
            if (contentPanel != null)
                contentPanel.SetActive(CurrentState == 정보Panel표시상태.펼침);
            if (expandedControls != null)
                expandedControls.SetActive(CurrentState == 정보Panel표시상태.펼침);
            if (expandTab != null)
                expandTab.SetActive(CurrentState == 정보Panel표시상태.접힘);
            if (reopenTab != null)
                reopenTab.SetActive(CurrentState == 정보Panel표시상태.닫힘);
        }
    }
}
