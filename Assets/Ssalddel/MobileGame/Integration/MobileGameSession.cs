using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Ssalddel.Sagajeong.Core;
using UnityEngine;

namespace Ssalddel.MobileGame
{
    /// <summary>모바일의 가상 시계와 확정 저장을 한 곳에서 소유합니다.</summary>
    public sealed class MobileGameSession : MonoBehaviour, IMobileGameSession
    {
        private const string SaveSchema = "hongdal-mobile-session.v1";
        private SagajeongSimulationCore core;
        private MobileGameExperienceLog experiences;
        private IMobileGameFileExchange files;
        private JObject lastReply = new JObject { ["ok"] = true };
        private string storageDirectory;
        private string sessionId;
        private string notice;
        private long elapsedMs;
        private int speed;
        private int resumeSpeed = 1;
        private bool initialized;
        private bool hasGame;
        private bool busy;
        private bool resumeRequired;
        private bool lifecycleSuspended;
        private bool applicationPaused;
        private bool focusLost;
        private double pendingMilliseconds;
        private double autosaveSeconds;
        public string SnapshotJson { get; private set; } = "{\"ok\":true}";
        public event Action<string> Changed;

        public static MobileGameSession Attach(GameObject host, string sampleJson, string roadJson, string directory)
        {
            var session = host.GetComponent<MobileGameSession>() ?? host.AddComponent<MobileGameSession>();
            session.Initialize(sampleJson, roadJson, directory);
            return session;
        }

        public void Initialize(string sampleJson, string roadJson, string directory)
        {
            if (initialized) throw new InvalidOperationException("모바일 게임 세션은 한 번만 연결합니다.");
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("저장 폴더를 확인해 주세요.", nameof(directory));
            storageDirectory = Path.GetFullPath(directory);
            Directory.CreateDirectory(storageDirectory);
            core = new SagajeongSimulationCore(sampleJson, roadJson);
            sessionId = Guid.NewGuid().ToString("N");
            experiences = new MobileGameExperienceLog(storageDirectory, Hash(sampleJson + "\n" + roadJson));
            initialized = true;
            lifecycleSuspended = applicationPaused || focusLost;
            TryLoadConfirmedSave();
            if (experiences.CorruptLineCount > 0 && string.IsNullOrWhiteSpace(notice))
                notice = "일부 이전 경험 기록을 읽지 못했습니다. 원문을 보존하고 게임 회차와 분리했습니다.";
            Publish(lastReply);
        }

        public void SetFileExchange(IMobileGameFileExchange port) => files = port;

