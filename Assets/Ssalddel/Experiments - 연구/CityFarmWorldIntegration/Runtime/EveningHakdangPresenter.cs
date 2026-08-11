using System;
using Ssalddel.Unity.Learning;
using Ssalddel.Unity.Presentation.World;
using UnityEngine;
using UnityEngine.UI;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration
{
    public static class EveningHakdangActionCodes
    {
        public const string Reset = "Reset";
        public const string Preview = "Preview";
        public const string Confirm = "Confirm";
        public const string ApplyTick = "ApplyTick";
    }

    [DefaultExecutionOrder(210)]
    public sealed class EveningHakdangPresenter : MonoBehaviour
    {
        [SerializeField] private 월드시간대Presenter timeOfDay = null!;
        [SerializeField] private Text timeText = null!;
        [SerializeField] private Text titleText = null!;
        [SerializeField] private Text teachingText = null!;
        [SerializeField] private Text promptText = null!;
        [SerializeField] private Text statusText = null!;
        [SerializeField] private Text effectText = null!;
        [SerializeField] private Text sourceText = null!;
        [SerializeField] private InputField reflectionInput = null!;

        private readonly 저녁학당SimulationValidator validator = new();
        private readonly 저녁학당추천Engine recommendationEngine = new();
        private 저녁학당SimulationEngine engine = null!;
        private 저녁학당SimulationSnapshot snapshot = null!;
        private 저녁학당학습Preview? preview;
        private 저녁학당학습Command? command;
        private string selectedContentStableId = 저녁학당SimulationFixture.FoolContentStableId;
        private string recommendationRationale = string.Empty;

        public 저녁학당SimulationSnapshot CurrentSnapshot => snapshot;
        public 저녁학당학습Preview? CurrentPreview => preview;
        public 저녁학당학습Command? CurrentCommand => command;

        public bool ValidateWiring() => timeOfDay != null && timeText != null && titleText != null
            && teachingText != null && promptText != null && statusText != null && effectText != null
            && sourceText != null && reflectionInput != null;

        public void Configure(월드시간대Presenter timePresenter, Text time, Text title, Text teaching,
            Text prompt, Text status, Text effect, Text source, InputField reflection)
        {
            timeOfDay = timePresenter;
            timeText = time;
            titleText = title;
            teachingText = teaching;
            promptText = prompt;
            statusText = status;
            effectText = effect;
            sourceText = source;
            reflectionInput = reflection;
            Ensure();
            ResetStudy();
        }

        private void Start()
        {
            Ensure();
            if (snapshot == null) ResetStudy();
        }

        public void ExecuteAction(string actionCode)
        {
            switch (actionCode)
            {
                case EveningHakdangActionCodes.Reset: ResetStudy(); break;
                case EveningHakdangActionCodes.Preview: PreviewStudy(); break;
                case EveningHakdangActionCodes.Confirm: ConfirmStudy(); break;
                case EveningHakdangActionCodes.ApplyTick: ApplyTick(); break;
                default: throw new InvalidOperationException("EveningHakdangActionUnknown:" + actionCode);
            }
        }

        public void ResetStudy()
        {
            Ensure();
            snapshot = 저녁학당SimulationFixture.CreateFoolEvening();
            preview = null;
            command = null;
            ApplyMorningAction(오전행동TagCodes.UnknownSkipped);
            if (reflectionInput != null) reflectionInput.text = "지금 선택의 결과를 아직 모른다.";
            timeOfDay?.ApplyNowForTests(21f / 24f);
            Apply();
        }

        public void PreviewStudy()
        {
            preview = engine.Preview(snapshot, selectedContentStableId);
            command = null;
            Apply();
        }

        public void ConfirmStudy()
        {
            if (preview == null) throw new InvalidOperationException("EveningStudyPreviewMissing");
            command = engine.Confirm(snapshot, preview, reflectionInput.text);
            Apply();
        }

        public void ApplyTick()
        {
            if (command == null) throw new InvalidOperationException("EveningStudyCommandMissing");
            snapshot = engine.Tick(snapshot, command);
            preview = null;
            command = null;
            timeOfDay?.ApplyNowForTests(5.5f / 24f);
            Apply();
        }

        public void RunFoolStudyPath()
        {
            ResetStudy();
            PreviewStudy();
            ConfirmStudy();
            ApplyTick();
        }

        public void RunChariotStudyPath()
        {
            ResetStudy();
            ApplyMorningAction(오전행동TagCodes.CargoLoaded);
            reflectionInput.text = "오늘 쓴 힘과 판단을 한 방향으로 통합한다.";
            PreviewStudy();
            ConfirmStudy();
            ApplyTick();
        }

        public void ApplyMorningAction(string outcomeTag)
        {
            var action = new 오전행동Summary
            {
                StableId = "morning-action:sim.potato.r" + snapshot.DataRevision,
                Revision = snapshot.DataRevision,
                OccurredAt = snapshot.SimulationDate.AddHours(-11),
                ActionCode = "PotatoWork",
                Summary = outcomeTag == 오전행동TagCodes.CargoLoaded
                    ? "감자 상차와 이동 준비를 수행했다."
                    : "불확실한 조건을 남긴 채 오전 작업을 마쳤다.",
                OutcomeTags = new[] { outcomeTag },
                SourceStableIds = new[] { "product:potato" },
            };
            var request = recommendationEngine.CreateRequest(snapshot.DataRevision,
                new[] { action }, snapshot.AvailableContents);
            var recommendation = recommendationEngine.Fallback(request);
            selectedContentStableId = recommendation.ContentStableId;
            recommendationRationale = recommendation.Rationale;
            if (titleText != null) Apply();
        }

        private void Ensure()
        {
            if (engine == null) engine = new 저녁학당SimulationEngine(validator);
        }

        private void Apply()
        {
            var content = Array.Find(snapshot.AvailableContents,
                value => value.StableId == selectedContentStableId) ?? snapshot.AvailableContents[0];
            var completed = snapshot.DayPhaseCode == 하루단계Codes.Day;
            timeText.text = completed ? "NEXT DAWN  05:30" : "EVENING  21:00";
            titleText.text = content.Title;
            teachingText.text = content.TeachingSummary;
            promptText.text = content.ReflectionPrompt;
            statusText.text = completed
                ? "STUDIED · 다음 날 규칙 활성화"
                : command != null ? "CONFIRMED · TICK을 적용하세요"
                : preview != null ? "PREVIEW · 명시적 확인 필요"
                : "오늘의 깊은 학습 1회 가능";
            effectText.text = completed
                ? (content.TargetStatCode == 내면StatCodes.Awareness ? "알아차림  "
                    + snapshot.InnerState.알아차림 : "의지  " + snapshot.InnerState.의지)
                    + "   ·   " + content.GrantedRuleCode + " ACTIVE\n" + recommendationRationale
                : preview != null
                    ? preview.TargetStatCode + "  " + preview.StatBefore + " → " + preview.StatAfter
                        + "   ·   " + preview.GrantedRuleCode + " NEXT DAY"
                    : "오전 행동 기반 추천 · " + recommendationRationale;
            sourceText.text = "HONGIK ACADEMY · " + content.SourceVideoId + " @ "
                + content.SourceStartSeconds + "s · NOTE rev " + content.Revision;
        }
    }

    [RequireComponent(typeof(Button))]
    public sealed class EveningHakdangActionButton : MonoBehaviour
    {
        [SerializeField] private EveningHakdangPresenter presenter = null!;
        [SerializeField] private string actionCode = string.Empty;

        public void Configure(EveningHakdangPresenter value, string code)
        {
            presenter = value;
            actionCode = code;
        }

        private void Awake() => GetComponent<Button>().onClick.AddListener(Execute);

        public void Execute()
        {
            if (presenter == null || string.IsNullOrWhiteSpace(actionCode))
                throw new InvalidOperationException("EveningHakdangButtonInvalid");
            presenter.ExecuteAction(actionCode);
        }
    }
}
