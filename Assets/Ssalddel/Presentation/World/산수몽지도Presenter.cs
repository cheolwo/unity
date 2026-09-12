using System;
using System.Globalization;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>
    /// 산수몽 호송에서 얻은 지도 지식을 표시한다. M 지도는 플레이어 조작만 막고
    /// WorldTick·NPC·호송·위협 진행을 멈추지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class 산수몽지도Presenter : MonoBehaviour
    {
        private const string MiniRectKey = "ssalddel.hud.meng-map.mini.r1";
        private const string MiniVisibleKey = "ssalddel.hud.meng-map.mini.visible.r1";
        private const string StatusRectKey = "ssalddel.hud.meng-map.status.r1";
        private const string StatusVisibleKey = "ssalddel.hud.meng-map.status.visible.r1";
        [SerializeField] private 플레이어경관Controller player = null!;
        [SerializeField] private string actorStableId = "actor:sim:player-survivor";
        private 산수몽지도ViewState state = new();
        private Rect miniRect = new(Screen.width - 292f, 18f, 274f, 188f);
        private Rect statusRect = new(16f, 14f, 310f, 92f);
        private Rect fullRect = new(100f, 72f, 1000f, 680f);
        private string seasonLabel = "절기 미확인";
        private string hexagramLabel = "☵☶ 산수몽";
        private bool mapOpen;
        private bool miniVisible = true;
        private bool statusVisible = true;
        private bool loadedSettings;

        public bool IsMapOpen => mapOpen;
        public bool WorldContinuesWhileOpen => true;
        public 산수몽지도ViewState Current => state;

        public void Configure(플레이어경관Controller playerController,
            string actorId)
        {
            player = playerController;
            actorStableId = actorId ?? string.Empty;
        }

        public void ApplyExploration(SimulationWorldExplorationStateSnapshot snapshot,
            string currentSeasonLabel, string currentHexagramLabel = "☵☶ 산수몽")
        {
            state = 산수몽지도Projection.Create(snapshot, actorStableId);
            seasonLabel = string.IsNullOrWhiteSpace(currentSeasonLabel)
                ? "절기 미확인" : currentSeasonLabel;
            hexagramLabel = string.IsNullOrWhiteSpace(currentHexagramLabel)
                ? "☵☶ 산수몽" : currentHexagramLabel;
        }

        public void SetMapOpen(bool open)
        {
            mapOpen = open;
            player?.SetModalInputBlocked(open);
        }

        private void Awake() => LoadSettings();

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.mKey.wasPressedThisFrame) SetMapOpen(!mapOpen);
            else if (mapOpen && keyboard.escapeKey.wasPressedThisFrame)
                SetMapOpen(false);
            if (keyboard.nKey.wasPressedThisFrame)
            {
                miniVisible = !miniVisible;
                SaveSettings();
            }
            if (keyboard.jKey.wasPressedThisFrame)
            {
                statusVisible = !statusVisible;
                SaveSettings();
            }
        }

        private void OnDisable() => SetMapOpen(false);

        private void OnGUI()
        {
            LoadSettings();
            if (statusVisible && !mapOpen)
                statusRect = GUI.Window(4020, statusRect, DrawStatus,
                    "절기·괘 · J 숨김");
            if (miniVisible && !mapOpen)
                miniRect = GUI.Window(4021, miniRect, DrawMiniMap,
                    "주변 지도 · N 숨김");
            if (mapOpen)
                fullRect = GUI.Window(4022, fullRect, DrawFullMap,
                    "전체 지도 · M/Esc 닫기 · 세계 시간은 계속 흐름");
        }

        private void DrawStatus(int id)
        {
            GUILayout.Label(seasonLabel + "  ·  " + hexagramLabel);
            if (GUILayout.Button("크기 + / -"))
            {
                statusRect.width = statusRect.width < 360f ? 390f : 310f;
                statusRect.height = statusRect.height < 110f ? 126f : 92f;
                SaveSettings();
            }
            GUI.DragWindow(new Rect(0f, 0f, statusRect.width, 28f));
        }

        private void DrawMiniMap(int id)
        {
            GUILayout.Label("현재 위치  " + (string.IsNullOrWhiteSpace(state.CurrentTileKey)
                ? "미확인" : state.CurrentTileKey));
            foreach (var entry in state.MiniMapEntries)
                GUILayout.Label(Symbol(entry) + " " + entry.KoreanLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("크기 + / -"))
            {
                miniRect.width = miniRect.width < 330f ? 350f : 274f;
                miniRect.height = miniRect.height < 230f ? 250f : 188f;
                SaveSettings();
            }
            GUI.DragWindow(new Rect(0f, 0f, miniRect.width, 28f));
        }

        private void DrawFullMap(int id)
        {
            GUILayout.Label("알렉스의 설명은 ··· 윤곽, 실제 통과는 ● 확정으로 표시됩니다.");
            GUILayout.Space(10f);
            foreach (var entry in state.FullMapEntries)
            {
                var timing = entry.ConfirmedWorldTick.HasValue
                    ? " · 확인 Tick " + entry.ConfirmedWorldTick.Value
                    : " · 설명으로 인지";
                GUILayout.Label(Symbol(entry) + "  " + entry.KoreanLabel
                    + "  [" + entry.KindCode + "]" + timing);
            }
            GUILayout.FlexibleSpace();
            GUILayout.Label("지도 열람은 이동·공격 입력만 막습니다. NPC, 호송, 위협, WorldTick은 정지하지 않습니다.");
            GUI.DragWindow(new Rect(0f, 0f, fullRect.width, 30f));
        }

        private static string Symbol(SimulationMapKnowledgeEntrySnapshot entry)
            => entry.KnowledgeLevelCode == SimulationMapKnowledgeLevelCodes.Confirmed
                ? "●" : "···";

        private void LoadSettings()
        {
            if (loadedSettings) return;
            loadedSettings = true;
            miniVisible = PlayerPrefs.GetInt(MiniVisibleKey, 1) != 0;
            statusVisible = PlayerPrefs.GetInt(StatusVisibleKey, 1) != 0;
            miniRect = ReadRect(MiniRectKey, miniRect, 240f, 160f);
            statusRect = ReadRect(StatusRectKey, statusRect, 260f, 82f);
        }

        private static Rect ReadRect(string key, Rect fallback,
            float minimumWidth, float minimumHeight)
        {
            var serialized = PlayerPrefs.GetString(key, string.Empty);
            var parts = serialized.Split('|');
            if (parts.Length == 4
                && float.TryParse(parts[0], NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var x)
                && float.TryParse(parts[1], NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var y)
                && float.TryParse(parts[2], NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var width)
                && float.TryParse(parts[3], NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var height))
                return new Rect(x, y, Mathf.Max(minimumWidth, width),
                    Mathf.Max(minimumHeight, height));
            return fallback;
        }

        private void SaveSettings()
        {
            PlayerPrefs.SetInt(MiniVisibleKey, miniVisible ? 1 : 0);
            PlayerPrefs.SetInt(StatusVisibleKey, statusVisible ? 1 : 0);
            PlayerPrefs.SetString(MiniRectKey, SerializeRect(miniRect));
            PlayerPrefs.SetString(StatusRectKey, SerializeRect(statusRect));
            PlayerPrefs.Save();
        }

        private static string SerializeRect(Rect rect)
            => string.Join("|", rect.x.ToString(CultureInfo.InvariantCulture),
                rect.y.ToString(CultureInfo.InvariantCulture),
                rect.width.ToString(CultureInfo.InvariantCulture),
                rect.height.ToString(CultureInfo.InvariantCulture));
    }
}