        public async Task<string> RequestJsonAsync(string json)
        {
            if (!initialized) return Failure("게임 자료를 먼저 연결해 주세요.");
            if (busy) return Failure("앞선 입력을 처리하고 있습니다.");
            busy = true;
            try
            {
                var request = JObject.Parse(json);
                var action = (string)request["action"];
                if (action == "pause" || action == "clock-pause")
                {
                    Pause(false);
                    SaveConfirmed();
                    return Publish(lastReply);
                }
                if (action == "set-speed" || action == "clock-speed")
                {
                    var selected = (int?)request["speed"] ?? -1;
                    if (selected != 0 && selected != 1 && selected != 2 && selected != 4)
                        return Failure("일시정지·기본·2배·4배 중 선택해 주세요.");
                    if (!hasGame) return Failure("회차를 먼저 시작해 주세요.");
                    if (lifecycleSuspended) return Failure("화면으로 돌아온 뒤 운행을 재개해 주세요.");
                    speed = selected;
                    if (speed > 0) resumeSpeed = speed;
                    resumeRequired = false;
                    pendingMilliseconds = 0;
                    notice = selected == 0 ? "가상 시간을 멈췄습니다." : null;
                    if (speed == 0) SaveConfirmed();
                    return Publish(lastReply);
                }
                if (action == "resume")
                {
                    if (!hasGame) return Failure("이어갈 회차가 없습니다. 새 게임을 시작해 주세요.");
                    if (lifecycleSuspended) return Failure("화면으로 돌아온 뒤 운행을 재개해 주세요.");
                    speed = resumeSpeed;
                    resumeRequired = false;
                    pendingMilliseconds = 0;
                    notice = "저장한 가상 시각에서 이어갑니다.";
                    return Publish(lastReply);
                }
                if (action == "start" || action == "campaign-start" || action == "new-game")
                {
                    request["action"] = action == "new-game" ? "start" : action;
                    request["manualPickup"] = true;
                    request["elapsedMs"] = 0;
                    request["sessionId"] = Guid.NewGuid().ToString("N");
                    var response = Execute(request);
                    if ((bool?)response["ok"] == true)
                    {
                        hasGame = true;
                        elapsedMs = 0;
                        speed = resumeSpeed = 1;
                        resumeRequired = false;
                        pendingMilliseconds = 0;
                        notice = null;
                        SaveConfirmed();
                    }
                    return Publish(response);
                }
                if (action == "export") return await ExportAsync();
                if (action == "import") return await ImportAsync((string)request["importJson"] ?? (string)request["referenceJson"]);
                if (action == "ai-trial") return RuleTrial(request);
                if (action == "tick") return Failure("가상 시계는 현재 모바일 세션에서만 진행합니다.");
                if (!hasGame && action != "restore") return Failure("회차를 먼저 시작해 주세요.");
                if (action == "restore" && !IsMobileManualSave((string)request["saveJson"]))
                    return Failure("직접 픽업 확인을 사용하는 모바일 회차의 저장 자료만 복원할 수 있습니다.");
                if (action != "restore") request["elapsedMs"] = elapsedMs;
                if (action == "assign" || action == "pickup-confirm")
                    if (string.IsNullOrWhiteSpace((string)request["commandId"])) request["commandId"] = Guid.NewGuid().ToString("N");
                var result = Execute(request);
                if (action == "restore" && (bool?)result["ok"] == true)
                {
                    hasGame = true;
                    Pause(true);
                    notice = "저장 자료를 복원했습니다. 이어하기를 눌러 주세요.";
                }
                if (action == "assign" || action == "pickup-confirm") Record(action, request, result);
                if ((bool?)result["ok"] == true && (action == "assign" || action == "pickup-confirm" || action == "restore" || action == "campaign-policy"))
                    SaveConfirmed();
                return Publish(result);
            }
            catch (OperationCanceledException)
            {
                notice = null;
                return Failure("파일 저장을 취소했습니다. 현재 회차와 기존 내보내기 파일은 유지합니다.");
            }
            catch (Exception exception) when (exception is JsonException || exception is ArgumentException ||
                exception is InvalidOperationException || exception is IOException || exception is UnauthorizedAccessException)
            {
                return Failure("입력을 처리하지 못했습니다. 현재 회차를 유지합니다. " + exception.Message);
            }
            finally { busy = false; }
        }

        private void Update()
        {
            if (!initialized || !hasGame || busy || speed == 0 || lifecycleSuspended) return;
            var delta = Math.Min(Time.unscaledDeltaTime, 0.25f);
            autosaveSeconds += delta;
            pendingMilliseconds += delta * 12000.0 * speed;
            if (pendingMilliseconds < 1200) return;
            var increment = (long)pendingMilliseconds;
            pendingMilliseconds -= increment;
            var response = Execute(new JObject { ["action"] = "tick", ["elapsedMs"] = elapsedMs + increment });
            if ((bool?)response["ok"] != true) Pause(true);
            if ((bool?)response["state"]?["settled"] == true)
            {
                Pause(false);
                SaveConfirmed();
            }
            if (autosaveSeconds >= 5)
            {
                autosaveSeconds = 0;
                SaveConfirmed();
            }
            Publish(response);
        }

