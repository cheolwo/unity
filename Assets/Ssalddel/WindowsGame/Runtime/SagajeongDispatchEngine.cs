using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Ssalddel.WindowsGame.Kernel;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 2026-11-12 Windows 실행판: 외부 엔진 exe 대신 공통 Core(SagajeongSimulationCore)를 같은 프로세스에서 실행한다.
    // 화면 코드가 기대하는 Send(DispatchRequest) → DispatchReply 계약은 그대로 둔다.
    // 확정 저장은 Core의 save/restore를 쓰며, 시작할 때 정상 저장이 있으면 그 회차를 불러온다.
    // 0단계 뼈대(같은 날): Core를 직접 부르지 않고 게임 커널의 업무 모듈 1호(FoodDeliveryModule)를 거친다.
    // 요청과 응답의 글자는 예전과 같다. 다른 사업의 상태는 저장 봉투의 kernelJson에 따로 담는다.
    internal sealed class SagajeongDispatchEngine : IDisposable
    {
        private const string SaveSchema = "hongdal-windows-session.v1";
        private const string ResourceFolder = "HongdalWindows/";
        private const long KernelSeed = 20261112;
        private readonly HongdalKernelHost host;
        private readonly GameKernel kernel;
        private readonly FoodDeliveryModule food;
        private readonly string savePath;
        private bool firstStart = true;
        private bool hasGame;
        private string currentStartAction = "campaign-start";
        private float lastSaveTime;
        private bool disposed;
        // 검증 촬영 인자(--sagajeong-*)로 켜면 저장을 읽지도 쓰지도 않는다. 사용자 저장을 덮지 않기 위해서다.
        private readonly bool persistent;

        /// <summary>시작할 때 저장한 회차를 불러왔으면 안내 문구를 한 번 담는다.</summary>
        internal string RestoredNotice { get; set; }

        /// <summary>카1: 화면의 운영 회차를 저장 글자로 주는 곳. 저장할 때 함께 담는다(업무 상태가 아니라 화면의 진행이다).</summary>
        internal Func<string> RoundText { get; set; }
        /// <summary>카1: 불러온 저장에 담겨 있던 운영 회차 글자. 화면이 한 번 읽고 비운다.</summary>
        internal string RestoredRound { get; set; }

        internal SagajeongDispatchEngine(string playerDataPath)
        {
            var sample = Resources.Load<TextAsset>(ResourceFolder + "delivery-sample");
            var roads = Resources.Load<TextAsset>(ResourceFolder + "road-graph");
            if (sample == null || roads == null)
                throw new FileNotFoundException("내장 배달 표본이나 도로 자료를 찾을 수 없습니다.");
            host = HongdalKernelHost.Create(sample.text, roads.text);
            kernel = host.Kernel;
            food = host.Food;
            var directory = Path.Combine(Application.persistentDataPath, "windows-game");
            Directory.CreateDirectory(directory);
            savePath = Path.Combine(directory, "confirmed-session.v1.json");
            // 검증 촬영(--sagajeong-…)과 방향 촬영(--hongdal-view-proof=)은 저장한 회차를 읽지도 쓰지도 않는다.
            persistent = Array.TrueForAll(Environment.GetCommandLineArgs(),
                value => !value.StartsWith("--sagajeong-", StringComparison.Ordinal) &&
                    !value.StartsWith(SagajeongDispatchView.ViewProofFlag, StringComparison.Ordinal));
        }

        /// <summary>화면이 상태 사본을 읽는 곳. 화면은 커널에 직접 명령하지 않고 Business()로 보낸다.</summary>
        internal GameKernel Kernel => kernel;
        internal FirstChainPack Pack => host.Pack;

        /// <summary>음식 배달이 아닌 사업의 확정 명령. 성공하면 바로 저장한다.</summary>
        internal GameResult Business(GameCommand command)
        {
            if (disposed) return GameResult.Fail("게임을 닫는 중입니다.", "Disposed");
            StampRealTime();
            var result = kernel.Confirm(command);
            if (result.Ok && !result.Replayed) Save();
            return result;
        }

        internal GamePreview BusinessPreview(GameCommand command) => kernel.Preview(command);

        // 떴다 사라지는 알림: 정산을 마친 주문들의 가상 차액 합. Core의 내보내기 요약을 그대로 읽는다.
        // 내보내기는 회차를 바꾸지 않는 조회다(FoodDeliveryModule.Query, Core의 기록에 남지 않음). 읽지 못하면 null.
        internal ExportSummary CompletedSummary()
        {
            try
            {
                var reply = JsonUtility.FromJson<ExportReply>(food.Query("{\"action\":\"export\"}"));
                if (reply == null || !reply.ok || string.IsNullOrEmpty(reply.exportJson)) return null;
                return JsonUtility.FromJson<ExportBody>(reply.exportJson)?.summary;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        [Serializable] private sealed class ExportReply { public bool ok; public string exportJson; }
        [Serializable] private sealed class ExportBody { public ExportSummary summary; }
        [Serializable] internal sealed class ExportSummary
        {
            public int totalOrders;
            public int completedOrders;
            public int virtualRevenue;
            public int virtualDriverPay;
            public int virtualBaseBalance;
        }

        internal Task<DispatchReply> Send(DispatchRequest request)
        {
            try
            {
                if (disposed) throw new ObjectDisposedException(nameof(SagajeongDispatchEngine));
                return Task.FromResult(Execute(request));
            }
            catch (Exception exception)
            {
                return Task.FromException<DispatchReply>(exception);
            }
        }

        // r174: 원장 줄에 함께 적을 현실 시각을 커널에 알린다. 가상 시각과 규칙에는 쓰이지 않는다.
        private void StampRealTime() => kernel.RealNowUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + proofRealTimeOffsetMs;

        // 저장하지 않는 세션(촬영)에서만: 지난 날짜에 적힌 기록을 흉내 내려고 원장에 함께 적는 현실 시각을 옮긴다.
        // 저장하는 세션에서는 받지 않는다. 가상 시각과 규칙에는 쓰이지 않는다.
        private long proofRealTimeOffsetMs;
        internal void ShiftProofRealTime(long offsetMs)
        {
            if (!persistent) proofRealTimeOffsetMs = offsetMs;
        }

        private DispatchReply Execute(DispatchRequest request)
        {
            StampRealTime();
            var starting = request.action == "start" || request.action == "campaign-start";
            if (starting && firstStart && persistent)
            {
                firstStart = false;
                var restored = TryRestore(request.action);
                if (restored != null) return restored;
            }
            else if (starting) firstStart = false;

            DispatchReply reply;
            if (request.action == "tick")
            {
                // 시각은 커널이 모든 사업에 함께 흘린다. 음식 배달이 시각을 받지 않으면 다른 사업도 멈춘다.
                kernel.Tick(request.elapsedMs);
                reply = Parse(food.LastReplyJson);
            }
            else
            {
                if (starting) kernel.Start(KernelSeed);       // 새 회차: 다른 사업도 처음부터
                var command = new GameCommand
                {
                    OsId = BusinessIds.FoodDelivery,
                    Action = request.action,
                    ElapsedMs = request.elapsedMs,
                    PayloadJson = JsonUtility.ToJson(request),
                };
                reply = Parse(request.action == "candidates" ? kernel.Preview(command).RawJson : kernel.Confirm(command).RawJson);
            }
            if (!reply.ok) return reply;
            if (starting) { hasGame = true; currentStartAction = request.action; }
            var confirmed = starting || request.action == "assign" || request.action == "campaign-policy" ||
                request.action == "pickup-confirm";
            if (confirmed || reply.state?.settled == true || Time.realtimeSinceStartup - lastSaveTime >= 5f)
                Save();
            return reply;
        }

        private DispatchReply TryRestore(string startAction)
        {
            foreach (var path in new[] { savePath, savePath + ".previous" })
            {
                if (!File.Exists(path)) continue;
                try
                {
                    var envelope = JsonUtility.FromJson<SaveEnvelope>(File.ReadAllText(path, Encoding.UTF8));
                    if (envelope == null || envelope.schemaVersion != SaveSchema || string.IsNullOrEmpty(envelope.saveJson) ||
                        envelope.startAction != startAction) continue;
                    kernel.Start(KernelSeed);
                    var reply = Parse(kernel.Confirm(new GameCommand
                    {
                        OsId = BusinessIds.FoodDelivery,
                        Action = "restore",
                        PayloadJson = JsonUtility.ToJson(new RestoreRequest { saveJson = envelope.saveJson }),
                    }).RawJson);
                    if (!reply.ok || reply.state == null) continue;
                    // 0단계 이전의 저장에는 kernelJson이 없다. 그때는 다른 사업만 처음부터 시작한다.
                    if (!string.IsNullOrEmpty(envelope.kernelJson) && !kernel.Restore(envelope.kernelJson))
                        Debug.LogWarning("Hongdal windows save: 다른 사업의 저장을 읽지 못해 처음 상태로 둡니다.");
                    hasGame = true;
                    currentStartAction = startAction;
                    RestoredRound = envelope.roundJson;
                    RestoredNotice = path == savePath
                        ? "저장한 회차를 불러왔습니다."
                        : "최근 저장 대신 직전 회차를 불러왔습니다.";
                    return reply;
                }
                catch (Exception exception) when (exception is IOException || exception is ArgumentException ||
                    exception is UnauthorizedAccessException)
                {
                    Debug.LogWarning("Hongdal windows save: " + exception.Message);
                }
            }
            return null;
        }

        private void Save()
        {
            if (!hasGame || !persistent) return;
            lastSaveTime = Time.realtimeSinceStartup;
            try
            {
                var saved = JsonUtility.FromJson<SaveReply>(food.Query("{\"action\":\"save\"}"));
                if (saved == null || !saved.ok || string.IsNullOrEmpty(saved.saveJson)) return;
                var envelope = new SaveEnvelope
                {
                    schemaVersion = SaveSchema,
                    savedAtUtc = DateTime.UtcNow.ToString("o"),
                    startAction = currentStartAction,
                    saveJson = saved.saveJson,
                    kernelJson = kernel.Save(),
                    roundJson = RoundText?.Invoke(),
                };
                AtomicWrite(savePath, JsonUtility.ToJson(envelope));
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogWarning("Hongdal windows save failed: " + exception.Message);
            }
        }

        // 임시 파일에 쓰고 동기화한 뒤 교체한다. 직전 정상본은 .previous로 남긴다.
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
            catch (PlatformNotSupportedException)
            {
                if (File.Exists(previous)) File.Delete(previous);
                File.Move(path, previous);
                File.Move(temporary, path);
            }
        }

        private static DispatchReply Parse(string json)
        {
            var reply = JsonUtility.FromJson<DispatchReply>(json)
                ?? throw new IOException("시뮬레이션 응답을 읽을 수 없습니다.");
            // JsonUtility가 단일 회차의 null 캠페인을 기본값 객체로 복원할 수 있다.
            if (reply.state?.campaign?.day == 0) reply.state.campaign = null;
            return reply;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Save();
        }

        [Serializable] private sealed class RestoreRequest { public string action = "restore"; public string saveJson; }
        [Serializable] private sealed class SaveReply { public bool ok; public string saveJson; }
        [Serializable] private sealed class SaveEnvelope { public string schemaVersion; public string savedAtUtc; public string startAction; public string saveJson; public string kernelJson; public string roundJson; }
    }

    [Serializable]
    internal sealed class DispatchRequest
    {
        public string action;
        public long elapsedMs;
        public string orderId;
        public string driverId;
        public string commandId;
        public long revision;
        public string policy;
    }

    [Serializable]
    internal sealed class DispatchReply
    {
        public bool ok;
        public string message;
        public DispatchState state;
        public DispatchRoad[] roads;
    }

    [Serializable]
    internal sealed class DispatchState
    {
        public long elapsedMs;
        public string now;
        public DispatchOrder[] orders;
        public DispatchCourier[] couriers;
        public DispatchStore[] stores;
        public DispatchDestination[] destinations;
        public DispatchEvent[] events;
        public bool settled;
        public DispatchSettlement settlement;
        public int remainingSeconds;
        public DispatchCampaign campaign;
    }

    [Serializable]
    internal sealed class DispatchCampaign
    {
        public int day;
        public string phase;
        public string policy;
        public long nextBoundaryMs;
        public bool canFastForward;
        public DispatchDaySummary latestSummary;
    }

    [Serializable]
    internal sealed class DispatchDaySummary
    {
        public int day;
        public int completed;
        public int waiting;
        public int delivering;
        public int balance;
        public int totalOrders;
    }

    [Serializable]
    internal sealed class DispatchOrder
    {
        public string id;
        public string storeId;
        public string storeName;
        public string menu;
        public string stage;
        public string driverId;
        public long revision;
        public string recommendedDriverId;
        public bool assignedOnTime;
        public double deliveryMinutes;
    }

    [Serializable]
    internal sealed class DispatchCourier
    {
        public string id;
        public string label;
        public double latitude;
        public double longitude;
        public string phase;
        public DispatchPoint[] route;
    }

    [Serializable]
    internal sealed class DispatchStore
    {
        public string id;
        public string name;
        public double latitude;
        public double longitude;
    }

    [Serializable]
    internal sealed class DispatchDestination
    {
        public string id;
        public double latitude;
        public double longitude;
    }

    [Serializable]
    internal sealed class DispatchEvent
    {
        public string orderId;
        public string at;
        public string code;
        public string text;
    }

    [Serializable]
    internal sealed class DispatchSettlement
    {
        public bool cleared;
        public int onTimeCount;
        public int revenue;
        public int driverPay;
        public int baseBalance;
        public int bonus;
        public int balance;
    }

    [Serializable]
    internal sealed class DispatchPoint
    {
        public double latitude;
        public double longitude;
    }

    [Serializable]
    internal sealed class DispatchRoad
    {
        public DispatchPoint a;
        public DispatchPoint b;
    }
}
