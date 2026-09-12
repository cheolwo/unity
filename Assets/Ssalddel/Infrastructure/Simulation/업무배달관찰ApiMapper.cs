using System;
using System.Linq;
using Ssalddel.Unity.Runtime.World;
using Ssalddel.Contracts.Common.Metadata;

namespace Ssalddel.Unity.Infrastructure.Simulation
{
    [Serializable] public sealed class 업무배달관찰Dto
    {
        public string schemaVersion, runId, status, orderNo, orderStatus, dispatchStatus, message;
        public long revision;
        public double elapsedSeconds, durationSeconds;
        public 업무배달관찰ActorDto[] actors;
        public 업무배달관찰EventDto[] events;
        public 업무배달메뉴선택Dto menuSelection;
    }
    [Serializable] public sealed class 업무배달메뉴선택Dto
    {
        public string snapshotHash, title, reason, source, reference, priceBasis;
        public long recipeId, menuId;
        public double salePrice;
        public string[] candidates;
    }
    [Serializable] public sealed class 업무배달관찰ActorDto
    { public string id, name, role, state, nextAction, waitReason, lastAction; public float x, z; }
    [Serializable] public sealed class 업무배달관찰EventDto
    { public long sequence; public string actorId, action, result, occurredAtUtc; }

    [SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E2, "검증 전용 업무 API 계약 매핑", Boundary = "서버 DTO를 Simulation 상태로 위장하지 않는다")]
    public static class 업무배달관찰ApiMapper
    {
        public static 업무배달관찰Model Map(업무배달관찰Dto dto)
        {
            if (dto == null || dto.schemaVersion != "food-delivery-observer.r1") throw new FormatException("ObserverSchemaMismatch");
            if (!new[] { "Idle", "Running", "Completed", "Failed", "TimedOut", "Waiting" }.Contains(dto.status)
                || dto.revision < 0 || !Finite(dto.elapsedSeconds) || dto.elapsedSeconds < 0
                || dto.durationSeconds != 300 || dto.actors == null || dto.actors.Length > 4
                || dto.events == null || dto.events.Length > 500) throw new FormatException("ObserverContractInvalid");
            if (dto.status != "Idle" && dto.status != "Failed" && string.IsNullOrWhiteSpace(dto.runId)) throw new FormatException("ObserverRunIdMissing");
            var ids = new System.Collections.Generic.HashSet<string>();
            var actors = dto.actors.Select(a => {
                if (a == null || !ids.Add(a.id ?? "") || !ValidActor(a.id, a.role)
                    || !Finite(a.x) || !Finite(a.z) || Math.Abs(a.x) > 1000 || Math.Abs(a.z) > 1000)
                    throw new FormatException("ObserverActorInvalid");
                return new 업무배달관찰Actor { Id = a.id, Name = Text(a.name), Role = a.role, State = Text(a.state),
                    NextAction = Text(a.nextAction), WaitReason = Text(a.waitReason), LastAction = Text(a.lastAction), X = a.x, Z = a.z };
            }).ToArray();
            if (dto.events.Any(e => e == null || e.sequence < 0) || dto.events.Select(e => e.sequence).Distinct().Count() != dto.events.Length)
                throw new FormatException("ObserverEventInvalid");
            return new 업무배달관찰Model { RunId = Text(dto.runId), Revision = dto.revision, Status = dto.status,
                OrderNo = Text(dto.orderNo), OrderStatus = Text(dto.orderStatus), DispatchStatus = Text(dto.dispatchStatus), Message = Text(dto.message),
                ElapsedSeconds = dto.elapsedSeconds, DurationSeconds = dto.durationSeconds, Actors = actors, MenuSelection = Menu(dto.menuSelection),
                Events = dto.events.OrderBy(e => e.sequence).Select(e => new 업무배달관찰Event { Sequence = e.sequence, ActorId = Text(e.actorId),
                    Action = Text(e.action), Result = Text(e.result), OccurredAtUtc = Text(e.occurredAtUtc) }).ToArray() };
        }
        private static 업무배달메뉴선택 Menu(업무배달메뉴선택Dto dto)
        {
            if (dto == null) return null; // 이전 r1 사본은 메뉴 선택 정보를 발명하지 않는다.
            if (dto.snapshotHash == null || dto.snapshotHash.Length != 64 || !dto.snapshotHash.All(Uri.IsHexDigit)
                || dto.recipeId < 0 || dto.menuId < 0 || ((dto.recipeId == 0) != (dto.menuId == 0))
                || !Finite(dto.salePrice) || dto.salePrice < 0 || dto.salePrice > 1000000
                || (dto.menuId > 0 && (string.IsNullOrWhiteSpace(dto.title) || dto.salePrice <= 0))
                || dto.candidates == null || dto.candidates.Length > 3)
                throw new FormatException("ObserverMenuInvalid");
            return new 업무배달메뉴선택 { SnapshotHash = dto.snapshotHash, RecipeId = dto.recipeId, MenuId = dto.menuId,
                Title = Text(dto.title), Reason = Text(dto.reason), Source = Text(dto.source), Reference = Text(dto.reference),
                PriceBasis = Text(dto.priceBasis), SalePrice = dto.salePrice, Candidates = dto.candidates.Select(Text).ToArray() };
        }
        private static bool ValidActor(string id, string role) => id switch {
            "customer" => role == "orderer", "restaurant" => role == "restaurant",
            "driver-near" or "driver-far" => role == "driver", _ => false };
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static string Text(string value) => value == null ? "" : value.Length <= 512 ? value : throw new FormatException("ObserverTextTooLong");
    }
}