        private void OnApplicationPause(bool paused)
        {
            applicationPaused = paused;
            Lifecycle(applicationPaused || focusLost);
        }
        private void OnApplicationFocus(bool focused)
        {
            focusLost = !focused;
            Lifecycle(applicationPaused || focusLost);
        }
        private void OnApplicationQuit()
        {
            if (!initialized || !hasGame) return;
            Pause(true);
            SaveConfirmed();
        }

        private void Lifecycle(bool suspended)
        {
            if (!initialized) return;
            if (lifecycleSuspended == suspended) return;
            lifecycleSuspended = suspended;
            if (suspended)
            {
                Pause(true);
                SaveConfirmed();
                notice = "화면을 떠나 가상 시간을 멈추고 저장했습니다.";
            }
            else
            {
                pendingMilliseconds = 0;
                if (hasGame) notice = "같은 가상 시각으로 돌아왔습니다. 이어하기를 눌러 주세요.";
            }
            Publish(lastReply);
        }

        private void Pause(bool requireResume)
        {
            if (speed > 0) resumeSpeed = speed;
            speed = 0;
            pendingMilliseconds = 0;
            resumeRequired = requireResume;
        }

        private JObject Execute(JObject request)
        {
            var response = JObject.Parse(core.Execute(request.ToString(Formatting.None)));
            if (response["state"] is JObject state)
            {
                elapsedMs = (long?)state["elapsedMs"] ?? elapsedMs;
                if (!string.IsNullOrWhiteSpace((string)state["sessionId"])) sessionId = (string)state["sessionId"];
                if ((response["roads"] == null || response["roads"].Type == JTokenType.Null) &&
                    lastReply["roads"] != null && lastReply["roads"].Type != JTokenType.Null)
                    response["roads"] = lastReply["roads"].DeepClone();
                lastReply = response;
            }
            return response;
        }

        private string Publish(JObject response)
        {
            if (initialized && hasGame && (bool?)response["state"]?["settled"] == true &&
                response["state"]?["settlement"] is JObject)
            {
                // 확정 결과는 같은 회차에서 한 번만 기록합니다. 복원 시에도 기존 eventId를 재사용합니다.
                var outcome = new JObject
                {
                    ["ok"] = true,
                    ["state"] = response["state"].DeepClone(),
                    ["message"] = "가상 회차의 수령·정산 결과를 확인했습니다."
                };
                Record("round-outcome", new JObject
                {
                    ["commandId"] = "round-outcome",
                    ["elapsedMs"] = response["state"]["elapsedMs"]?.DeepClone() ?? elapsedMs
                }, outcome);
            }
            var displayed = (JObject)response.DeepClone();
            displayed["clock"] = new JObject { ["speed"] = speed, ["paused"] = speed == 0, ["resumeRequired"] = resumeRequired };
            if (!string.IsNullOrWhiteSpace(notice)) displayed["notice"] = notice;
            SnapshotJson = displayed.ToString(Formatting.None);
            Changed?.Invoke(SnapshotJson);
            return SnapshotJson;
        }

        private string Failure(string message) => Publish(new JObject
        {
            ["ok"] = false, ["message"] = message,
            ["state"] = lastReply["state"]?.DeepClone(), ["roads"] = lastReply["roads"]?.DeepClone()
        });

        private string SavePath => Path.Combine(storageDirectory, "confirmed-session.v1.json");
        private void SaveConfirmed()
        {
            if (!hasGame) return;
            try
            {
                var saved = JObject.Parse(core.Execute("{\"action\":\"save\"}"));
                var saveJson = (string)saved["saveJson"];
                if ((bool?)saved["ok"] != true || string.IsNullOrWhiteSpace(saveJson))
                {
                    notice = "저장 결과를 확인하지 못했습니다. 이전 정상 저장을 유지합니다.";
                    return;
                }
                var envelope = new JObject
                {
                    ["schemaVersion"] = SaveSchema, ["sessionId"] = sessionId,
                    ["savedAtUtc"] = DateTime.UtcNow.ToString("o"), ["resumeSpeed"] = resumeSpeed,
                    ["saveJson"] = saveJson
                };
                AtomicWrite(SavePath, envelope.ToString(Formatting.None));
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is JsonException)
            {
                notice = "현재 회차 저장에 실패했습니다. 이전 정상 저장은 보존합니다.";
            }
        }

