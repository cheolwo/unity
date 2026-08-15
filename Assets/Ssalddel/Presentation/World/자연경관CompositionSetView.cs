using System.Linq;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    [DisallowMultipleComponent]
    public sealed class 자연경관CompositionSetView : MonoBehaviour
    {
        [SerializeField] private string setName = string.Empty;
        [SerializeField] private string variantCode = string.Empty;
        [SerializeField] private Transform environmentRoot = null!;
        [SerializeField] private Transform occlusionRoot = null!;
        [SerializeField] private Transform detailRoot = null!;
        [SerializeField] private Transform fxRoot = null!;
        [SerializeField] private Vector2 footprint = Vector2.one;
        [SerializeField] private bool presentationOnly = true;

        public string SetName => setName;
        public string VariantCode => variantCode;
        public Transform EnvironmentRoot => environmentRoot;
        public Transform OcclusionRoot => occlusionRoot;
        public Transform DetailRoot => detailRoot;
        public Transform FxRoot => fxRoot;
        public Vector2 Footprint => footprint;
        public bool PresentationOnly => presentationOnly;

        public void Configure(
            string name,
            string variant,
            Transform environment,
            Transform occlusion,
            Transform detail,
            Transform fx,
            Vector2 size)
        {
            setName = name;
            variantCode = variant;
            environmentRoot = environment;
            occlusionRoot = occlusion;
            detailRoot = detail;
            fxRoot = fx;
            footprint = size;
            presentationOnly = true;
        }

        public bool ValidateWiring()
            => 자연경관SetNames.All.Contains(setName)
                && 월드CompositionVariantCodes.IsKnown(variantCode)
                && environmentRoot != null && environmentRoot.IsChildOf(transform)
                && occlusionRoot != null && occlusionRoot.IsChildOf(transform)
                && detailRoot != null && detailRoot.IsChildOf(transform)
                && fxRoot != null && fxRoot.IsChildOf(transform)
                && environmentRoot.childCount + occlusionRoot.childCount
                    + detailRoot.childCount + fxRoot.childCount >= 4
                && footprint.x > 0f && footprint.y > 0f && presentationOnly;
    }
}
