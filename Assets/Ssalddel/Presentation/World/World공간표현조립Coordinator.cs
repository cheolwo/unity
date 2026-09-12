using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>
    /// LH가 지면과 셀을 준비한 뒤 Sky, 실외, 실내 표현을 순서대로 조립한다.
    /// Simulation 상태나 배치 계획을 변경하지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class World공간표현조립Coordinator : MonoBehaviour
    {
        public static event Action SurfacePrepared;

        public const string LhSurfacePrepared = "LH.SurfacePrepared";
        public const string SkyApplied = "Sky.Applied";
        public const string SkyUnavailable = "Sky.Unavailable";
        public const string ExteriorApplied = "Exterior.Applied";
        public const string InteriorApplied = "Interior.Applied";
        public const string DirectionalLightingValidated =
            "DirectionalLighting.Validated";
        public const string DirectionalLightingRejected =
            "DirectionalLighting.Rejected";
        public const string PresentationReady = "WorldPresentation.Ready";

        [SerializeField] private 실외자산배치Runtime exteriorRuntime = null!;
        [SerializeField] private 실내자산배치Runtime interiorRuntime = null!;
        [SerializeField] private 방향광표현Controller directionalLighting = null!;

        private readonly Dictionary<string, string[]> stageHistory =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string,
            SimulationPlayableLoopEngineTraceEntry[]> interactionTraceHistory =
                new(StringComparer.Ordinal);
        private 공간문법CompositionCatalog compositionCatalog = null!;

        public void Configure(공간문법CompositionCatalog catalog)
        {
            compositionCatalog = catalog;
            exteriorRuntime ??= GetComponent<실외자산배치Runtime>();
            exteriorRuntime ??= gameObject.AddComponent<실외자산배치Runtime>();
            interiorRuntime ??= GetComponent<실내자산배치Runtime>();
            interiorRuntime ??= gameObject.AddComponent<실내자산배치Runtime>();
            directionalLighting ??= GetComponent<방향광표현Controller>();
            directionalLighting ??= gameObject.AddComponent<방향광표현Controller>();
        }

        public bool ApplyPreparedCell(공간LHCellPlanData plan,
            Transform cellRoot, GameObject floor)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (cellRoot == null) throw new ArgumentNullException(nameof(cellRoot));
            var stages = new List<string> { LhSurfacePrepared };

            SurfacePrepared?.Invoke();
            var sky = FindFirstObjectByType<SkyEnginePresenter>(
                FindObjectsInactive.Include);
            if (sky != null)
            {
                sky.RefreshFromAuthority(true);
                stages.Add(SkyApplied);
            }
            else
                stages.Add(SkyUnavailable);
            if (!exteriorRuntime.Apply(plan, cellRoot, floor,
                    compositionCatalog))
            {
                stageHistory[plan.CellKey] = stages.ToArray();
                return false;
            }
            stages.Add(ExteriorApplied);
            if (!interiorRuntime.Apply(plan, cellRoot))
            {
                stageHistory[plan.CellKey] = stages.ToArray();
                return false;
            }
            stages.Add(InteriorApplied);
            directionalLighting ??= GetComponent<방향광표현Controller>();
            directionalLighting ??= gameObject.AddComponent<방향광표현Controller>();
            var timeOfDay = FindFirstObjectByType<월드시간대Presenter>(
                FindObjectsInactive.Include);
            var lightingPassed = directionalLighting.ValidateCell(
                plan.CellKey,
                cellRoot,
                floor,
                timeOfDay?.LightingProfileKey,
                plan.WindowRoleCode == 공간LHWorldCodes.Detail);
            stages.Add(lightingPassed
                ? DirectionalLightingValidated
                : DirectionalLightingRejected);
            if (lightingPassed) stages.Add(PresentationReady);
            stageHistory[plan.CellKey] = stages.ToArray();
            return true;
        }

        public string[] StageHistoryOf(string cellKey)
            => stageHistory.TryGetValue(cellKey ?? string.Empty, out var value)
                ? value.ToArray() : Array.Empty<string>();

        public 방향광표현ValidationReceipt[] DirectionalLightingReceiptsOf(
            string cellKey) => directionalLighting == null
                ? Array.Empty<방향광표현ValidationReceipt>()
                : directionalLighting.ReceiptsOf(cellKey);

        /// <summary>
        /// 권위 WI 기록 뒤에 실제 Unity 표현 엔진 단계를 이어 붙인다.
        /// 이 기록은 진단 전용이며 AuthorityRevision을 변경하지 않는다.
        /// </summary>
        public SimulationPlayableLoopEngineTraceEntry[]
            RecordNatureInteractionPresentation(
                IEnumerable<SimulationPlayableLoopEngineTraceEntry> authorityTrace)
        {
            var source = (authorityTrace
                          ?? Array.Empty<SimulationPlayableLoopEngineTraceEntry>())
                .OrderBy(value => value.Sequence).Select(Clone).ToList();
            if (source.Count == 0) return Array.Empty<
                SimulationPlayableLoopEngineTraceEntry>();
            var context = source[0];
            if (context.WorldInteractionId != "WI-NATURE-13"
                && context.WorldInteractionId != "WI-NATURE-14"
                && context.WorldInteractionId != "WI-NATURE-15")
                return Array.Empty<SimulationPlayableLoopEngineTraceEntry>();
            var revision = source[source.Count - 1].AfterAuthorityRevision;
            var sequence = source.Max(value => value.Sequence);

            if (context.WorldInteractionId == "WI-NATURE-14")
            {
                AddPresentationTrace(source, context, ref sequence,
                    SimulationEngineInteractionComponentCodes.LhSurface,
                    SimulationEngineInteractionPhaseCodes.SurfacePreparation,
                    HasStage(LhSurfacePrepared)
                        ? SimulationEngineInteractionStatusCodes.Reused
                        : SimulationEngineInteractionStatusCodes.Blocked,
                    revision, "lh-streaming.r1");

                var sky = FindFirstObjectByType<SkyEnginePresenter>(
                    FindObjectsInactive.Include);
                if (sky != null) sky.RefreshFromAuthority(true);
                AddPresentationTrace(source, context, ref sequence,
                    SimulationEngineInteractionComponentCodes.SkyPresentation,
                    SimulationEngineInteractionPhaseCodes.AtmosphereProjection,
                    sky != null
                        ? SimulationEngineInteractionStatusCodes.Executed
                        : SimulationEngineInteractionStatusCodes.Blocked,
                    revision, "sky-engine.r2");

                AddPresentationTrace(source, context, ref sequence,
                    SimulationEngineInteractionComponentCodes.ExteriorPlacement,
                    SimulationEngineInteractionPhaseCodes.ExteriorPlacement,
                    HasStage(ExteriorApplied)
                        ? SimulationEngineInteractionStatusCodes.Reused
                        : SimulationEngineInteractionStatusCodes.Blocked,
                    revision, "exterior-placement.r1");
            }

            // 호출자는 Nature생존Controller.RenderState 이후에 이 메서드를 호출한다.
            AddPresentationTrace(source, context, ref sequence,
                SimulationEngineInteractionComponentCodes.InteriorPlacement,
                SimulationEngineInteractionPhaseCodes.InteriorPlacement,
                SimulationEngineInteractionStatusCodes.Executed,
                revision, "nature-cabin-presentation.r1");
            AddPresentationTrace(source, context, ref sequence,
                SimulationEngineInteractionComponentCodes.WorldPresentation,
                SimulationEngineInteractionPhaseCodes.ReturnProjection,
                source.Any(value => value.StatusCode ==
                                    SimulationEngineInteractionStatusCodes.Blocked)
                    ? SimulationEngineInteractionStatusCodes.Blocked
                    : SimulationEngineInteractionStatusCodes.Executed,
                revision, "world-presentation.r1");

            var snapshot = source.OrderBy(value => value.Sequence)
                .Select(Clone).ToArray();
            interactionTraceHistory[context.CommandId] = snapshot;
            return snapshot.Select(Clone).ToArray();
        }

        public SimulationPlayableLoopEngineTraceEntry[] InteractionTraceOf(
            string commandId)
            => interactionTraceHistory.TryGetValue(commandId ?? string.Empty,
                out var value) ? value.Select(Clone).ToArray()
                : Array.Empty<SimulationPlayableLoopEngineTraceEntry>();

        public void CacheCell(string cellKey)
        {
            exteriorRuntime?.SetCellActive(cellKey, false);
            interiorRuntime?.SetCellActive(cellKey, false);
        }

        public void ActivateCell(string cellKey)
        {
            exteriorRuntime?.SetCellActive(cellKey, true);
            interiorRuntime?.SetCellActive(cellKey, true);
        }

        public void ReleaseCell(string cellKey)
        {
            exteriorRuntime?.ReleaseCell(cellKey);
            interiorRuntime?.ReleaseCell(cellKey);
            directionalLighting?.ReleaseCell(cellKey);
            stageHistory.Remove(cellKey ?? string.Empty);
        }

        private bool HasStage(string stageCode) => stageHistory.Values.Any(
            stages => stages.Contains(stageCode, StringComparer.Ordinal));

        private static void AddPresentationTrace(
            ICollection<SimulationPlayableLoopEngineTraceEntry> target,
            SimulationPlayableLoopEngineTraceEntry context, ref int sequence,
            string componentCode, string phaseCode, string statusCode,
            long revision, string componentRevision)
        {
            sequence++;
            target.Add(new SimulationPlayableLoopEngineTraceEntry
            {
                PlayableLoopStableId = context.PlayableLoopStableId,
                WorldInteractionId = context.WorldInteractionId,
                CommandId = context.CommandId,
                AuthorityLocationCode = context.AuthorityLocationCode,
                ComponentCode = componentCode,
                ComponentKindCode =
                    SimulationEngineInteractionComponentKinds.Presentation,
                ComponentRevision = componentRevision,
                PhaseCode = phaseCode,
                Sequence = sequence,
                InputHashSha256 = Hash(context.CommandId + "|" + componentCode
                                          + "|" + phaseCode + "|" + revision),
                OutputHashSha256 = Hash(statusCode + "|" + revision),
                StatusCode = statusCode,
                BeforeAuthorityRevision = revision,
                AfterAuthorityRevision = revision,
                ReasonCode = statusCode ==
                             SimulationEngineInteractionStatusCodes.Blocked
                    ? componentCode + "Unavailable" : string.Empty,
            });
        }

        private static string Hash(string value)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(
                    Encoding.UTF8.GetBytes(value ?? string.Empty)))
                .Replace("-", string.Empty).ToLowerInvariant();
        }

        private static SimulationPlayableLoopEngineTraceEntry Clone(
            SimulationPlayableLoopEngineTraceEntry value) => new()
        {
            PlayableLoopStableId = value.PlayableLoopStableId,
            WorldInteractionId = value.WorldInteractionId,
            CommandId = value.CommandId,
            AuthorityLocationCode = value.AuthorityLocationCode,
            ComponentCode = value.ComponentCode,
            ComponentKindCode = value.ComponentKindCode,
            ComponentRevision = value.ComponentRevision,
            PhaseCode = value.PhaseCode,
            Sequence = value.Sequence,
            InputHashSha256 = value.InputHashSha256,
            OutputHashSha256 = value.OutputHashSha256,
            StatusCode = value.StatusCode,
            BeforeAuthorityRevision = value.BeforeAuthorityRevision,
            AfterAuthorityRevision = value.AfterAuthorityRevision,
            ReasonCode = value.ReasonCode,
        };
    }

    /// <summary>
    /// Simulation이 승인한 실외 계획을 안정 식별자 기준으로 추가·교체·해제한다.
    /// 조립 자료가 없는 구형 셀만 LH 호환 Placements를 읽는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class 실외자산배치Runtime : MonoBehaviour
    {
        private sealed class 배치표현Record
        {
            public string CompositionKey = string.Empty;
            public GameObject Instance = null!;
        }

        private readonly Dictionary<string,
            Dictionary<string, 배치표현Record>> cellObjects =
                new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> cellFailureCodes =
            new(StringComparer.Ordinal);
        private readonly HashSet<string> reportedAmbientFailures =
            new(StringComparer.Ordinal);

        public int ObjectCount(string cellKey)
            => cellObjects.TryGetValue(cellKey ?? string.Empty, out var values)
                ? values.Values.Count(value => value.Instance != null) : 0;

        public string FailureCode(string cellKey)
            => cellFailureCodes.TryGetValue(cellKey ?? string.Empty,
                out var value) ? value : string.Empty;

        public bool TryGetObject(string cellKey, string placementStableId,
            out GameObject instance)
        {
            instance = null!;
            return cellObjects.TryGetValue(cellKey ?? string.Empty,
                       out var values)
                   && values.TryGetValue(placementStableId ?? string.Empty,
                       out var record)
                   && record.Instance != null
                   && (instance = record.Instance) != null;
        }

        public bool Apply(공간LHCellPlanData plan, Transform cellRoot,
            GameObject floor, 공간문법CompositionCatalog catalog)
        {
            if (plan.WindowRoleCode != 공간LHWorldCodes.Detail)
            {
                ReleaseCell(plan.CellKey);
                return true;
            }
            if (plan.WorldAssetAssembly?.IsAvailable == true)
                return ApplyAssembly(plan, cellRoot, floor, catalog,
                    plan.WorldAssetAssembly.ExteriorPlacement);
            return ApplyLegacy(plan, cellRoot, floor, catalog);
        }

        private bool ApplyAssembly(공간LHCellPlanData plan,
            Transform cellRoot, GameObject floor,
            공간문법CompositionCatalog catalog,
            Simulation실외자산배치Plan exterior)
        {
            if (exterior == null
                || exterior.CellStableId != plan.CellKey
                || exterior.SourceLhBasePlanHashSha256 !=
                plan.BasePlanHashSha256)
                return Fail(plan.CellKey,
                    "WorldExteriorPlacementContractMismatch");
            var desired = (exterior.Placements
                           ?? Array.Empty<
                               Simulation세계자산PlacementSnapshot>())
                .OrderBy(value => value.PlacementStableId,
                    StringComparer.Ordinal).ToArray();
            var prefabs = new Dictionary<string, GameObject>(
                StringComparer.Ordinal);
            foreach (var placement in desired)
            {
                var prefab = ResolvePrefab(placement.CompositionKey,
                    catalog);
                if (prefab == null && placement.AuthorityKindCode !=
                    Simulation세계자산배치Codes.AmbientPresentation)
                    return Fail(plan.CellKey,
                        "WorldExteriorPlacementPrefabMissing:" +
                        placement.CompositionKey);
                prefabs[placement.PlacementStableId] = prefab;
            }

            if (!cellObjects.TryGetValue(plan.CellKey, out var current))
            {
                current = new Dictionary<string, 배치표현Record>(
                    StringComparer.Ordinal);
                cellObjects.Add(plan.CellKey, current);
            }
            var desiredIds = desired.Select(value => value.PlacementStableId)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var stale in current.Keys.Where(value =>
                         !desiredIds.Contains(value)).ToArray())
            {
                if (current[stale].Instance != null)
                    Destroy(current[stale].Instance);
                current.Remove(stale);
            }

            foreach (var placement in desired)
            {
                var prefab = prefabs[placement.PlacementStableId];
                if (prefab == null) continue;
                if (!current.TryGetValue(placement.PlacementStableId,
                        out var record)
                    || record.Instance == null
                    || record.CompositionKey != placement.CompositionKey)
                {
                    if (record?.Instance != null) Destroy(record.Instance);
                    record = new 배치표현Record
                    {
                        CompositionKey = placement.CompositionKey,
                        Instance = Instantiate(prefab, cellRoot),
                    };
                    current[placement.PlacementStableId] = record;
                }
                try
                {
                    Configure(record.Instance, placement, plan, cellRoot,
                        floor);
                }
                catch (InvalidOperationException exception)
                {
                    if (record.Instance != null) Destroy(record.Instance);
                    current.Remove(placement.PlacementStableId);
                    if (placement.AuthorityKindCode ==
                        Simulation세계자산배치Codes.AmbientPresentation)
                    {
                        var warningKey = plan.CellKey + ":" +
                                         placement.PlacementStableId + ":" +
                                         exception.Message;
                        if (reportedAmbientFailures.Add(warningKey))
                            Debug.LogWarning(
                                "WorldAmbientPlacementSkipped:" +
                                placement.PlacementStableId + ":" +
                                exception.Message, this);
                        continue;
                    }
                    return Fail(plan.CellKey, exception.Message);
                }
            }
            cellFailureCodes.Remove(plan.CellKey);
            return true;
        }

        private bool Fail(string cellKey, string failureCode)
        {
            var key = cellKey ?? string.Empty;
            var changed = !cellFailureCodes.TryGetValue(key,
                              out var previous)
                          || !string.Equals(previous, failureCode,
                              StringComparison.Ordinal);
            cellFailureCodes[key] = failureCode;
            if (changed) Debug.LogError(failureCode + ":" + cellKey, this);
            return false;
        }

        private bool ApplyLegacy(공간LHCellPlanData plan,
            Transform cellRoot, GameObject floor,
            공간문법CompositionCatalog catalog)
        {
            ReleaseCell(plan.CellKey);
            var current = new Dictionary<string, 배치표현Record>(
                StringComparer.Ordinal);
            cellObjects[plan.CellKey] = current;
            if (catalog == null) return true;
            foreach (var placement in plan.Placements)
            {
                try
                {
                    var entry = catalog.Resolve(placement.CompositionKey);
                    if (entry.Prefab == null) continue;
                    var instance = Instantiate(entry.Prefab, cellRoot);
                    instance.name = "ExteriorPlacement_" +
                                    placement.GeneratedStableId;
                    instance.transform.localPosition = new Vector3(
                        (float)placement.LocalXMeters,
                        (float)placement.LocalYMeters,
                        (float)placement.LocalZMeters);
                    instance.transform.localRotation = Quaternion.Euler(
                        0f, (float)placement.RotationDegrees, 0f);
                    instance.transform.localScale = Vector3.one *
                                                    (float)placement.UniformScale;
                    if (!string.IsNullOrWhiteSpace(
                            placement.PlacementRuleRevision)
                        && !AlignAndValidate(instance, SurfaceWorldY(plan,
                            cellRoot, floor, placement.LocalXMeters,
                            placement.LocalYMeters, placement.LocalZMeters),
                            out _))
                    {
                        Destroy(instance);
                        ReleaseCell(plan.CellKey);
                        return false;
                    }
                    World방향광표현TargetCatalog.AttachIfSupported(
                        instance,
                        placement.GeneratedStableId,
                        placement.CompositionKey,
                        string.Empty);
                    current[placement.GeneratedStableId] =
                        new 배치표현Record
                        {
                            CompositionKey = placement.CompositionKey,
                            Instance = instance,
                        };
                }
                catch (InvalidOperationException)
                {
                    // 구형 주변 표현의 누락은 지면·통행 준비를 막지 않는다.
                }
            }
            cellFailureCodes.Remove(plan.CellKey);
            return true;
        }

        private static void Configure(GameObject instance,
            Simulation세계자산PlacementSnapshot placement,
            공간LHCellPlanData plan, Transform cellRoot, GameObject floor)
        {
            instance.name = NameOf(placement);
            instance.transform.SetParent(cellRoot, false);
            instance.transform.localPosition = new Vector3(
                (float)placement.LocalXMeters,
                (float)placement.LocalYMeters,
                (float)placement.LocalZMeters);
            instance.transform.localRotation = Quaternion.Euler(0f,
                (float)placement.RotationDegrees, 0f);
            instance.transform.localScale = Vector3.one *
                                            (float)placement.UniformScale;
            var surfaceY = SurfaceWorldY(plan, cellRoot, floor,
                placement.LocalXMeters, placement.LocalYMeters,
                placement.LocalZMeters);
            if (!AlignAndValidate(instance, surfaceY,
                    out var surfaceFailure))
                throw new InvalidOperationException(
                    "WorldExteriorPlacementSurfaceInvalid:" +
                    placement.PlacementStableId + ":" + surfaceFailure);
            ConfigureNatureMarker(instance, placement);
            World방향광표현TargetCatalog.AttachIfSupported(
                instance,
                placement.PlacementStableId,
                placement.CompositionKey,
                placement.CategoryCode);
        }

        private static string NameOf(
            Simulation세계자산PlacementSnapshot placement)
            => placement.CategoryCode switch
            {
                "NatureResourceNode" => "Nature_Authority_Resource_" +
                                        placement.PlacementStableId,
                "NatureDroppedTimber" => "Nature_DroppedTimber_Synty_" +
                                         placement.PlacementStableId,
                "NatureCabin" => "Nature_Cabin_SyntyGardenShed_" +
                                 placement.PlacementStableId,
                "NatureWorkbench" => "Nature_Workbench_Authoritative_" +
                                     placement.PlacementStableId,
                _ => "ExteriorPlacement_" + placement.PlacementStableId,
            };

        private static void ConfigureNatureMarker(GameObject instance,
            Simulation세계자산PlacementSnapshot placement)
        {
            var interactive = placement.CategoryCode == "NatureCabin"
                              || placement.CategoryCode ==
                              "NatureDroppedTimber"
                              || placement.CategoryCode ==
                              "NatureWorkbench"
                              || (placement.CategoryCode ==
                                  "NatureResourceNode"
                                  && placement.StateCode ==
                                  SimulationNatureSurvivalCodes.Standing);
            var marker = instance.GetComponent<Nature생존상호작용표식>();
            if (!interactive)
            {
                if (marker != null) Destroy(marker);
                return;
            }
            marker ??= instance.AddComponent<Nature생존상호작용표식>();
            marker.Kind = placement.CategoryCode switch
            {
                "NatureCabin" => Nature생존상호작용Kind.Cabin,
                "NatureDroppedTimber" =>
                    Nature생존상호작용Kind.DroppedTimber,
                "NatureWorkbench" => Nature생존상호작용Kind.Workbench,
                _ => Nature생존상호작용Kind.HarvestTree,
            };
            marker.StableId = placement.SourceChangeStableIds?.FirstOrDefault()
                              ?? placement.PlacementStableId;
            marker.UsesSyntyAsset = true;
            marker.VisualSourceStableId = placement.CompositionKey;
            if (placement.CategoryCode == "NatureCabin"
                && instance.GetComponentInChildren<SkyExposureVolume>(true)
                == null)
            {
                var exposureObject = new GameObject("SkyExposureVolume");
                exposureObject.transform.SetParent(instance.transform, false);
                var exposureCollider =
                    exposureObject.AddComponent<BoxCollider>();
                exposureCollider.center = new Vector3(0f, 1.25f, 0f);
                exposureCollider.size = new Vector3(3.6f, 2.5f, 3.6f);
                exposureObject.AddComponent<SkyExposureVolume>()
                    .Configure(exposureCollider);
            }
        }

        private static GameObject ResolvePrefab(string compositionKey,
            공간문법CompositionCatalog catalog)
        {
            var nature = Resources.Load<Nature생존VisualCatalog>(
                Nature생존VisualCatalog.ResourcesPath);
            var direct = compositionKey switch
            {
                "Nature.Tree.Standing" => nature?.TreePrefab,
                "Nature.Tree.Stump" => nature?.StumpPrefab,
                "Nature.Timber.Dropped" => nature?.TimberLogPrefab,
                "Nature.Cabin.Completed" => nature?.CabinPrefab,
                "Nature.Cabin.Construction" => nature?.CabinPrefab,
                "Nature.Workbench.Operational" => nature?.WorkbenchPrefab,
                "Nature.Workbench.Construction" => nature?.WorkbenchPrefab,
                _ => null,
            };
            if (direct != null) return direct;
            if (catalog == null) return null;
            try { return catalog.Resolve(compositionKey).Prefab; }
            catch (InvalidOperationException) { return null; }
        }

        public void SetCellActive(string cellKey, bool active)
        {
            if (!cellObjects.TryGetValue(cellKey ?? string.Empty,
                    out var values)) return;
            foreach (var value in values.Values)
                if (value.Instance != null) value.Instance.SetActive(active);
        }

        public void ReleaseCell(string cellKey)
        {
            var key = cellKey ?? string.Empty;
            cellFailureCodes.Remove(key);
            reportedAmbientFailures.RemoveWhere(value => value.StartsWith(
                key + ":", StringComparison.Ordinal));
            if (!cellObjects.TryGetValue(key, out var values)) return;
            foreach (var value in values.Values)
                if (value.Instance != null) Destroy(value.Instance);
            cellObjects.Remove(key);
        }

        private static bool AlignAndValidate(GameObject instance,
            float surfaceWorldY, out string failureCode)
        {
            failureCode = string.Empty;
            try
            {
                배치객체표면정렬Utility.AlignVisibleBottomToWorldHeight(
                    instance.transform, surfaceWorldY);
                var validation = 배치객체표면정렬Utility
                    .ValidateVisibleBottomClearance(instance.transform,
                        surfaceWorldY);
                if (validation.Passed) return true;
                failureCode = string.Join(":", new[]
                {
                    validation.ResultCode,
                    validation.SurfaceWorldY.ToString("R"),
                    validation.VisibleBottomWorldY.ToString("R"),
                    validation.Error.ToString("R"),
                });
                return false;
            }
            catch (InvalidOperationException exception)
            {
                failureCode = exception.Message;
                return false;
            }
        }

        private static float SurfaceWorldY(공간LHCellPlanData plan,
            Transform cellRoot, GameObject floor, double localX,
            double localY, double localZ)
        {
            if (plan.Surface != null && plan.Surface.HasGeneratedGrid
                && plan.Surface.SurfaceModeCode != "Flat")
            {
                var grid = plan.Surface;
                var half = grid.CellSizeMeters * .5d;
                var x01 = Math.Max(0d, Math.Min(1d,
                    (localX + half) / grid.CellSizeMeters));
                var z01 = Math.Max(0d, Math.Min(1d,
                    (localZ + half) / grid.CellSizeMeters));
                var gx = x01 * (grid.SampleResolution - 1);
                var gz = z01 * (grid.SampleResolution - 1);
                var x0 = Math.Min(grid.SampleResolution - 1,
                    (int)Math.Floor(gx));
                var z0 = Math.Min(grid.SampleResolution - 1,
                    (int)Math.Floor(gz));
                var x1 = Math.Min(grid.SampleResolution - 1, x0 + 1);
                var z1 = Math.Min(grid.SampleResolution - 1, z0 + 1);
                var tx = gx - x0;
                var tz = gz - z0;
                double H(int x, int z) =>
                    grid.HeightMeters[z * grid.SampleResolution + x];
                var a = H(x0, z0) + (H(x1, z0) - H(x0, z0)) * tx;
                var b = H(x0, z1) + (H(x1, z1) - H(x0, z1)) * tx;
                var sampledY = a + (b - a) * tz;
                return cellRoot.TransformPoint(new Vector3((float)localX,
                    (float)sampledY, (float)localZ)).y;
            }
            if (floor != null && floor.activeInHierarchy)
            {
                var renderer = floor.GetComponent<Renderer>();
                if (renderer != null) return renderer.bounds.max.y;
            }
            return cellRoot.TransformPoint(new Vector3((float)localX,
                (float)localY, (float)localZ)).y;
        }
    }

    /// <summary>
    /// Simulation의 고정 Interior 본문과 변화 Overlay를 오두막 배치 객체 아래에
    /// 조립한다. 같은 계획 hash는 재사용하고 새 hash만 다시 조립한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class 실내자산배치Runtime : MonoBehaviour
    {
        private sealed class 실내CellRecord
        {
            public string PlanHashSha256 = string.Empty;
            public GameObject Root = null!;
        }

        private readonly Dictionary<string, 실내CellRecord> cellRoots =
            new(StringComparer.Ordinal);

        public bool Apply(공간LHCellPlanData plan, Transform cellRoot)
        {
            if (plan.WindowRoleCode != 공간LHWorldCodes.Detail)
            {
                ReleaseCell(plan.CellKey);
                return true;
            }
            var interior = plan.WorldAssetAssembly?.InteriorPlacement;
            if (interior == null)
                return ApplyCompatibilityRoot(plan, cellRoot);
            if (interior.CellStableId != plan.CellKey
                || interior.SourceLhBasePlanHashSha256 !=
                plan.BasePlanHashSha256)
                return false;
            if (cellRoots.TryGetValue(plan.CellKey, out var current)
                && current.Root != null
                && current.PlanHashSha256 ==
                interior.InteriorPlacementPlanHashSha256)
                return true;

            ReleaseCell(plan.CellKey);
            var root = new GameObject("InteriorPlacementRoot_" +
                                      plan.CellKey.Replace(':', '_'));
            root.transform.SetParent(cellRoot, false);
            var exterior = GetComponent<실외자산배치Runtime>();
            var objects = new Dictionary<string, Transform>(
                StringComparer.Ordinal);
            foreach (var body in interior.InteriorPlanBodies
                         ?? Array.Empty<
                             SimulationInteriorPlacementPlanBodySnapshot>())
            {
                if (exterior == null || !exterior.TryGetObject(plan.CellKey,
                        body.BuildingPlacementStableId, out var building))
                {
                    Destroy(root);
                    return false;
                }
                var bodyRoot = new GameObject("InteriorBody_" +
                    body.BuildingPlacementStableId.Replace(':', '_'));
                bodyRoot.transform.SetParent(root.transform, false);
                bodyRoot.transform.localPosition =
                    building.transform.localPosition;
                bodyRoot.transform.localRotation =
                    building.transform.localRotation;
                bodyRoot.transform.localScale =
                    building.transform.localScale;
                objects[body.BuildingPlacementStableId] =
                    bodyRoot.transform;
                foreach (var item in body.Placements.OrderBy(value =>
                             value.PlacementStableId, StringComparer.Ordinal))
                {
                    if (string.IsNullOrWhiteSpace(item.VisualKey)) continue;
                    var prefab = ResolveInteriorPrefab(item.VisualKey);
                    if (prefab == null)
                    {
                        Destroy(root);
                        Destroy(bodyRoot);
                        return false;
                    }
                    var instance = Instantiate(prefab, bodyRoot.transform);
                    instance.name = "InteriorPlacement_" +
                                    item.PlacementStableId;
                    instance.transform.localPosition = new Vector3(
                        (float)item.LocalX, (float)item.LocalY,
                        (float)item.LocalZ);
                    instance.transform.localRotation = Quaternion.Euler(0f,
                        (float)item.LocalRotationDegrees, 0f);
                    instance.transform.localScale = Vector3.one *
                                                    (float)item.UniformScale;
                    objects[item.PlacementStableId] = instance.transform;
                }
            }
            foreach (var overlay in interior.OverlayPlacements
                         ?? Array.Empty<Simulation세계자산PlacementSnapshot>())
            {
                if (!objects.TryGetValue(overlay.ParentPlacementStableId,
                        out var parent))
                {
                    Destroy(root);
                    return false;
                }
                var prefab = ResolveInteriorPrefab(overlay.CompositionKey);
                if (prefab == null)
                {
                    Destroy(root);
                    return false;
                }
                var instance = Instantiate(prefab, parent);
                instance.name = "InteriorOverlay_" +
                                overlay.PlacementStableId;
                instance.transform.localPosition = new Vector3(0f, .8f, 0f);
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one *
                                                (float)overlay.UniformScale;
            }
            cellRoots[plan.CellKey] = new 실내CellRecord
            {
                PlanHashSha256 = interior.InteriorPlacementPlanHashSha256,
                Root = root,
            };
            return true;
        }

        private bool ApplyCompatibilityRoot(공간LHCellPlanData plan,
            Transform cellRoot)
        {
            ReleaseCell(plan.CellKey);
            var root = new GameObject("InteriorPlacementRoot_" +
                                      plan.CellKey.Replace(':', '_'));
            root.transform.SetParent(cellRoot, false);
            cellRoots[plan.CellKey] = new 실내CellRecord
            {
                PlanHashSha256 = "compatibility-empty",
                Root = root,
            };
            return true;
        }

        private static GameObject ResolveInteriorPrefab(string visualKey)
        {
            var catalog = Resources.Load<Nature생존VisualCatalog>(
                Nature생존VisualCatalog.ResourcesPath);
            return visualKey switch
            {
                "Nature.Shelter.Bedroll" => catalog?.SleepBedPrefab,
                "Nature.Storage.Rack.Small" => catalog?.StorageRackPrefab,
                "Nature.Work.Table.Small" => catalog?.WorkTablePrefab,
                "Nature.Storage.Timber.Small" =>
                    catalog?.StoredTimberPrefab,
                "Nature.Storage.Timber.Half" =>
                    catalog?.StoredTimberPrefab,
                "Nature.Storage.Timber.Full" =>
                    catalog?.StoredTimberPrefab,
                "Nature.Work.ToolBox" => catalog?.ToolBoxPrefab,
                _ => null,
            };
        }

        public void SetCellActive(string cellKey, bool active)
        {
            if (cellRoots.TryGetValue(cellKey ?? string.Empty, out var value)
                && value.Root != null) value.Root.SetActive(active);
        }

        public void ReleaseCell(string cellKey)
        {
            if (!cellRoots.TryGetValue(cellKey ?? string.Empty,
                    out var value)) return;
            if (value.Root != null) Destroy(value.Root);
            cellRoots.Remove(cellKey ?? string.Empty);
        }
    }
}
