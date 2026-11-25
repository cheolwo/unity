using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ssalddel.RoadReviewGame
{
    /// <summary>기존 관측 후보를 비교하는 로컬 게임 표현. 업무 권위나 별도 승인 저장소를 만들지 않습니다.</summary>
    [DefaultExecutionOrder(-32000)]
    public sealed class RoadReviewGameController : MonoBehaviour
    {
        private const string DefaultManifest = "C:/Users/user/Documents/ChatGPT/배달/edit-kit/private/road-review-game/20261025-r1/mission-manifest.json";
        private readonly Color ink = new Color(.87f, .94f, .96f);
        private readonly Color muted = new Color(.55f, .68f, .73f);
        private readonly Color teal = new Color(.19f, .83f, .70f);
        private readonly Color gold = new Color(1f, .78f, .35f);
        private Font font;
        private Canvas canvas;
        private RectTransform surface;
        private RawImage photo, boardImage;
        private Text title, chapter, status, photoCaption, hint, score, selectionSummary, scope;
        private RectTransform choices;
        private GameObject modal, roadRoot;
        private Text modalText;
        private Button registerButton, reviewButton, retryButton;
        private readonly List<Button> segmentButtons = new List<Button>();
        private readonly HashSet<string> selected = new HashSet<string>();
        private readonly HashSet<string> openedEvidence = new HashSet<string>();
        private readonly List<LineRenderer> roadLines = new List<LineRenderer>();
        private readonly List<Material> boardMaterials = new List<Material>();
        private readonly List<JObject> segments = new List<JObject>();
        private readonly List<JObject> evidence = new List<JObject>();
        private readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        private JObject manifest, candidate, listing;
        private JArray missions;
        private JObject progress;
        private string serverBase = "http://localhost:8791", token, pendingBody;
        private string progressPath, pendingPath;
        private int missionIndex, frameIndex, frameGeneration;
        private bool busy, showHints;
        private Camera mapCamera;
        private RenderTexture boardTexture;
        private Material roadMaterial;
        private Vector3 boardCenter;
        [SerializeField] private Shader boardShader;

        // 빌드 사본에서 Shader 참조를 결속해 이름 기반 검색만으로 인한 shader stripping을 피합니다.
        public void ConfigureBoardShader(Shader shader) => boardShader = shader;

        private void Awake()
        {
            // 빌드에서만 주입되는 독립 모듈입니다. 저장된 공식 Scene 이름과 내용은 바꾸지 않습니다.
            var scene = gameObject.scene;
            if (scene.IsValid()) scene.name = "RoadReviewLocal";
            Application.runInBackground = true;
            Application.targetFrameRate = 45;
            Screen.SetResolution(1440, 900, FullScreenMode.Windowed);
        }

        private IEnumerator Start()
        {
            var fonts = Font.GetOSInstalledFontNames();
            var korean = fonts.FirstOrDefault(x => x.IndexOf("Malgun", StringComparison.OrdinalIgnoreCase) >= 0)
                         ?? fonts.FirstOrDefault(x => x.IndexOf("맑은", StringComparison.Ordinal) >= 0)
                         ?? "Arial";
            font = Font.CreateDynamicFontFromOSFont(korean, 26);
            BuildInterface();
            progressPath = Path.Combine(Application.persistentDataPath, "road-review-game-progress-v1.json");
            pendingPath = Path.Combine(Application.persistentDataPath, "road-review-game-pending-v1.json");
            try
            {
                progress = File.Exists(progressPath) ? JObject.Parse(File.ReadAllText(progressPath)) : new JObject();
                var args = Environment.GetCommandLineArgs();
                var arg = Array.IndexOf(args, "--road-game-manifest");
                var path = arg >= 0 && arg + 1 < args.Length ? args[arg + 1] : DefaultManifest;
                manifest = JObject.Parse(File.ReadAllText(path));
                var baseValue = manifest.Value<string>("serverBaseUrl") ?? "http://localhost:8791";
                if (!RoadReviewContract.IsAllowedServer(baseValue)) throw new InvalidDataException("기존 로컬 검토 서버 주소만 사용할 수 있습니다.");
                serverBase = baseValue.TrimEnd('/');
                missions = manifest["missions"] as JArray;
                if (missions == null || missions.Count == 0) throw new InvalidDataException("플레이할 관측 목록이 없습니다.");
                if (File.Exists(pendingPath)) pendingBody = File.ReadAllText(pendingPath);
            }
            catch (Exception e) { SetStatus("시작 자료를 읽지 못했습니다. " + e.Message, true); yield break; }
            yield return LoadMission();
            if (!string.IsNullOrEmpty(pendingBody))
            {
                SetStatus("이전 등록의 결과가 불확실합니다. ‘등록 재시도’는 같은 요청을 재사용합니다. 먼저 기록을 확인해 주세요.", true);
                retryButton.gameObject.SetActive(true);
            }
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || busy || candidate == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame && modal != null) modal.SetActive(false);
            if (modal != null && modal.activeSelf) return;
            if (keyboard.leftArrowKey.wasPressedThisFrame) ChangeFrame(-1);
            if (keyboard.rightArrowKey.wasPressedThisFrame) ChangeFrame(1);
            if (keyboard.hKey.wasPressedThisFrame) ToggleHints();
        }

        private void BuildInterface()
        {
            if (FindFirstObjectByType<EventSystem>() == null)
                new GameObject("RoadReviewInput", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var root = new GameObject("RoadReviewCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1440, 900);
            scaler.matchWidthOrHeight = .5f;
            surface = root.GetComponent<RectTransform>();
            Panel(surface, "배경", 0, 0, 1440, 900, new Color(.035f, .065f, .09f));
            Panel(surface, "왼쪽상단강조", 30, 28, 5, 53, teal);
            Label(surface, "동네 길 탐험", 48, 25, 680, 40, 29, ink, FontStyle.Bold);
            Label(surface, "내가 촬영한 장면으로 길의 기록을 다듬어 보세요", 49, 68, 850, 28, 15, muted);
            score = Label(surface, "비교 0  ·  보류 0", 1050, 36, 350, 35, 18, teal, FontStyle.Bold, TextAnchor.MiddleRight);
            chapter = Label(surface, "관측을 불러오는 중", 34, 116, 1000, 29, 16, gold, FontStyle.Bold);
            ButtonAt(surface, "다시 연결", 1230, 112, 176, 37, () => { if (!busy) StartCoroutine(LoadMission()); }, false);
            Panel(surface, "관측카드", 30, 163, 735, 509, new Color(.07f, .11f, .145f));
            title = Label(surface, "실제 관측 사진", 49, 179, 694, 39, 21, ink, FontStyle.Bold);
            photo = Raw(surface, "관측사진", 48, 230, 699, 393, new Color(.015f, .025f, .035f));
            // 사진은 받은 실제 화각에 맞춰 고정 틀 안에서 비율을 유지합니다.
            photoCaption = Label(surface, "사진을 읽고 있습니다", 49, 632, 690, 26, 14, muted);
            ButtonAt(surface, "← 이전 장면", 49, 691, 155, 39, () => ChangeFrame(-1));
            ButtonAt(surface, "다음 장면 →", 218, 691, 155, 39, () => ChangeFrame(1));
            ButtonAt(surface, "단서 보기  H", 388, 691, 155, 39, ToggleHints);
            Label(surface, "← / → 사진 이동   ·   H 단서   ·   Esc 확인창 닫기", 49, 744, 700, 28, 14, muted);
            Panel(surface, "도로카드", 786, 163, 621, 509, new Color(.07f, .11f, .145f));
            Label(surface, "장면과 이어지는 길을 골라 주세요", 807, 179, 565, 34, 20, ink, FontStyle.Bold);
            boardImage = Raw(surface, "도로도식", 804, 227, 585, 219, Color.white);
            Label(surface, "실제 선분 좌표의 도식  ·  지도 배경 / 건물 복원이 아닙니다", 810, 450, 566, 22, 12, muted);
            choices = Panel(surface, "선분선택", 804, 481, 585, 177, Color.clear);
            hint = Label(surface, "간판과 갈림점을 비교해 보세요. 위치를 모르면 보류해도 괜찮습니다.", 806, 682, 586, 77, 16, muted);
            selectionSummary = Label(surface, "아직 선택하지 않았습니다", 807, 767, 585, 27, 15, teal);
            status = Label(surface, "기존 검토 서버에 연결 중입니다", 32, 810, 890, 53, 14, muted);
            scope = Label(surface, "비공개 검토  ·  선분별 통과 시각·전체 경로·공개 허가는 별도입니다", 31, 867, 1130, 23, 12, muted);
            ButtonAt(surface, "판단 보류", 944, 817, 137, 45, Defer);
            ButtonAt(surface, "다음 관측 →", 1092, 817, 142, 45, NextMission);
            reviewButton = ButtonAt(surface, "선택 검토", 1245, 817, 162, 45, ReviewSelection, true);
            retryButton = ButtonAt(surface, "등록 재시도", 566, 691, 180, 39, () => { if (!busy) StartCoroutine(SubmitPending()); });
            retryButton.gameObject.SetActive(false);
            BuildMapCamera();
            BuildConfirmation();
        }

        private void BuildConfirmation()
        {
            modal = Panel(surface, "명시적등록확인", 0, 0, 1440, 900, new Color(0, 0, 0, .86f)).gameObject;
            var card = Panel(modal.transform, "확인카드", 350, 220, 740, 460, new Color(.08f, .14f, .18f));
            Label(card, "선택한 관측 연결을 등록할까요?", 30, 27, 680, 45, 25, ink, FontStyle.Bold);
            modalText = Label(card, "", 31, 87, 678, 238, 18, ink);
            ButtonAt(card, "돌아가서 비교", 29, 371, 237, 48, () => modal.SetActive(false));
            registerButton = ButtonAt(card, "확인한 선분만 등록", 289, 371, 420, 48, BeginRegistration, true);
            modal.SetActive(false);
        }

        private IEnumerator LoadMission()
        {
            if (busy || missions == null || missions.Count == 0) yield break;
            busy = true;
            selected.Clear(); candidate = null; frameGeneration++;
            openedEvidence.Clear();
            segments.Clear(); evidence.Clear();
            foreach (Transform child in choices) Destroy(child.gameObject);
            segmentButtons.Clear();
            if (roadRoot != null) Destroy(roadRoot);
            roadLines.Clear();
            reviewButton.interactable = false;
            photo.texture = null;
            title.text = "실제 관측 사진";
            if (modal != null) modal.SetActive(false);
            SetStatus("원본·후보·보호 근거를 현재 검토 서버에서 확인하고 있습니다.");
            var mission = missions[missionIndex] as JObject;
            chapter.text = string.Format("관측 {0} / {1}     원본 {2}     ·     비교 → 선택 → 검토", missionIndex + 1, missions.Count, mission.Value<string>("sourceCode"));
            using (var request = UnityWebRequest.Get(serverBase + "/api/segment-candidates?sourceId=" + UnityWebRequest.EscapeURL(mission.Value<string>("sourceId"))))
            {
                request.timeout = 35;
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    busy = false; SetStatus("검토 서버에 연결하지 못했습니다. 기존 지도 검토함을 켜고 ‘다시 연결’을 눌러 주세요. " + request.error, true); yield break;
                }
                try
                {
                    listing = JObject.Parse(request.downloadHandler.text);
                    candidate = (listing["candidates"] as JArray)?.OfType<JObject>().FirstOrDefault(x => x.Value<string>("candidateId") == mission.Value<string>("candidateId"));
                    if (candidate == null) throw new InvalidDataException("현재 판본에서 관측 후보를 찾지 못했습니다.");
                    segments.Clear(); segments.AddRange((candidate["segments"] as JArray ?? new JArray()).OfType<JObject>());
                    evidence.Clear(); evidence.AddRange((candidate["evidence"] as JArray ?? new JArray()).OfType<JObject>());
                    title.text = FriendlyTitle(mission.Value<string>("sourceCode"));
                    frameIndex = 0; showHints = false;
                    DrawChoices(); DrawMap(); RefreshSelection(); RefreshHint(); RefreshScore();
                    var holds = candidate["holdReasons"]?.ToString(Formatting.None);
                    SetStatus(candidate.Value<bool>("approvable") ? "사진의 앞뒤 흐름을 비교하고, 실제로 확인한 선분만 선택해 주세요." : "지금은 등록할 수 없는 관측입니다. " + holds, !candidate.Value<bool>("approvable"));
                    Debug.Log("RoadReviewGameMissionLoaded:" + mission.Value<string>("sourceCode") + ":evidence=" + evidence.Count + ":segments=" + segments.Count);
                }
                catch (Exception e) { candidate = null; SetStatus("관측 자료를 읽지 못했습니다. " + e.Message, true); }
            }
            busy = false;
            if (candidate != null) yield return LoadFrame();
        }

        private static string FriendlyTitle(string sourceCode)
        {
            if (sourceCode == "0103") return "갈림길의 방향을 찾아서";
            if (sourceCode == "0093") return "큰길에서 왼쪽으로";
            if (sourceCode == "0101") return "골목을 잇는 세 조각";
            return "장면과 길의 연결을 찾아서";
        }

        private void DrawChoices()
        {
            foreach (Transform child in choices) Destroy(child.gameObject);
            segmentButtons.Clear();
            for (var i = 0; i < segments.Count; i++)
            {
                var captured = i;
                var segment = segments[i];
                var text = string.Format("○  {0}    {1}    {2}", segment.Value<string>("segmentCode"), segment.Value<string>("roadName"), Direction(segment.Value<string>("direction")));
                var button = ButtonAt(choices, text, 0, i * 53, 585, 46, () => ToggleSegment(captured));
                button.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
                button.GetComponentInChildren<Text>().rectTransform.anchoredPosition = new Vector2(15, 0);
                button.GetComponentInChildren<Text>().rectTransform.sizeDelta = new Vector2(555, 46);
                segmentButtons.Add(button);
            }
        }

        private static string Direction(string value) => value == "reverse" ? "역방향 후보" : value == "forward" ? "정방향 후보" : "방향 미확인";

        public void ToggleSegment(int index)
        {
            if (busy || candidate == null || !string.IsNullOrEmpty(pendingBody) || index < 0 || index >= segments.Count) return;
            var id = segments[index].Value<string>("segmentId");
            if (!selected.Add(id)) selected.Remove(id);
            RefreshSelection();
        }

        private void RefreshSelection()
        {
            for (var i = 0; i < segmentButtons.Count; i++)
            {
                var segment = segments[i];
                var isSelected = selected.Contains(segment.Value<string>("segmentId"));
                segmentButtons[i].GetComponent<Image>().color = isSelected ? new Color(.10f, .39f, .35f) : new Color(.12f, .20f, .25f);
                segmentButtons[i].GetComponentInChildren<Text>().text = string.Format("{0}  {1}    {2}    {3}", isSelected ? "●" : "○", segment.Value<string>("segmentCode"), segment.Value<string>("roadName"), Direction(segment.Value<string>("direction")));
                foreach (var line in roadLines.Where(x => x.name == segment.Value<string>("segmentId")))
                {
                    line.startColor = line.endColor = isSelected ? gold : teal;
                    SetMaterialColor(line.sharedMaterial, isSelected ? gold : teal);
                    line.widthMultiplier = isSelected ? .30f : .18f;
                }
            }
            selectionSummary.text = "사진 " + openedEvidence.Count + "/" + evidence.Count + "  ·  " + (selected.Count == 0 ? "아직 선택하지 않았습니다" : "선택 " + selected.Count + "개");
            reviewButton.interactable = candidate != null && candidate.Value<bool>("approvable") && selected.Count > 0 && AllEvidenceOpened() && string.IsNullOrEmpty(pendingBody);
        }

        private void ChangeFrame(int delta)
        {
            if (candidate == null || evidence.Count == 0 || busy) return;
            frameIndex = (frameIndex + delta + evidence.Count) % evidence.Count;
            StartCoroutine(LoadFrame());
        }

        private IEnumerator LoadFrame()
        {
            if (evidence.Count == 0) { photoCaption.text = "현재 제공 가능한 관측 사진이 없습니다"; yield break; }
            var generation = ++frameGeneration;
            var frame = evidence[frameIndex];
            var evidenceId = frame.Value<string>("evidenceId");
            photoCaption.text = string.Format("장면 {0}/{1}   ·   실제 원본 PTS {2:0.000}초", frameIndex + 1, evidence.Count, frame.Value<double>("sourceSeconds"));
            photo.texture = null;
            // 서버는 모든 사진 요청에서 최신 보호 근거를 재확인합니다. 오래된 캐시를 바로 보여주지 않습니다.
            using (var request = UnityWebRequestTexture.GetTexture(serverBase + "/api/segment-candidates/frame/" + UnityWebRequest.EscapeURL(evidenceId)))
            {
                request.timeout = 30;
                yield return request.SendWebRequest();
                if (generation != frameGeneration) yield break;
                if (request.result != UnityWebRequest.Result.Success)
                {
                    photoCaption.text += "   ·   사진 제공 보류";
                    SetStatus("관측 사진을 제공할 수 없습니다. 보호 또는 원본 판본 변경 가능성이 있어 다시 확인해 주세요.", true);
                    yield break;
                }
                var texture = DownloadHandlerTexture.GetContent(request);
                if (textures.TryGetValue(evidenceId, out var old)) Destroy(old);
                textures[evidenceId] = texture;
                photo.texture = texture;
                photo.color = Color.white;
                var rect = photo.rectTransform;
                var ratio = (float)texture.width / texture.height;
                var height = Mathf.Min(393, 699 / ratio);
                rect.sizeDelta = new Vector2(height * ratio, height);
                rect.anchoredPosition = new Vector2(48 + (699 - height * ratio) / 2, -(230 + (393 - height) / 2));
                MarkProgress("seen", false);
                openedEvidence.Add(evidenceId);
                RefreshSelection();
            }
        }

        private void ToggleHints() { showHints = !showHints; RefreshHint(); }

        private void RefreshHint()
        {
            if (candidate == null) return;
            hint.text = showHints
                ? (candidate.Value<string>("label") ?? "공개 간판과 갈림점을 앞뒤 장면에서 비교해 보세요.") + "\n후보는 추론입니다. 다른 길일 가능성이 남으면 보류해 주세요."
                : "간판 → 갈림점 → 다음 장면을 비교해 보세요.\n모르면 보류해도 괜찮습니다. 정답 맞히기 점수는 없습니다.";
            hint.fontSize = showHints ? 14 : 16;
        }

        private void Defer()
        {
            if (busy || candidate == null) return;
            MarkProgress("deferred", true);
            SetStatus("보류로 남겼습니다. 위치나 통과 시각이 확정된 것은 아닙니다. 다음 관측으로 넘어갈 수 있습니다.");
        }

        private void NextMission()
        {
            if (busy || missions == null) return;
            if (!string.IsNullOrEmpty(pendingBody)) { SetStatus("결과가 불확실한 등록 요청이 있습니다. 재시도로 같은 요청 결과를 먼저 확인해 주세요.", true); return; }
            if (candidate != null && AllEvidenceOpened()) MarkProgress("compared", false);
            missionIndex = (missionIndex + 1) % missions.Count;
            StartCoroutine(LoadMission());
        }

        private void MarkProgress(string state, bool overwrite)
        {
            if (progress == null || candidate == null) return;
            var id = candidate.Value<string>("candidateId");
            var current = progress[id] as JObject;
            if (current == null) current = new JObject();
            if (overwrite || current["state"] == null || (state == "compared" && current.Value<string>("state") == "seen")) current["state"] = state;
            var seen = current["seenEvidenceIds"] as JArray ?? new JArray();
            if (evidence.Count > frameIndex)
            {
                var evidenceId = evidence[frameIndex].Value<string>("evidenceId");
                if (!seen.Values<string>().Contains(evidenceId)) seen.Add(evidenceId);
            }
            current["seenEvidenceIds"] = seen;
            current["updatedAtUtc"] = DateTime.UtcNow.ToString("O");
            progress[id] = current;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(progressPath));
                File.WriteAllText(progressPath, progress.ToString(Formatting.Indented));
            }
            catch (Exception e) { SetStatus("플레이 진행을 로컬에 저장하지 못했습니다. " + e.Message, true); }
            RefreshScore();
        }

        private void RefreshScore()
        {
            if (progress == null) return;
            var compared = progress.Properties().Count(x => x.Value["state"]?.Value<string>() == "compared");
            var deferred = progress.Properties().Count(x => x.Value["state"]?.Value<string>() == "deferred");
            score.text = "비교 " + compared + "  ·  보류 " + deferred;
        }

        private void ReviewSelection()
        {
            if (busy || candidate == null || selected.Count == 0 || !candidate.Value<bool>("approvable") || !string.IsNullOrEmpty(pendingBody)) return;
            if (!AllEvidenceOpened()) { SetStatus("관측 사진을 모두 앞뒤로 비교한 뒤 선택 검토를 눌러 주세요.", true); return; }
            modalText.text = string.Join("\n", segments.Where(x => selected.Contains(x.Value<string>("segmentId"))).Select(x => "• " + x.Value<string>("segmentCode") + "  " + x.Value<string>("roadName")))
                + string.Format("\n\n관측창: 원본 {0:0.000}–{1:0.000}초\n같은 관측 범위에 이 선분이 등장한다는 연결입니다.\n선분별 진입·이탈 / 전체 통과 / 공개 검수와는 다릅니다.\n기존 지도 검토 기록에 명시적으로 등록합니다.", candidate.Value<double>("sourceInSeconds"), candidate.Value<double>("sourceOutSeconds"));
            modal.SetActive(true);
        }

        private void BeginRegistration()
        {
            if (busy || candidate == null || selected.Count == 0 || !AllEvidenceOpened() || !string.IsNullOrEmpty(pendingBody)) return;
            try
            {
                var body = RoadReviewContract.BuildApproval(candidate, listing, selected, Guid.NewGuid().ToString());
                pendingBody = body.ToString(Formatting.None);
            }
            catch (Exception e) { modal.SetActive(false); SetStatus("선택 근거를 다시 확인해 주세요. " + e.Message, true); return; }
            try { File.WriteAllText(pendingPath, pendingBody); }
            catch (Exception e) { pendingBody = null; SetStatus("재시도 근거를 저장하지 못해 등록을 시작하지 않았습니다. " + e.Message, true); return; }
            modal.SetActive(false);
            StartCoroutine(SubmitPending());
        }

        private IEnumerator SubmitPending()
        {
            if (busy || string.IsNullOrEmpty(pendingBody)) yield break;
            busy = true;
            reviewButton.interactable = false;
            retryButton.interactable = false;
            SetStatus("기존 검토 기록에 연결을 등록하고 있습니다.");
            using (var session = UnityWebRequest.Get(serverBase + "/api/session"))
            {
                session.timeout = 20;
                yield return session.SendWebRequest();
                if (session.result != UnityWebRequest.Result.Success) { RegistrationUncertain("검토 세션에 연결하지 못했습니다."); yield break; }
                try { token = JObject.Parse(session.downloadHandler.text).Value<string>("token"); }
                catch (Exception) { token = null; }
                if (string.IsNullOrEmpty(token)) { RegistrationUncertain("검토 세션 토큰을 읽지 못했습니다."); yield break; }
            }
            using (var request = new UnityWebRequest(serverBase + "/api/segment-candidates/action", "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(pendingBody));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Origin", serverBase);
                request.SetRequestHeader("X-Review-Token", token);
                request.timeout = 45;
                yield return request.SendWebRequest();
                if (request.responseCode == 409 || (request.responseCode >= 400 && request.responseCode < 500))
                {
                    ClearPending(); selected.Clear(); busy = false;
                    SetStatus("현재 근거나 판본이 바뀌어 등록되지 않았습니다. 다시 연결해 사진과 선택을 새로 검토해 주세요. HTTP " + request.responseCode, true);
                    candidate = null; yield break;
                }
                if (request.result != UnityWebRequest.Result.Success) { RegistrationUncertain("등록 결과를 확인하지 못했습니다. " + request.error); yield break; }
                try
                {
                    var result = JObject.Parse(request.downloadHandler.text);
                    if (result.Value<bool>("ok") != true) throw new InvalidDataException("서버가 성공을 확인하지 않았습니다.");
                    var registeredCandidateId = JObject.Parse(pendingBody).Value<string>("candidateId");
                    ClearPending();
                    SaveComparedProgress(registeredCandidateId);
                    selected.Clear(); RefreshSelection();
                    SetStatus("선택한 관측 연결이 기존 지도 검토 기록에 등록됐습니다. 전체 경로나 공개 검수 완료를 뜻하지 않습니다.");
                    Debug.Log("RoadReviewGameExplicitRegistrationConfirmed");
                }
                catch (Exception e) { RegistrationUncertain(e.Message); yield break; }
            }
            busy = false;
            retryButton.interactable = true;
        }

        private void RegistrationUncertain(string message)
        {
            busy = false;
            retryButton.gameObject.SetActive(true);
            retryButton.interactable = true;
            SetStatus(message + " ‘등록 재시도’는 같은 요청 식별자와 선택으로 결과를 확인합니다.", true);
        }

        private bool AllEvidenceOpened() => evidence.Count > 0 && evidence.All(x => openedEvidence.Contains(x.Value<string>("evidenceId")));

        private void SaveComparedProgress(string candidateId)
        {
            var record = progress[candidateId] as JObject ?? new JObject();
            record["state"] = "compared";
            record["updatedAtUtc"] = DateTime.UtcNow.ToString("O");
            progress[candidateId] = record;
            try { File.WriteAllText(progressPath, progress.ToString(Formatting.Indented)); }
            catch (Exception e) { Debug.LogWarning("RoadReviewProgressSave:" + e.Message); }
            RefreshScore();
        }

        private void ClearPending()
        {
            pendingBody = null;
            try { if (File.Exists(pendingPath)) File.Delete(pendingPath); }
            catch (Exception e) { Debug.LogWarning("RoadReviewPendingCleanup:" + e.Message); }
            retryButton.gameObject.SetActive(false);
        }

        private void SetStatus(string message, bool warning = false)
        {
            status.text = message;
            status.color = warning ? gold : muted;
        }

        private void BuildMapCamera()
        {
            var cameraObject = new GameObject("RoadReviewBoardCamera");
            mapCamera = cameraObject.AddComponent<Camera>();
            mapCamera.clearFlags = CameraClearFlags.SolidColor;
            mapCamera.backgroundColor = new Color(.045f, .085f, .11f);
            mapCamera.cullingMask = 1 << 30;
            mapCamera.orthographic = true;
            mapCamera.nearClipPlane = .1f;
            mapCamera.farClipPlane = 300f;
            boardTexture = new RenderTexture(1170, 438, 16);
            mapCamera.targetTexture = boardTexture;
            boardImage.texture = boardTexture;
            var shader = boardShader ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
            roadMaterial = new Material(shader);
        }

        private void DrawMap()
        {
            if (roadRoot != null) Destroy(roadRoot);
            foreach (var material in boardMaterials) if (material != null) Destroy(material);
            boardMaterials.Clear();
            roadRoot = new GameObject("RoadReviewActualSegmentDiagram");
            roadLines.Clear();
            var rawLines = new List<Tuple<string, List<Vector2>>>();
            foreach (var segment in segments)
            {
                var lines = RoadReviewContract.ReadLines(segment);
                foreach (var line in lines) if (line.Count > 1) rawLines.Add(Tuple.Create(segment.Value<string>("segmentId"), line));
            }
            var points = rawLines.SelectMany(x => x.Item2).ToList();
            if (points.Count == 0) return;
            var minX = points.Min(x => x.x); var maxX = points.Max(x => x.x);
            var minY = points.Min(x => x.y); var maxY = points.Max(x => x.y);
            var latitudeScale = Mathf.Cos((minY + maxY) * .5f * Mathf.Deg2Rad);
            var width = (maxX - minX) * latitudeScale;
            var height = maxY - minY;
            var scale = 16f / Mathf.Max(.00001f, width, height);
            Vector3 Project(Vector2 p) => new Vector3((p.x - (minX + maxX) / 2) * latitudeScale * scale, .1f, (p.y - (minY + maxY) / 2) * scale);
            var gridColor = new Color(.10f, .17f, .20f);
            for (var i = -12; i <= 12; i += 2)
            {
                CreateLine("grid", new[] { new Vector3(i, 0, -12), new Vector3(i, 0, 12) }, .025f, gridColor);
                CreateLine("grid", new[] { new Vector3(-12, 0, i), new Vector3(12, 0, i) }, .025f, gridColor);
            }
            foreach (var line in rawLines)
            {
                var renderer = CreateLine(line.Item1, line.Item2.Select(Project).ToArray(), .18f, teal);
                roadLines.Add(renderer);
                foreach (var point in new[] { line.Item2.First(), line.Item2.Last() })
                {
                    var node = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    node.name = "실제선분끝점"; node.layer = 30;
                    node.transform.SetParent(roadRoot.transform, false);
                    node.transform.position = Project(point);
                    node.transform.localScale = Vector3.one * .35f;
                    var material = new Material(roadMaterial);
                    boardMaterials.Add(material);
                    SetMaterialColor(material, gold);
                    node.GetComponent<Renderer>().material = material;
                    Destroy(node.GetComponent<Collider>());
                }
            }
            boardCenter = new Vector3(0, 0, 0);
            mapCamera.transform.position = new Vector3(0, 24, -14);
            mapCamera.transform.LookAt(boardCenter);
            mapCamera.orthographicSize = 9;
            Debug.Log("RoadReviewGameDiagram:segments=" + segments.Count + ":actualGeometryLines=" + rawLines.Count);
        }

        private LineRenderer CreateLine(string name, Vector3[] points, float width, Color color)
        {
            var go = new GameObject(name); go.layer = 30; go.transform.SetParent(roadRoot.transform, false);
            var line = go.AddComponent<LineRenderer>();
            var material = new Material(roadMaterial);
            SetMaterialColor(material, color);
            boardMaterials.Add(material);
            line.sharedMaterial = material;
            line.positionCount = points.Length; line.SetPositions(points);
            line.widthMultiplier = width; line.startColor = line.endColor = color;
            line.numCapVertices = 4; line.numCornerVertices = 3;
            return line;
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        }

        private RectTransform Panel(Transform parent, string name, float x, float y, float w, float h, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false);
            SetRect(rect, x, y, w, h); go.GetComponent<Image>().color = color;
            return rect;
        }

        private RawImage Raw(Transform parent, string name, float x, float y, float w, float h, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            var rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false); SetRect(rect, x, y, w, h);
            var raw = go.GetComponent<RawImage>(); raw.color = color;
            return raw;
        }

        private Text Label(Transform parent, string text, float x, float y, float w, float h, int size, Color color, FontStyle style = FontStyle.Normal, TextAnchor anchor = TextAnchor.UpperLeft)
        {
            var go = new GameObject(text.Length > 22 ? text.Substring(0, 22) : text, typeof(RectTransform), typeof(Text));
            var rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false); SetRect(rect, x, y, w, h);
            var label = go.GetComponent<Text>(); label.font = font; label.text = text; label.color = color;
            label.fontSize = size; label.fontStyle = style; label.alignment = anchor; label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }

        private Button ButtonAt(Transform parent, string text, float x, float y, float w, float h, Action action, bool primary = false)
        {
            var rect = Panel(parent, text, x, y, w, h, primary ? new Color(.16f, .61f, .51f) : new Color(.12f, .20f, .25f));
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>();
            var colors = button.colors; colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f); colors.disabledColor = new Color(.45f, .45f, .45f); button.colors = colors;
            var label = Label(rect, text, 0, 0, w, h, 16, ink, FontStyle.Bold, TextAnchor.MiddleCenter);
            button.onClick.AddListener(() => action());
            return button;
        }

        private static void SetRect(RectTransform rect, float x, float y, float w, float h)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h);
        }

        private void OnDestroy()
        {
            foreach (var texture in textures.Values) if (texture != null) Destroy(texture);
            if (boardTexture != null) { boardTexture.Release(); Destroy(boardTexture); }
            if (roadMaterial != null) Destroy(roadMaterial);
            foreach (var material in boardMaterials) if (material != null) Destroy(material);
        }
    }

}
