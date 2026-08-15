using System;
using System.Linq;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>
    /// 서버·Simulation 역할을 표현 역할로 해석해 VisualRoot 아래 캐릭터만 교체합니다.
    /// 역할 변경이나 Prefab 교체는 서버 권한과 업무 상태를 변경하지 않습니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class 역할CharacterVisualSwitcher : MonoBehaviour
    {
        [SerializeField] private WorldActorAppearanceProfile appearanceProfile = new();
        [SerializeField] private string sourceRoleCode = string.Empty;
        [SerializeField] private string workflowContextCode = string.Empty;
        [SerializeField] private string areaRoleCode = string.Empty;
        [SerializeField] private 역할CharacterVisualCatalog catalog = null!;
        [SerializeField] private Transform visualRoot = null!;
        [SerializeField] private GameObject prefabInstanceRoot = null!;
        [SerializeField] private 공용AnimationAdapter animationAdapter = null!;
        [SerializeField] private 역할CharacterVisualInstanceView instanceView = null!;
        [SerializeField] private string diagnosticCode = string.Empty;
        [SerializeField] private bool presentationOnly = true;

        public string SourceRoleCode => sourceRoleCode;
        public string WorkflowContextCode => workflowContextCode;
        public string AreaRoleCode => areaRoleCode;
        public string DiagnosticCode => diagnosticCode;
        public bool PresentationOnly => presentationOnly;

        public void ConfigureExisting(
            WorldActorAppearanceProfile profile,
            string sourceRole,
            string workflowContext,
            string areaRole,
            역할CharacterVisualCatalog sourceCatalog,
            Transform root,
            GameObject instance,
            공용AnimationAdapter adapter,
            역할CharacterVisualInstanceView view)
        {
            appearanceProfile = profile;
            sourceRoleCode = sourceRole ?? string.Empty;
            workflowContextCode = workflowContext ?? string.Empty;
            areaRoleCode = areaRole ?? string.Empty;
            catalog = sourceCatalog;
            visualRoot = root;
            prefabInstanceRoot = instance;
            animationAdapter = adapter;
            instanceView = view;
            diagnosticCode = string.Empty;
            presentationOnly = true;
            if (!ValidateWiring())
                throw new InvalidOperationException(
                    "RoleCharacterVisualSwitcherConfigurationInvalid");
        }

        public WorldCharacterAssignmentResult ApplyServerRole(
            string newSourceRoleCode,
            string newWorkflowContextCode)
        {
            if (!presentationOnly || catalog == null || appearanceProfile == null
                || !appearanceProfile.Validate())
                throw new InvalidOperationException("RoleCharacterVisualSwitcherNotReady");

            var normalized = WorldActorRoleNormalizer.Normalize(
                newSourceRoleCode, newWorkflowContextCode);
            sourceRoleCode = normalized.SourceRoleCode;
            workflowContextCode = normalized.WorkflowContextCode;
            var assignment = WorldCharacterAssignmentPolicy.Assign(
                appearanceProfile,
                normalized.ActorRoleCode,
                catalog.CatalogRevision,
                catalog.AssignmentCandidates());
            var entry = catalog.Resolve(assignment.VisualKey);
            if (!entry.AllowedAreaRoleCodes.Contains(areaRoleCode))
                throw new InvalidOperationException(
                    "RoleCharacterVisualAreaNotAllowed:" + assignment.VisualKey);

            if (prefabInstanceRoot != null)
            {
                if (UnityEngine.Application.isPlaying) Destroy(prefabInstanceRoot);
                else DestroyImmediate(prefabInstanceRoot);
            }
            prefabInstanceRoot = Instantiate(entry.Prefab, visualRoot);
            prefabInstanceRoot.name = "SyntyRoleCharacterVisual_" + assignment.ActorRoleCode;
            prefabInstanceRoot.transform.SetLocalPositionAndRotation(
                Vector3.zero, Quaternion.identity);
            var animator = prefabInstanceRoot.GetComponentInChildren<Animator>(true)
                ?? throw new InvalidOperationException(
                    "RoleCharacterVisualAnimatorMissing:" + assignment.VisualKey);
            animator.runtimeAnimatorController = null;
            var animationEntry = new 공용AnimationCatalogEntry();
            animationEntry.Configure(
                entry.AnimationPackCode,
                assignment.ActorRoleCode,
                "locomotion.idle.v1",
                "locomotion.walk.v1",
                공용AnimationSourceKindCodes.ProceduralFallback,
                "humanoid.procedural-locomotion.v1",
                entry.Prefab,
                null,
                null);
            animationAdapter.Configure(animationEntry, animator);
            instanceView.Configure(
                appearanceProfile,
                assignment,
                areaRoleCode,
                catalog,
                visualRoot,
                prefabInstanceRoot);
            diagnosticCode = string.IsNullOrEmpty(normalized.DiagnosticCode)
                ? assignment.DiagnosticCode : normalized.DiagnosticCode;

            var playerController = GetComponent<플레이어경관Controller>();
            if (playerController != null)
                playerController.RebindVisual(visualRoot, animationAdapter);
            return assignment;
        }

        public bool ValidateWiring()
            => appearanceProfile != null && appearanceProfile.Validate()
                && !string.IsNullOrWhiteSpace(sourceRoleCode)
                && WorldActorWorkflowContextCodes.IsKnown(workflowContextCode)
                && (areaRoleCode == 법정동WorldRoleCodes.Farm
                    || areaRoleCode == 법정동WorldRoleCodes.Hub
                    || areaRoleCode == 법정동WorldRoleCodes.Town)
                && catalog != null
                && visualRoot != null
                && prefabInstanceRoot != null
                && prefabInstanceRoot.transform.IsChildOf(visualRoot)
                && animationAdapter != null
                && instanceView != null
                && instanceView.ValidateWiring()
                && presentationOnly;
    }
}
