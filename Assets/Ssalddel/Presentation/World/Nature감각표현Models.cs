using System;
using System.Linq;
using Ssalddel.Unity.PresentationContracts;
using Ssalddel.Unity.Runtime.World;

namespace Ssalddel.Unity.Presentation.World
{
    [Serializable]
    public sealed class Nature감각표현Frame
    {
        public string PhaseCode = 감각표현단계Codes.Available;
        public string AnimationIntentCode = string.Empty;
        public string AudioCueCode = string.Empty;
        public string FxCueCode = string.Empty;
        public string UiCueCode = string.Empty;
        public string TargetStableId = string.Empty;
        public string CueStableId = string.Empty;
        public int CueOrdinal;
        public long ObservedWorldRevision;
        public string AreaAmbientCueCode = 감각표현CueCodes.ForestAmbient;
        public string AreaMusicCueCode = 감각표현CueCodes.NatureExplorationMusic;
        public bool PresentationOnly = true;
        public bool MutatesCanonicalState;
        public bool ConfirmsBusinessCompletion;
    }

    /// <summary>
    /// Nature 권위 상태 사본의 전후 차이만 읽어 Animation·Audio·FX 의도를 만듭니다.
    /// 출력은 Presentation 전용이며 작업 완료나 WorldTick을 호출하지 않습니다.
    /// </summary>
    public sealed class Nature감각표현상태Projector
    {
        public Nature감각표현Frame Project(Nature생존State? previous,
            Nature생존State current)
        {
            if (current == null) throw new ArgumentNullException(nameof(current));
            var currentWork = HarvestWork(current);
            var previousWork = previous == null ? null : HarvestWork(previous);

            if (previous == null)
                return Baseline(current, currentWork);

            if (!previous.HasAxe && current.HasAxe)
                return Frame(current, 감각표현단계Codes.Acquired,
                    target: "pickup:nature-safe-clearing:basic-axe",
                    audio: 감각표현CueCodes.AxePickup,
                    ui: "ui:nature:axe-acquired");

            if (currentWork != null)
            {
                var impact = previousWork != null
                             && previousWork.TargetStableId ==
                             currentWork.TargetStableId
                             && currentWork.CompletedSeconds >
                             previousWork.CompletedSeconds;
                return Frame(current, 감각표현단계Codes.Working,
                    target: currentWork.TargetStableId,
                    animation: "animation:nature:axe-swing",
                    audio: impact ? 감각표현CueCodes.AxeImpact : string.Empty,
                    fx: impact ? 감각표현CueCodes.WoodChip : string.Empty,
                    ui: "ui:nature:harvest-progress",
                    ordinal: currentWork.CompletedSeconds);
            }

            if (previousWork != null)
            {
                var completed = IsStump(current, previousWork.TargetStableId);
                return Frame(current,
                    completed ? 감각표현단계Codes.Completed
                        : 감각표현단계Codes.Cancelled,
                    target: previousWork.TargetStableId,
                    animation: "animation:common:idle",
                    audio: completed ? 감각표현CueCodes.TreeFall
                        : 감각표현CueCodes.HarvestCancelled,
                    fx: completed ? 감각표현CueCodes.TreeFallDust : string.Empty,
                    ui: completed ? "ui:nature:harvest-completed"
                        : "ui:nature:harvest-cancelled",
                    ordinal: previousWork.CompletedSeconds);
            }

            var transitionedNode = previous.ResourceNodes.FirstOrDefault(before =>
                before.StateCode == Nature생존Codes.Standing
                && IsStump(current, before.StableId));
            if (transitionedNode != null)
                return Frame(current, 감각표현단계Codes.Completed,
                    target: transitionedNode.StableId,
                    animation: "animation:common:idle",
                    audio: 감각표현CueCodes.TreeFall,
                    fx: 감각표현CueCodes.TreeFallDust,
                    ui: "ui:nature:harvest-completed");

            return Baseline(current, null);
        }

        private static Nature감각표현Frame Baseline(Nature생존State state,
            Nature생존WorkState? currentWork)
        {
            if (currentWork != null)
                return Frame(state, 감각표현단계Codes.Working,
                    target: currentWork.TargetStableId,
                    animation: "animation:nature:axe-swing",
                    ui: "ui:nature:harvest-progress",
                    ordinal: currentWork.CompletedSeconds);
            return Frame(state, state.HasAxe
                    ? 감각표현단계Codes.Acquired
                    : 감각표현단계Codes.Available,
                target: state.HasAxe ? string.Empty
                    : "pickup:nature-safe-clearing:basic-axe",
                ui: state.HasAxe ? "ui:nature:axe-acquired"
                    : "ui:nature:axe-available");
        }

        private static Nature생존WorkState? HarvestWork(Nature생존State state)
            => state.ActiveWork?.KindCode == Nature생존Codes.Harvest
                ? state.ActiveWork : null;

        private static bool IsStump(Nature생존State state, string targetStableId)
            => state.ResourceNodes.Any(value =>
                value.StableId == targetStableId
                && value.StateCode == Nature생존Codes.Stump);

        private static Nature감각표현Frame Frame(Nature생존State state,
            string phase, string target, string animation = "",
            string audio = "", string fx = "", string ui = "",
            int ordinal = 0)
            => new()
            {
                PhaseCode = phase,
                AnimationIntentCode = animation,
                AudioCueCode = audio,
                FxCueCode = fx,
                UiCueCode = ui,
                TargetStableId = target,
                CueOrdinal = ordinal,
                CueStableId = string.IsNullOrWhiteSpace(audio)
                               && string.IsNullOrWhiteSpace(fx)
                    ? string.Empty
                    : $"presentation-cue:{state.SessionStableId}:"
                      + $"{target}:{phase}:{ordinal}:r{state.Revision}",
                ObservedWorldRevision = state.Revision,
            };
    }
}
