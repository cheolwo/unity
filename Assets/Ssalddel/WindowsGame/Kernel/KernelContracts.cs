using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Ssalddel.WindowsGame.Kernel
{
    // 2026-11-12 0단계 뼈대 S3: 게임 커널과 업무 모듈 사이의 계약.
    // 이 어셈블리는 Unity 엔진을 쓰지 않는다(noEngineReferences). 화면은 상태 사본만 읽고 명령만 보낸다.
    // 게임 상태는 언제나 Simulation이다. 실제 주문·배차·결제와 이어지지 않는다.

    /// <summary>사업 라인 식별자. 운영 플랫폼의 업무 OS 카탈로그에 있는 것은 그 ID를 그대로 쓴다.</summary>
    public static class BusinessIds
    {
        public const string FoodDelivery = "FoodDeliveryOS";
        public const string CargoTransport = "DomesticCargoTransportOS";
        public const string LogisticsCenter = "WarehouseCommerceFulfillmentOS";
        // 아래 둘은 업무 OS 카탈로그에 아직 없다. 큰 Simulation의 공급망 모듈과 r71의 점포 업무를 가리키는 임시 이름이다.
        public const string FranchiseSupply = "FranchiseSupplyNetwork";
        public const string StoreOperations = "FranchiseStoreOperations";
        // 업무 OS가 아니다. 어느 동까지 운영하는지를 맡는 게임 층이다. 커널에 함께 꽂혀 시각과 저장을 같이 쓴다.
        // 판의 단위는 팩이 정한다(처음에는 법정동, 2026-11-13부터 행정동 팩).
        public const string Territory = "TerritoryExpansion";
        // 업무 OS가 아니다. 깃발을 꽂은 행정동마다 생기는 가상 주문과 그것을 받아내는 기사·제휴를 맡는 게임 층이다.
        public const string DongDemand = "DongDemandOperations";
    }

    /// <summary>영역 모듈이 묻는다: 이 동은 지금 깊어져도(밝아져도) 되는가. 주문을 받아내지 못하는 동은 깊어지지 않는다.</summary>
    public interface ITerritoryGate
    {
        bool Deepens(string dongCode);
    }

    /// <summary>다른 게임 층이 묻는다: 이 동에 깃발이 꽂혔는가, 어느 단계인가.</summary>
    public interface ITerritoryView
    {
        int LevelOf(string dongCode);
    }

    /// <summary>업무 모듈. 음식 배달도 이 가운데 하나다.</summary>
    public interface IBusinessModule
    {
        string OsId { get; }
        /// <summary>새 회차. 이전 상태를 버린다.</summary>
        void Start(ModuleContext context);
        /// <summary>가상 시각 진행. elapsedMs는 회차 시작부터 흐른 가상 시간이다.</summary>
        void Tick(long elapsedMs);
        /// <summary>후보와 추천만 돌려준다. 상태를 바꾸지 않는다.</summary>
        GamePreview Preview(GameCommand command);
        /// <summary>검증한 뒤 상태를 바꾼다.</summary>
        GameResult Confirm(GameCommand command);
        /// <summary>화면용 상태 사본.</summary>
        object Snapshot();
        /// <summary>다른 사업이 넘긴 인계를 받는다. 같은 인계를 다시 받아도 한 번만 반영한다.</summary>
        HandoffReceipt Accept(Handoff handoff);
        /// <summary>저장할 상태. null이면 커널 저장에 넣지 않는다(스스로 저장하는 모듈).</summary>
        string Save();
        void Restore(string saved);
    }

    /// <summary>가상 시각의 기준이 되는 모듈. 이 모듈이 시각을 받지 않으면 다른 모듈도 진행하지 않는다.</summary>
    public interface IClockAuthority
    {
        bool LastTickAccepted { get; }
    }

    /// <summary>명령 번호의 중복 처리를 스스로 하는 모듈. 커널이 결과를 따로 기억하지 않는다.</summary>
    public interface ISelfIdempotent
    {
    }

    public sealed class ModuleContext
    {
        public ModuleContext(GameKernel kernel, long seed)
        {
            Kernel = kernel;
            Seed = seed;
        }

        public GameKernel Kernel { get; }
        public long Seed { get; }
    }

    public sealed class GameCommand
    {
        public string OsId = string.Empty;
        public string Action = string.Empty;
        /// <summary>같은 번호의 명령은 한 번만 반영한다. 비어 있으면 중복 검사를 하지 않는다.</summary>
        public string CommandId = string.Empty;
        public long ElapsedMs;
        /// <summary>모듈이 그대로 읽는 원문. 음식 배달은 Core 요청 JSON을 여기에 담는다.</summary>
        public string PayloadJson = string.Empty;
        public Dictionary<string, string> Values = new Dictionary<string, string>();

        public string Value(string key)
        {
            string value;
            return Values.TryGetValue(key, out value) ? value ?? string.Empty : string.Empty;
        }

        public decimal Number(string key)
        {
            decimal number;
            return decimal.TryParse(Value(key), NumberStyles.Number, CultureInfo.InvariantCulture, out number) ? number : 0m;
        }

        public GameCommand With(string key, string value)
        {
            Values[key] = value ?? string.Empty;
            return this;
        }

        public GameCommand With(string key, decimal value)
        {
            Values[key] = value.ToString(CultureInfo.InvariantCulture);
            return this;
        }

        public string Signature()
        {
            return OsId + "|" + Action + "|" + PayloadJson + "|" + string.Join("&",
                Values.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Key + "=" + pair.Value));
        }
    }

    public sealed class GameCandidate
    {
        public string Id = string.Empty;
        public string Title = string.Empty;
        public string Reason = string.Empty;
        public bool Recommended;
        public bool Eligible = true;
    }

    public sealed class GamePreview
    {
        public bool CanConfirm;
        public string Message = string.Empty;
        public string BlockCode = string.Empty;
        public List<GameCandidate> Candidates = new List<GameCandidate>();
        public string RawJson = string.Empty;

        public static GamePreview Blocked(string message, string code)
        {
            return new GamePreview { CanConfirm = false, Message = message, BlockCode = code };
        }
    }

    public sealed class GameResult
    {
        public bool Ok;
        public string Message = string.Empty;
        public string Code = string.Empty;
        /// <summary>같은 명령 번호를 다시 받아 예전 결과를 돌려준 경우.</summary>
        public bool Replayed;
        public string RawJson = string.Empty;

        public static GameResult Done(string message)
        {
            return new GameResult { Ok = true, Message = message };
        }

        public static GameResult Fail(string message, string code)
        {
            return new GameResult { Ok = false, Message = message, Code = code };
        }

        public GameResult AsReplay()
        {
            return new GameResult { Ok = Ok, Message = Message, Code = Code, RawJson = RawJson, Replayed = true };
        }
    }

    public static class HandoffStates
    {
        public const string Posted = "Posted";
        public const string Accepted = "Accepted";
        /// <summary>받는 쪽이 아직 처리할 수 없어 기록만 해 둔 상태.</summary>
        public const string Held = "Held";
        public const string Refused = "Refused";
    }

    /// <summary>
    /// 사업 사이의 인계. 모듈끼리는 서로 직접 부르지 않고 커널에 인계를 낸다.
    /// r71의 인계 기록과 같은 항목이다: 보내는 쪽, 받는 쪽, 대상, 확정 수량·단위, 시각, 근거.
    /// </summary>
    public sealed class Handoff
    {
        /// <summary>같은 인계는 같은 ID다. 두 번 내도 한 번만 전달된다.</summary>
        public string Id = string.Empty;
        public string ChainId = string.Empty;
        public string Kind = string.Empty;
        public string FromOsId = string.Empty;
        public string ToOsId = string.Empty;
        public string FromPlaceId = string.Empty;
        public string ToPlaceId = string.Empty;
        public string SubjectId = string.Empty;
        public string ItemId = string.Empty;
        public string ItemName = string.Empty;
        public decimal Quantity;
        public string Unit = string.Empty;
        public long AtMs;
        public string Evidence = string.Empty;
        public string State = HandoffStates.Posted;
        public string StateNote = string.Empty;
        public Dictionary<string, string> Values = new Dictionary<string, string>();

        public string Value(string key)
        {
            string value;
            return Values.TryGetValue(key, out value) ? value ?? string.Empty : string.Empty;
        }

        public decimal Number(string key)
        {
            decimal number;
            return decimal.TryParse(Value(key), NumberStyles.Number, CultureInfo.InvariantCulture, out number) ? number : 0m;
        }
    }

    public sealed class HandoffReceipt
    {
        public string State = HandoffStates.Accepted;
        public string Note = string.Empty;

        public static HandoffReceipt Accepted(string note)
        {
            return new HandoffReceipt { State = HandoffStates.Accepted, Note = note ?? string.Empty };
        }

        public static HandoffReceipt Held(string note)
        {
            return new HandoffReceipt { State = HandoffStates.Held, Note = note ?? string.Empty };
        }

        public static HandoffReceipt Refused(string note)
        {
            return new HandoffReceipt { State = HandoffStates.Refused, Note = note ?? string.Empty };
        }
    }

    public static class LedgerKinds
    {
        public const string Stock = "stock";
        public const string Money = "money";
        public const string Time = "time";
    }

    public static class StockBuckets
    {
        public const string Available = "available";
        public const string Reserved = "reserved";
        public const string Shipped = "shipped";
        public const string Unloaded = "unloaded";
        public const string Quarantine = "quarantine";
        public const string Rejected = "rejected";
        public const string Used = "used";
    }

    /// <summary>공통 원장의 한 줄. 재고·돈·시간의 이동을 적는다. 잔량은 이 줄들의 합이다.</summary>
    public sealed class LedgerEntry
    {
        public string Kind = LedgerKinds.Stock;
        public string OsId = string.Empty;
        public string PlaceId = string.Empty;
        public string ItemId = string.Empty;
        public string Bucket = string.Empty;
        public decimal Amount;
        public string Unit = string.Empty;
        public long AtMs;
        /// <summary>
        /// 이 줄을 적은 현실 시각(UTC 밀리초). 0이면 모른다(이 칸이 생기기 전의 줄, 또는 현실 시각 없이 돌린 검사).
        /// 가상 시각 AtMs와 별개이며 규칙 계산에는 쓰지 않는다. 일일 결산이 한국표준시 날짜로 읽는다 (r174).
        /// </summary>
        public long RealAtUtcMs;
        public string Reason = string.Empty;
        public string RefId = string.Empty;
    }
}
