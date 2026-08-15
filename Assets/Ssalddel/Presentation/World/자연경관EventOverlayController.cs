using System;
using System.Linq;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    [DisallowMultipleComponent]
    public sealed class 자연경관EventOverlayController : MonoBehaviour
    {
        [SerializeField] private Transform overlayRoot = null!;
        [SerializeField] private 자연경관EventOverlayCatalog catalog = null!;
        [SerializeField] private string activePresentationKey =
            자연경관EventPresentationKeys.ScenicExploration;
        [SerializeField] private bool presentationOnly = true;

        public string ActivePresentationKey => activePresentationKey;
        public bool PresentationOnly => presentationOnly;
        public int ActiveOverlayCount => overlayRoot == null ? 0 : overlayRoot
            .GetComponentsInChildren<자연경관EventOverlayView>(true)
            .Count(value => value.gameObject.activeSelf);

        public void Configure(
            Transform root,
            자연경관EventOverlayCatalog sourceCatalog)
        {
            overlayRoot = root;
            catalog = sourceCatalog;
            presentationOnly = true;
            ApplyPresentationKey(자연경관EventPresentationKeys.ScenicExploration);
        }

        public bool ApplyPresentationKey(string presentationKey)
        {
            if (overlayRoot == null || catalog == null) return false;
            catalog.Validate();
            foreach (var view in overlayRoot
                         .GetComponentsInChildren<자연경관EventOverlayView>(true))
                view.gameObject.SetActive(false);

            activePresentationKey = presentationKey ?? string.Empty;
            if (activePresentationKey
                == 자연경관EventPresentationKeys.ScenicExploration)
                return false;
            if (!catalog.TryResolve(activePresentationKey, out _)) return false;

            var selected = overlayRoot
                .GetComponentsInChildren<자연경관EventOverlayView>(true)
                .Single(value => value.PresentationKey == activePresentationKey);
            selected.gameObject.SetActive(true);
            return true;
        }

        public bool ValidateWiring()
        {
            if (overlayRoot == null || catalog == null || !presentationOnly)
                return false;
            try
            {
                catalog.Validate();
                var views = overlayRoot
                    .GetComponentsInChildren<자연경관EventOverlayView>(true);
                return views.Length == catalog.Entries.Count
                    && views.All(value => value.ValidateWiring());
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
    }
}
