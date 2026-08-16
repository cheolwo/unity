using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>
    /// Nature 원본을 수정하지 않고 생성 wrapper의 Renderer 그림자 책임만 고정합니다.
    /// 실제 그림자 거리는 URP 품질 프로필이 결정하며 이 View는 Simulation 권위를 갖지 않습니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class 자연경관ShadowPolicyView : MonoBehaviour
    {
        [SerializeField] private string shadowPolicyCode = string.Empty;
        [SerializeField] private Transform environmentRoot = null!;
        [SerializeField] private Transform occlusionRoot = null!;
        [SerializeField] private Transform detailRoot = null!;
        [SerializeField] private Transform fxRoot = null!;
        [SerializeField] private int castingRendererCount;
        [SerializeField] private int receiveOnlyRendererCount;
        [SerializeField] private bool presentationOnly = true;

        public string ShadowPolicyCode => shadowPolicyCode;
        public int CastingRendererCount => castingRendererCount;
        public int ReceiveOnlyRendererCount => receiveOnlyRendererCount;
        public bool PresentationOnly => presentationOnly;

        public void Configure(
            string policyCode,
            Transform environment,
            Transform occlusion,
            Transform detail,
            Transform fx)
        {
            if (!자연경관ShadowPolicyCodes.All.Contains(policyCode, StringComparer.Ordinal))
                throw new InvalidOperationException("NatureShadowPolicyUnknown:" + policyCode);
            shadowPolicyCode = policyCode;
            environmentRoot = environment;
            occlusionRoot = occlusion;
            detailRoot = detail;
            fxRoot = fx;
            presentationOnly = true;

            ApplyMainPolicy(environmentRoot);
            ApplyMainPolicy(occlusionRoot);
            ApplyReceiveOnly(detailRoot);
            ApplyDisabled(fxRoot);
            var renderers = GetComponentsInChildren<Renderer>(true);
            castingRendererCount = renderers.Count(value =>
                value.shadowCastingMode != ShadowCastingMode.Off);
            receiveOnlyRendererCount = renderers.Count(value =>
                value.shadowCastingMode == ShadowCastingMode.Off && value.receiveShadows);
        }

        public bool ValidateWiring()
        {
            if (!자연경관ShadowPolicyCodes.All.Contains(
                    shadowPolicyCode, StringComparer.Ordinal)
                || environmentRoot == null || occlusionRoot == null
                || detailRoot == null || fxRoot == null
                || !presentationOnly)
                return false;
            if (!ValidateMainPolicy(environmentRoot)
                || !ValidateMainPolicy(occlusionRoot)
                || !ValidateReceiveOnly(detailRoot)
                || !ValidateDisabled(fxRoot))
                return false;
            var renderers = GetComponentsInChildren<Renderer>(true);
            return castingRendererCount == renderers.Count(value =>
                       value.shadowCastingMode != ShadowCastingMode.Off)
                   && receiveOnlyRendererCount == renderers.Count(value =>
                       value.shadowCastingMode == ShadowCastingMode.Off
                       && value.receiveShadows);
        }

        private void ApplyMainPolicy(Transform root)
        {
            if (shadowPolicyCode == 자연경관ShadowPolicyCodes.CastReceive)
                Apply(root, ShadowCastingMode.On, true);
            else if (shadowPolicyCode == 자연경관ShadowPolicyCodes.ReceiveOnly)
                ApplyReceiveOnly(root);
            else
                ApplyDisabled(root);
        }

        private bool ValidateMainPolicy(Transform root) => shadowPolicyCode switch
        {
            자연경관ShadowPolicyCodes.CastReceive =>
                Validate(root, ShadowCastingMode.On, true),
            자연경관ShadowPolicyCodes.ReceiveOnly => ValidateReceiveOnly(root),
            자연경관ShadowPolicyCodes.Disabled => ValidateDisabled(root),
            _ => false,
        };

        private static void ApplyReceiveOnly(Transform root) =>
            Apply(root, ShadowCastingMode.Off, true);

        private static void ApplyDisabled(Transform root) =>
            Apply(root, ShadowCastingMode.Off, false);

        private static bool ValidateReceiveOnly(Transform root) =>
            Validate(root, ShadowCastingMode.Off, true);

        private static bool ValidateDisabled(Transform root) =>
            Validate(root, ShadowCastingMode.Off, false);

        private static void Apply(
            Transform root, ShadowCastingMode castingMode, bool receiveShadows)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = castingMode;
                renderer.receiveShadows = receiveShadows;
            }
        }

        private static bool Validate(
            Transform root, ShadowCastingMode castingMode, bool receiveShadows) =>
            root.GetComponentsInChildren<Renderer>(true).All(value =>
                value.shadowCastingMode == castingMode
                && value.receiveShadows == receiveShadows);
    }
}