        private void TryLoadConfirmedSave()
        {
            var paths = new[] { SavePath, SavePath + ".previous" };
            var found = false;
            foreach (var path in paths)
            {
                if (!File.Exists(path)) continue;
                found = true;
                try
                {
                    var value = JObject.Parse(File.ReadAllText(path, Encoding.UTF8));
                    if ((string)value["schemaVersion"] != SaveSchema || !IsMobileManualSave((string)value["saveJson"])) continue;
                    var result = Execute(new JObject { ["action"] = "restore", ["saveJson"] = (string)value["saveJson"] });
                    if ((bool?)result["ok"] != true) continue;
                    resumeSpeed = (int?)value["resumeSpeed"] ?? 1;
                    if (resumeSpeed != 1 && resumeSpeed != 2 && resumeSpeed != 4) resumeSpeed = 1;
                    hasGame = true;
                    Pause(true);
                    notice = path == SavePath ? "저장한 회차를 복원했습니다. 이어하기를 눌러 주세요." :
                        "최근 저장을 읽을 수 없어 이전 정상 회차로 복원했습니다.";
                    return;
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException ||
                    exception is JsonException || exception is ArgumentException || exception is InvalidOperationException) { }
            }
            if (found) notice = "저장 자료를 복원하지 못했습니다. 원본은 보존했습니다. 새 게임을 시작할 수 있습니다.";
        }

        private static void AtomicWrite(string path, string text)
        {
            var temporary = path + ".tmp";
            var previous = path + ".previous";
            var bytes = new UTF8Encoding(false).GetBytes(text);
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            if (!File.Exists(path)) { File.Move(temporary, path); return; }
            try { File.Replace(temporary, path, previous); }
            catch (PlatformNotSupportedException) { ReplaceWithRollback(temporary, path, previous); }
            catch (NotSupportedException) { ReplaceWithRollback(temporary, path, previous); }
        }

        private static bool IsMobileManualSave(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return false;
            try
            {
                var saved = JObject.Parse(json);
                var commands = saved["commands"] as JArray;
                if (commands == null || commands.Count == 0 || commands[0].Type != JTokenType.String) return false;
                var first = JObject.Parse((string)commands[0]);
                return ((string)first["action"] == "start" || (string)first["action"] == "campaign-start") &&
                    (bool?)first["manualPickup"] == true;
            }
            catch (JsonException) { return false; }
        }

