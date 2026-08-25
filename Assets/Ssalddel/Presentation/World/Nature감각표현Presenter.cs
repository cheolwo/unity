using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Unity.PresentationContracts;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>
    /// WI-NATURE-05~06의 권위 상태 사본을 도구 자세·3D 효과음·접촉 FX로 표현합니다.
    /// 이 Component는 Nature생존Engine을 호출하지 않고 상태 변경을 확정하지 않습니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Nature감각표현Presenter : MonoBehaviour
    {
        [SerializeField] private Nature생존Controller controller = null!;
        [SerializeField] private 플레이어경관Controller player = null!;
        [SerializeField] private 감각표현검증ReceiptView receipt = null!;
        [SerializeField] private 감각표현AnchorView[] anchors =
            Array.Empty<감각표현AnchorView>();
        [SerializeField] private AudioSource uiCueSource = null!;
        [SerializeField] private AudioSource optionalAreaAmbientSource = null!;
        [SerializeField] private AudioSource optionalAreaMusicSource = null!;
        [SerializeField] private string currentPhaseCode = string.Empty;
        [SerializeField] private string currentAnimationIntentCode = string.Empty;
        [SerializeField] private string lastAudioCueCode = string.Empty;
        [SerializeField] private string lastFxCueCode = string.Empty;
        [SerializeField] private string currentUiCueCode = string.Empty;
        [SerializeField] private string areaAmbientCueCode = string.Empty;
        [SerializeField] private string areaMusicCueCode = string.Empty;
        [SerializeField] private string diagnosticCode = string.Empty;
        [SerializeField] private bool axePlacementNormalized;
        [SerializeField] private bool presentationOnly = true;

        private readonly Nature감각표현상태Projector projector = new();
        private readonly Dictionary<string, Nature생존상호작용표식> markers =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, AudioSource> spatialSources =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, ParticleSystem> fxSystems =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, AudioClip> clips =
            new(StringComparer.Ordinal);
        private readonly HashSet<string> playedCueStableIds =
            new(StringComparer.Ordinal);
        private Nature생존State? previousState;
        private long observedRevision = -1;
        private bool harvestWorking;
        private float workAnimationStartedAt;
        private Transform? firstPersonToolRoot;
        private Transform? thirdPersonToolRoot;
        private Transform? leftUpperArm;
        private Transform? rightUpperArm;
        private Transform? spine;
        private Quaternion leftUpperArmBase;
        private Quaternion rightUpperArmBase;
        private Quaternion spineBase;
        private Material? fxMaterial;

        public bool IsConfigured => controller != null && player != null
                                    && receipt != null && anchors.Length > 0;
        public 감각표현검증ReceiptView Receipt => receipt;
        public 감각표현AnchorView[] Anchors => anchors;
        public string CurrentPhaseCode => currentPhaseCode;
        public string CurrentAnimationIntentCode => currentAnimationIntentCode;
        public string LastAudioCueCode => lastAudioCueCode;
        public string LastFxCueCode => lastFxCueCode;
        public string CurrentUiCueCode => currentUiCueCode;
        public string AreaAmbientCueCode => areaAmbientCueCode;
        public string AreaMusicCueCode => areaMusicCueCode;
        public string DiagnosticCode => diagnosticCode;
        public bool AxePlacementNormalized => axePlacementNormalized;
        public bool PresentationOnly => presentationOnly;
        public bool OptionalAreaAudioAvailable =>
            optionalAreaAmbientSource != null
            && optionalAreaAmbientSource.clip != null
            && optionalAreaMusicSource != null
            && optionalAreaMusicSource.clip != null;

        public void Configure(Nature생존Controller natureController,
            플레이어경관Controller playerController)
        {
            if (natureController == null || playerController == null
                || !natureController.ValidateWiring()
                || playerController.FirstPersonCamera == null)
                throw new InvalidOperationException(
                    "NatureSensoryPresentationConfigurationInvalid");

            controller = natureController;
            player = playerController;
            presentationOnly = true;
            NormalizeAxePlacement();
            BuildMarkersAndAnchors();
            BuildAudioRouting();
            BuildToolViews();
            CacheHumanoidBones();
            receipt = gameObject.GetComponent<감각표현검증ReceiptView>()
                      ?? gameObject.AddComponent<감각표현검증ReceiptView>();
            receipt.Configure(anchors, OptionalAreaAudioAvailable);
            previousState = controller.State;
            observedRevision = previousState.Revision;
            ApplyFrame(projector.Project(null, previousState), true);
            enabled = true;
            ValidateWiring();
        }

        public void ValidateWiring()
        {
            if (!IsConfigured || !presentationOnly || uiCueSource == null
                || optionalAreaAmbientSource == null
                || optionalAreaMusicSource == null
                || anchors.Any(value => value == null || !value.Accepted)
                || receipt.PresentationOnly == false)
                throw new InvalidOperationException(
                    "NatureSensoryPresentationWiringInvalid");
            receipt.Validate();
        }

        public void RefreshForTest()
        {
            if (!CanReadAuthoritySnapshot()) return;
            RefreshFromAuthoritySnapshot();
            TickToolAnimation();
        }

        private void Update()
        {
            if (!CanReadAuthoritySnapshot()) return;
            RefreshFromAuthoritySnapshot();
        }

        private void LateUpdate()
        {
            if (!CanReadAuthoritySnapshot()) return;
            TickToolAnimation();
        }

        private bool CanReadAuthoritySnapshot()
            => IsConfigured && controller.isActiveAndEnabled
                            && gameObject.activeInHierarchy;

        private void RefreshFromAuthoritySnapshot()
        {
            var current = controller.State;
            if (previousState != null && current.Revision == observedRevision)
                return;
            var frame = projector.Project(previousState, current);
            ApplyFrame(frame, false);
            previousState = current;
            observedRevision = current.Revision;
        }

        private void ApplyFrame(Nature감각표현Frame frame, bool baseline)
        {
            currentPhaseCode = frame.PhaseCode;
            currentAnimationIntentCode = frame.AnimationIntentCode;
            currentUiCueCode = frame.UiCueCode;
            areaAmbientCueCode = frame.AreaAmbientCueCode;
            areaMusicCueCode = frame.AreaMusicCueCode;
            var wasWorking = harvestWorking;
            harvestWorking = frame.PhaseCode == 감각표현단계Codes.Working;
            if (harvestWorking && !wasWorking)
                workAnimationStartedAt = Time.unscaledTime;

            if (!baseline && !string.IsNullOrWhiteSpace(frame.CueStableId)
                && playedCueStableIds.Add(frame.CueStableId))
            {
                if (!string.IsNullOrWhiteSpace(frame.AudioCueCode))
                {
                    lastAudioCueCode = frame.AudioCueCode;
                    PlayAudio(frame.AudioCueCode, frame.TargetStableId);
                }
                if (!string.IsNullOrWhiteSpace(frame.FxCueCode))
                {
                    lastFxCueCode = frame.FxCueCode;
                    PlayFx(frame.FxCueCode, frame.TargetStableId);
                }
                if (frame.PhaseCode == 감각표현단계Codes.Completed
                    && markers.TryGetValue(frame.TargetStableId, out var marker))
                    StartCoroutine(AnimateTreeFall(marker));
            }

            UpdateToolVisibility();
        }

        private void NormalizeAxePlacement()
        {
            var axe = controller.GetComponentsInChildren<
                    Nature생존상호작용표식>(true)
                .FirstOrDefault(value => value.Kind ==
                    Nature생존상호작용Kind.Axe);
            if (axe == null) return;
            var scale = axe.transform.localScale;
            if (ApproximatelyUniform(scale))
            {
                axePlacementNormalized = true;
                return;
            }

            var sourceParent = axe.transform.parent;
            var wrapper = new GameObject("PlacementWrapper_Nature_AxePickup");
            wrapper.transform.SetParent(sourceParent, false);
            wrapper.transform.localPosition = axe.transform.localPosition;
            wrapper.transform.localRotation = axe.transform.localRotation;
            wrapper.transform.localScale = Vector3.one;
            axe.transform.SetParent(wrapper.transform, false);
            axe.transform.localPosition = Vector3.zero;
            axe.transform.localRotation = Quaternion.identity;
            axe.transform.localScale = Vector3.one;
            axePlacementNormalized = ApproximatelyUniform(
                wrapper.transform.localScale)
                && ApproximatelyUniform(axe.transform.localScale);
        }

        private void BuildMarkersAndAnchors()
        {
            markers.Clear();
            var anchorRoot = new GameObject("SensoryPresentationAnchors").transform;
            anchorRoot.SetParent(transform, false);
            var built = new List<감각표현AnchorView>();
            foreach (var marker in controller.GetComponentsInChildren<
                         Nature생존상호작용표식>(true))
            {
                markers[marker.StableId] = marker;
                var h1 = marker.Kind == Nature생존상호작용Kind.Axe
                    ? "h1-stock:nature-trailhead"
                    : controller.State.ResourceNodes.FirstOrDefault(value =>
                        value.StableId == marker.StableId)?.H1StableId
                      ?? "h1-stock:nature-exploration-buffer";
                var baseWorld = MarkerBoundsCenter(marker);
                if (marker.Kind == Nature생존상호작용Kind.Axe)
                {
                    built.Add(CreateWorldAnchor(anchorRoot, marker.StableId,
                        h1, 배치표현AnchorRoleCodes.CameraFocus,
                        baseWorld));
                    built.Add(CreateWorldAnchor(anchorRoot, marker.StableId,
                        h1, 배치표현AnchorRoleCodes.AudioEmitter,
                        baseWorld));
                }
                else if (marker.Kind == Nature생존상호작용Kind.HarvestTree)
                {
                    built.Add(CreateWorldAnchor(anchorRoot, marker.StableId,
                        h1, 배치표현AnchorRoleCodes.CameraFocus,
                        baseWorld + Vector3.up * .3f));
                    built.Add(CreateWorldAnchor(anchorRoot, marker.StableId,
                        h1, 배치표현AnchorRoleCodes.ActorWork,
                        marker.transform.position - marker.transform.forward * 1.5f));
                    built.Add(CreateWorldAnchor(anchorRoot, marker.StableId,
                        h1, 배치표현AnchorRoleCodes.AudioEmitter,
                        marker.transform.position + Vector3.up * .9f));
                    built.Add(CreateWorldAnchor(anchorRoot, marker.StableId,
                        h1, 배치표현AnchorRoleCodes.Fx,
                        marker.transform.position + Vector3.up * .8f));
                }
            }

            var toolAnchorObject = new GameObject("ToolSocketAnchor");
            toolAnchorObject.transform.SetParent(
                player.FirstPersonCamera.transform, false);
            var toolAnchor = toolAnchorObject.AddComponent<감각표현AnchorView>();
            toolAnchor.Configure("anchor:nature-player:tool-socket",
                "pickup:nature-safe-clearing:basic-axe",
                "h1-stock:nature-trailhead",
                배치표현AnchorRoleCodes.ToolSocket,
                new Vector3(.32f, -.35f, .7f));
            built.Add(toolAnchor);
            anchors = built.ToArray();
        }

        private 감각표현AnchorView CreateWorldAnchor(Transform parent,
            string placementId, string h1Id, string role, Vector3 worldPosition)
        {
            var value = new GameObject(role + "_" + SafeName(placementId));
            value.transform.SetParent(parent, false);
            var anchor = value.AddComponent<감각표현AnchorView>();
            anchor.Configure($"anchor:{placementId}:{role.ToLowerInvariant()}",
                placementId, h1Id, role,
                parent.InverseTransformPoint(worldPosition));
            return anchor;
        }

        private void BuildAudioRouting()
        {
            if (FindObjectsByType<AudioListener>(FindObjectsInactive.Include,
                    FindObjectsSortMode.None).Length == 0)
            {
                var listenerObject = new GameObject("Audio_Listener_Player");
                listenerObject.transform.SetParent(player.transform, false);
                listenerObject.AddComponent<AudioListener>();
            }
            uiCueSource = CreateAudioSource(gameObject,
                "Audio_UI_Cue_2D", 0f, false);
            optionalAreaAmbientSource = CreateAudioSource(gameObject,
                "Audio_Area_Ambient_Optional", 0f, true);
            optionalAreaMusicSource = CreateAudioSource(gameObject,
                "Audio_Area_Music_Optional", 0f, true);
            optionalAreaAmbientSource.volume = .08f;
            optionalAreaMusicSource.volume = .045f;

            foreach (var anchor in anchors.Where(value =>
                         value.RoleCode == 배치표현AnchorRoleCodes.AudioEmitter))
            {
                var source = CreateAudioSource(anchor.gameObject,
                    "Audio_WorldSfx_3D", 1f, false);
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = 1f;
                source.maxDistance = 24f;
                spatialSources[anchor.PlacementStableId] = source;
            }

            clips[감각표현CueCodes.AxePickup] = CreateProceduralClip(
                "Nature_AxePickup_ProceduralFallback", .22f, 1);
            clips[감각표현CueCodes.AxeImpact] = CreateProceduralClip(
                "Nature_AxeImpact_ProceduralFallback", .18f, 2);
            clips[감각표현CueCodes.HarvestCancelled] = CreateProceduralClip(
                "Nature_HarvestCancelled_ProceduralFallback", .16f, 3);
            clips[감각표현CueCodes.TreeFall] = CreateProceduralClip(
                "Nature_TreeFall_ProceduralFallback", .9f, 4);
        }

        private void BuildToolViews()
        {
            var axe = markers.Values.FirstOrDefault(value =>
                value.Kind == Nature생존상호작용Kind.Axe);
            if (axe == null)
            {
                diagnosticCode = "NatureSensoryAxeVisualMissing";
                return;
            }

            firstPersonToolRoot = BuildStaticVisualClone(axe.gameObject,
                player.FirstPersonCamera.transform, "FirstPerson_AxeView");
            firstPersonToolRoot.localPosition = new Vector3(.32f, -.35f, .7f);
            firstPersonToolRoot.localRotation = Quaternion.Euler(8f, -20f, -35f);
            firstPersonToolRoot.localScale = Vector3.one;

            var animator = player.VisualRoot.GetComponentInChildren<Animator>(true);
            var hand = animator != null && animator.isHuman
                ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            if (hand != null)
            {
                thirdPersonToolRoot = BuildStaticVisualClone(axe.gameObject,
                    hand, "ThirdPerson_AxeView");
                thirdPersonToolRoot.localPosition = new Vector3(.03f, .04f, .02f);
                thirdPersonToolRoot.localRotation = Quaternion.Euler(15f, 0f, 90f);
                thirdPersonToolRoot.localScale = Vector3.one;
            }
            else
            {
                diagnosticCode = "NatureSensoryHumanoidRightHandMissing";
            }
            UpdateToolVisibility();
        }

        private void CacheHumanoidBones()
        {
            var animator = player.VisualRoot.GetComponentInChildren<Animator>(true);
            if (animator == null || !animator.isHuman) return;
            leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            if (leftUpperArm != null) leftUpperArmBase = leftUpperArm.localRotation;
            if (rightUpperArm != null) rightUpperArmBase = rightUpperArm.localRotation;
            if (spine != null) spineBase = spine.localRotation;
        }

        private void TickToolAnimation()
        {
            UpdateToolVisibility();
            if (!harvestWorking)
            {
                if (firstPersonToolRoot != null)
                    firstPersonToolRoot.localRotation =
                        Quaternion.Euler(8f, -20f, -35f);
                RestoreHumanoidPose();
                return;
            }

            var cycle = Mathf.Repeat((Time.unscaledTime - workAnimationStartedAt)
                                     * 1.55f, 1f);
            var strike = Mathf.SmoothStep(0f, 1f,
                cycle < .62f ? cycle / .62f : (1f - cycle) / .38f);
            var angle = Mathf.Lerp(-58f, 64f, strike);
            if (firstPersonToolRoot != null)
                firstPersonToolRoot.localRotation = Quaternion.Euler(
                    angle, -20f, -35f);
            if (rightUpperArm != null)
                rightUpperArm.localRotation = rightUpperArmBase
                    * Quaternion.Euler(-52f + angle * .28f, 8f, 35f);
            if (leftUpperArm != null)
                leftUpperArm.localRotation = leftUpperArmBase
                    * Quaternion.Euler(-46f + angle * .24f, -8f, 38f);
            if (spine != null)
                spine.localRotation = spineBase
                    * Quaternion.Euler(-8f, angle * .08f, 0f);
        }

        private void RestoreHumanoidPose()
        {
            if (rightUpperArm != null) rightUpperArm.localRotation = rightUpperArmBase;
            if (leftUpperArm != null) leftUpperArm.localRotation = leftUpperArmBase;
            if (spine != null) spine.localRotation = spineBase;
        }

        private void UpdateToolVisibility()
        {
            if (controller == null) return;
            var hasAxe = controller.State.HasAxe;
            if (firstPersonToolRoot != null)
                firstPersonToolRoot.gameObject.SetActive(hasAxe
                    && player.CurrentMode == 플레이어시점Mode.FirstPerson);
            if (thirdPersonToolRoot != null)
                thirdPersonToolRoot.gameObject.SetActive(hasAxe
                    && player.CurrentMode != 플레이어시점Mode.FirstPerson);
        }

        private void PlayAudio(string cueCode, string targetStableId)
        {
            if (!clips.TryGetValue(cueCode, out var clip)) return;
            if (cueCode == 감각표현CueCodes.AxePickup)
            {
                uiCueSource.PlayOneShot(clip, .42f);
                return;
            }
            if (spatialSources.TryGetValue(targetStableId, out var source))
                source.PlayOneShot(clip, cueCode == 감각표현CueCodes.TreeFall
                    ? .52f : .6f);
            else
                uiCueSource.PlayOneShot(clip, .35f);
        }

        private void PlayFx(string cueCode, string targetStableId)
        {
            if (!fxSystems.TryGetValue(targetStableId, out var system))
            {
                var anchor = anchors.FirstOrDefault(value =>
                    value.PlacementStableId == targetStableId
                    && value.RoleCode == 배치표현AnchorRoleCodes.Fx);
                if (anchor == null) return;
                system = CreateFxSystem(anchor.gameObject);
                fxSystems[targetStableId] = system;
            }
            system.Emit(cueCode == 감각표현CueCodes.TreeFallDust ? 18 : 7);
        }

        private IEnumerator AnimateTreeFall(Nature생존상호작용표식 marker)
        {
            var pivot = new GameObject("TreeFallPresentation_" +
                                       SafeName(marker.StableId)).transform;
            pivot.SetParent(transform, true);
            var bounds = RendererBounds(marker.gameObject);
            pivot.position = new Vector3(bounds.center.x, bounds.min.y,
                bounds.center.z);
            pivot.rotation = marker.transform.rotation;
            var clone = BuildStaticVisualClone(marker.gameObject, pivot,
                "TreeFallVisual");
            clone.SetPositionAndRotation(marker.transform.position,
                marker.transform.rotation);
            var elapsed = 0f;
            const float duration = .9f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var progress = Mathf.Clamp01(elapsed / duration);
                pivot.localRotation = Quaternion.Euler(
                    Mathf.SmoothStep(0f, 84f, progress), 0f, 0f);
                yield return null;
            }
            Destroy(pivot.gameObject);
        }

        private ParticleSystem CreateFxSystem(GameObject owner)
        {
            var system = owner.AddComponent<ParticleSystem>();
            var main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.22f, .48f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.7f, 2.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(.025f, .08f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(.28f, .12f, .045f, 1f),
                new Color(.68f, .42f, .16f, 1f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 28f;
            shape.radius = .08f;
            var emission = system.emission;
            emission.enabled = false;
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader != null)
            {
                fxMaterial ??= new Material(shader)
                {
                    name = "Nature_WoodChip_ProceduralFallback",
                };
                fxMaterial.SetColor("_BaseColor", new Color(.5f, .27f, .08f, 1f));
                renderer.sharedMaterial = fxMaterial;
            }
            return system;
        }

        private static AudioSource CreateAudioSource(GameObject owner,
            string sourceName, float spatialBlend, bool loop)
        {
            var child = new GameObject(sourceName);
            child.transform.SetParent(owner.transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = spatialBlend;
            source.dopplerLevel = spatialBlend > 0f ? .15f : 0f;
            return source;
        }

        private static AudioClip CreateProceduralClip(string clipName,
            float seconds, int kind)
        {
            const int sampleRate = 24000;
            var samples = Mathf.CeilToInt(sampleRate * seconds);
            var data = new float[samples];
            uint noise = (uint)(kind * 7919 + 17);
            var lowPass = 0f;
            for (var index = 0; index < samples; index++)
            {
                var time = index / (float)sampleRate;
                var progress = index / (float)Math.Max(1, samples - 1);
                var envelope = Mathf.Sin(progress * Mathf.PI);
                noise = noise * 1664525u + 1013904223u;
                var rawNoise = ((noise >> 8) & 0xffff) / 32767.5f - 1f;
                lowPass = Mathf.Lerp(lowPass, rawNoise, kind == 4 ? .035f : .18f);
                data[index] = kind switch
                {
                    1 => Mathf.Sin(2f * Mathf.PI * (520f + 360f * progress)
                                   * time) * envelope * .22f,
                    2 => (Mathf.Sin(2f * Mathf.PI * 92f * time) * .38f
                          + lowPass * .42f) * envelope,
                    3 => Mathf.Sin(2f * Mathf.PI * (380f - 120f * progress)
                                   * time) * envelope * .14f,
                    _ => (Mathf.Sin(2f * Mathf.PI * 54f * time) * .34f
                          + lowPass * .58f) * envelope,
                };
            }
            var clip = AudioClip.Create(clipName, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static Transform BuildStaticVisualClone(GameObject source,
            Transform parent, string objectName)
        {
            var root = new GameObject(objectName).transform;
            root.SetParent(parent, false);
            foreach (var sourceRenderer in source.GetComponentsInChildren<
                         MeshRenderer>(true))
            {
                var sourceFilter = sourceRenderer.GetComponent<MeshFilter>();
                if (sourceFilter == null || sourceFilter.sharedMesh == null)
                    continue;
                var child = new GameObject(sourceRenderer.gameObject.name);
                child.transform.SetParent(root, false);
                child.transform.localPosition = source.transform.InverseTransformPoint(
                    sourceRenderer.transform.position);
                child.transform.localRotation = Quaternion.Inverse(
                    source.transform.rotation) * sourceRenderer.transform.rotation;
                child.transform.localScale = DivideScale(
                    sourceRenderer.transform.lossyScale,
                    source.transform.lossyScale);
                var filter = child.AddComponent<MeshFilter>();
                filter.sharedMesh = sourceFilter.sharedMesh;
                var renderer = child.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = sourceRenderer.sharedMaterials;
                renderer.shadowCastingMode = sourceRenderer.shadowCastingMode;
                renderer.receiveShadows = sourceRenderer.receiveShadows;
            }
            return root;
        }

        private static Vector3 DivideScale(Vector3 value, Vector3 divisor)
            => new(Mathf.Abs(divisor.x) < .0001f ? value.x : value.x / divisor.x,
                Mathf.Abs(divisor.y) < .0001f ? value.y : value.y / divisor.y,
                Mathf.Abs(divisor.z) < .0001f ? value.z : value.z / divisor.z);

        private static Vector3 MarkerBoundsCenter(
            Nature생존상호작용표식 marker)
            => RendererBounds(marker.gameObject).center;

        private static Bounds RendererBounds(GameObject value)
        {
            var renderers = value.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return new Bounds(value.transform.position, Vector3.one);
            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
                bounds.Encapsulate(renderers[index].bounds);
            return bounds;
        }

        private static bool ApproximatelyUniform(Vector3 value)
            => Mathf.Abs(value.x - value.y) <= .001f
               && Mathf.Abs(value.y - value.z) <= .001f;

        private static string SafeName(string value)
            => value.Replace(':', '_').Replace('/', '_');

        private void OnDisable()
        {
            harvestWorking = false;
            RestoreHumanoidPose();
        }

        private void OnDestroy()
        {
            RestoreHumanoidPose();
            foreach (var clip in clips.Values)
                if (clip != null) Destroy(clip);
            if (fxMaterial != null) Destroy(fxMaterial);
        }
    }
}
