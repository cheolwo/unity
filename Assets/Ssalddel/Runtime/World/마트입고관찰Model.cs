using System;
using System.Collections.Generic;
using System.Linq;

namespace Ssalddel.Unity.Runtime.World
{
    public sealed class 마트입고관찰품목
    {
        public string Id, ProductName, Status, StorageLocation;
        public int AvailableQuantity;
    }

    /// <summary>권한으로 제한된 서버 재고의 읽기 사본. 실제 작업자 신원이나 작업 명령을 소유하지 않는다.</summary>
    public sealed class 마트입고관찰Model
    {
        public long WarehouseId;
        public string Revision;
        public DateTimeOffset GeneratedAtUtc;
        public 마트입고관찰품목[] Items;
        public int TotalAvailableQuantity;
    }

    public sealed class 마트입고관찰Store
    {
        // 기존 가상동네생활기준.MartWorkerId와 같은 표현 주체. 모드별 상태는 별도로 소유한다.
        public const string WorkerId = "actor:synthetic-mart-worker";
        public 마트입고관찰Model State { get; private set; }
        public string SelectedItemId { get; private set; } = "";
        public string Error { get; private set; } = "창고 서버 연결 준비 필요";
        public bool InFlight { get; private set; }
        public 마트입고관찰품목 Item => State?.Items.FirstOrDefault(x => x.Id == SelectedItemId);
        public string Stage => Item == null ? "대기" : Item.Status == "적재완료" && !string.IsNullOrWhiteSpace(Item.StorageLocation)
            ? "적치 확인" : Item.Status.StartsWith("검수완료", StringComparison.Ordinal) ? "검수 확인"
            : Item.Status == "보관중" ? "입고 확인" : "상태 확인 필요";
        public bool TryBegin() { if (InFlight) return false; InFlight = true; return true; }
        public void End() => InFlight = false;
        public void Fail(string error) => Error = error;

        public bool Accept(마트입고관찰Model value)
        {
            Validate(value);
            if (State != null && (State.WarehouseId != value.WarehouseId || value.GeneratedAtUtc < State.GeneratedAtUtc))
                throw new InvalidOperationException("WarehouseObservationScopeOrTimeMismatch");
            // Revision은 해시다. 숫자/문자열 대소 비교로 승격하지 않는다.
            if (State != null && value.GeneratedAtUtc == State.GeneratedAtUtc && value.Revision != State.Revision)
                throw new InvalidOperationException("WarehouseObservationRevisionConflict");
            var changed = State == null || State.Revision != value.Revision;
            State = value;
            Error = "";
            if (!value.Items.Any(x => x.Id == SelectedItemId))
                SelectedItemId = value.Items.OrderBy(x => x.Status == "적재완료" ? 1 : 0).ThenBy(x => x.Id, StringComparer.Ordinal).FirstOrDefault()?.Id ?? "";
            return changed;
        }

        public bool Select(string id)
        {
            if (State == null || !State.Items.Any(x => x.Id == id)) return false;
            SelectedItemId = id;
            return true;
        }

        public static void Validate(마트입고관찰Model value)
        {
            if (value == null || value.WarehouseId <= 0 || string.IsNullOrWhiteSpace(value.Revision)
                || value.Revision.Length > 128 || value.GeneratedAtUtc == default || value.TotalAvailableQuantity < 0
                || value.Items == null || value.Items.Length > 50) throw new FormatException("WarehouseObservationInvalid");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in value.Items)
                if (item == null || string.IsNullOrWhiteSpace(item.Id) || !item.Id.StartsWith("warehouse-inventory:", StringComparison.Ordinal)
                    || !ids.Add(item.Id) || item.AvailableQuantity < 0 || !Text(item.ProductName) || !Text(item.Status) || !Text(item.StorageLocation))
                    throw new FormatException("WarehouseObservationItemInvalid");
        }
        private static bool Text(string value) => value != null && value.Length <= 512;
    }
}