        private static string Hash(string text)
        {
            using (var algorithm = SHA256.Create())
                return BitConverter.ToString(algorithm.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
        }

        private static void ReplaceWithRollback(string temporary, string path, string previous)
        {
            if (File.Exists(previous)) File.Delete(previous);
            File.Move(path, previous);
            try { File.Move(temporary, path); }
            catch
            {
                if (!File.Exists(path) && File.Exists(previous)) File.Copy(previous, path);
                throw;
            }
        }

        private async Task<string> ExportAsync()
        {
            var result = Execute(new JObject { ["action"] = "export" });
            var json = (string)result["exportJson"];
            if ((bool?)result["ok"] != true || string.IsNullOrWhiteSpace(json)) return Publish(result);
            if (!MobileGameExchangeValidator.TryRead(json, out var value, out var error)) return Failure(error);
            var safe = value.ToString(Formatting.Indented);
            var name = "hongdal-game-preparation-" + sessionId + ".json";
            var directory = Path.Combine(storageDirectory, "exports");
            Directory.CreateDirectory(directory);
            AtomicWrite(Path.Combine(directory, name), safe);
            if (files != null) await files.ShareTextAsync(name, safe, "application/json");
            notice = files == null ? "내보내기 파일을 기기에 저장했습니다. 파일 저장 연결은 아직 준비되지 않았습니다." : "선택한 위치에 가상 운영 자료를 저장했습니다.";
            result["exportFileName"] = name;
            return Publish(result);
        }

        private async Task<string> ImportAsync(string text)
        {
            Pause(true);
            SaveConfirmed();
            if (string.IsNullOrWhiteSpace(text))
            {
                if (files == null) return Failure("파일 선택 연결이 준비되지 않았습니다.");
                text = await files.ReadTextAsync();
                if (string.IsNullOrWhiteSpace(text)) return Failure("파일 선택을 취소했습니다. 현재 회차는 유지합니다.");
            }
            if (!MobileGameExchangeValidator.TryRead(text, out var reference, out var error)) return Failure(error);
            var bytes = Encoding.UTF8.GetBytes(reference.ToString(Formatting.None));
            string hash;
            using (var algorithm = SHA256.Create()) hash = BitConverter.ToString(algorithm.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
            var directory = Path.Combine(storageDirectory, "references");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, hash + ".json");
            if (!File.Exists(path)) AtomicWrite(path, reference.ToString(Formatting.Indented));
            notice = "자료를 비공개 참고 사본으로 가져왔습니다. 현재 회차·현실 업무·모델은 바꾸지 않았습니다.";
            var result = (JObject)lastReply.DeepClone();
            result["importedReferenceHash"] = hash;
            return Publish(result);
        }

        private string RuleTrial(JObject request)
        {
            if (!hasGame) return Failure("회차를 먼저 시작해 주세요.");
            var orderId = (string)request["orderId"];
            var inquiry = new JObject { ["action"] = "candidates", ["orderId"] = orderId, ["elapsedMs"] = elapsedMs };
            var preview = Execute(inquiry);
            var candidates = preview["candidates"] as JArray ?? new JArray();
            var recommended = (string)preview["recommendedDriverId"];
            var eligible = candidates.OfType<JObject>().Where(candidate => (bool?)candidate["eligible"] == true).ToArray();
            var selected = eligible.FirstOrDefault(candidate => (string)candidate["driverId"] == recommended)
                ?? eligible.OrderBy(candidate => (double?)candidate["pickupKilometers"] ?? double.MaxValue).FirstOrDefault();
            if ((bool?)preview["ok"] != true || selected == null)
            {
                var rejected = new JObject { ["ok"] = false, ["message"] = "현재 배차 가능한 후보가 없습니다. 규칙과 현재 상태를 확인해 주세요." };
                Record("ai-trial", inquiry, rejected, candidates);
                return Failure((string)rejected["message"]);
            }
            var command = new JObject
            {
                ["action"] = "assign", ["orderId"] = orderId, ["driverId"] = selected["driverId"].DeepClone(),
                ["commandId"] = "rule-trial-" + Guid.NewGuid().ToString("N"),
                ["revision"] = preview["revision"]?.DeepClone() ?? 0, ["elapsedMs"] = elapsedMs
            };
            var result = Execute(command);
            Record("ai-trial", command, result, candidates);
            if ((bool?)result["ok"] == true) SaveConfirmed();
            notice = "모델 연결 없이 현재 적격 후보의 규칙 추천을 가상 배차에 사용했습니다. 가중치 학습·사람 승인은 없습니다.";
            return Publish(result);
        }

        private void Record(string action, JObject request, JObject result, JArray candidates = null)
        {
            try { experiences.Record(sessionId, action, request, result, candidates); }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            { notice = "게임 진행은 유지했지만 경험 기록 저장을 확인하지 못했습니다."; }
        }
    }
}
