using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Unity.Application;
using Ssalddel.Unity.InterpretationContracts;
using Ssalddel.Unity.UrbanMarket;
using UnityEngine;

namespace Ssalddel.Unity.Samples.UrbanMarket
{
    public sealed class 도심마트ManagerSurfaceView : MonoBehaviour
    {
        [SerializeField] private TextMesh statusText = null!;
        [SerializeField] private TextMesh taskText = null!;
        [SerializeField] private TextMesh sourcePlanText = null!;
        [SerializeField] private TextMesh detailText = null!;
        [SerializeField] private 도심마트ManagerShelfView[] shelfViews = Array.Empty<도심마트ManagerShelfView>();

        private Dictionary<string, 도심마트ManagerShelfView>? shelvesById;

        public event Action<WorldStableId>? ShelfSelected;

        public void Configure(
            TextMesh status,
            TextMesh tasks,
            TextMesh sourcePlans,
            TextMesh details,
            도심마트ManagerShelfView[] shelves)
        {
            statusText = status;
            taskText = tasks;
            sourcePlanText = sourcePlans;
            detailText = details;
            shelfViews = shelves ?? Array.Empty<도심마트ManagerShelfView>();
            shelvesById = null;
        }

        public void ShowLoading(bool refresh)
            => statusText.text = refresh ? "Refreshing · 마지막 성공 화면 유지" : "Loading manager world...";

        public void Apply(도심마트ManagerRuntimeResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            statusText.text = StatusText(result.Status);
            if (result.Presentation == null)
            {
                if (!result.Status.IsShowingLastSuccess) ClearAll();
                return;
            }
            if (result.Changes == null) return;

            var presentation = result.Presentation;
            if (Changed(result.Changes.TaskMarkers))
                taskText.text = Join(presentation.TaskMarkers.Select(value => value.LabelText + " · " + value.StateCode));
            if (Changed(result.Changes.SourcePlans))
                sourcePlanText.text = Join(presentation.SourcePlans.Select(value =>
                    value.Sequence + ". " + value.FromLabelText + " → " + value.ToLabelText + " · " + value.QuantityText));
            if (Changed(result.Changes.Details))
                detailText.text = Join(presentation.Details.Select(value =>
                    value.TitleText + "\n" + value.QuantityText + "\n" + value.ReasonText + "\n" + value.BoundaryText));

            ApplyShelves(result.Changes, presentation);
        }

        public bool ValidateWiring()
        {
            if (statusText == null || taskText == null || sourcePlanText == null || detailText == null
                || shelfViews == null || shelfViews.Length == 0)
                return false;
            if (shelfViews.Any(value => value == null || !value.ValidateWiring())) return false;
            return shelfViews.Select(value => value.PresentationStableId)
                       .Distinct(StringComparer.Ordinal).Count() == shelfViews.Length;
        }

        private void ApplyShelves(
            도심마트ManagerSurfaceChangeSet changes,
            도심마트PresentationSnapshot presentation)
        {
            EnsureShelfIndex();
            foreach (var removed in changes.Shelves.Removed)
                if (shelvesById!.TryGetValue(removed.StableId.Value, out var removedView)) removedView.Hide();
            foreach (var item in changes.Shelves.Added.Concat(changes.Shelves.Updated))
            {
                if (!shelvesById!.TryGetValue(item.StableId.Value, out var view))
                    throw new InvalidOperationException("UrbanMarketManagerShelfViewMissing:" + item.StableId.Value);
                view.Apply(item, value => ShelfSelected?.Invoke(value));
            }
            var incoming = presentation.Shelves.Select(value => value.StableId.Value).ToHashSet(StringComparer.Ordinal);
            foreach (var pair in shelvesById!.Where(pair => !incoming.Contains(pair.Key))) pair.Value.Hide();
        }

        private void EnsureShelfIndex()
            => shelvesById ??= shelfViews.ToDictionary(value => value.PresentationStableId, StringComparer.Ordinal);

        private void ClearAll()
        {
            taskText.text = string.Empty;
            sourcePlanText.text = string.Empty;
            detailText.text = string.Empty;
            foreach (var shelf in shelfViews) shelf.Hide();
        }

        private static bool Changed<T>(Ssalddel.Unity.PresentationContracts.Reconciliation.StableIdChangeSet<T> changes)
            => changes.Added.Length + changes.Updated.Length + changes.Removed.Length > 0;

        private static string Join(IEnumerable<string> lines)
            => string.Join("\n", lines.Where(value => !string.IsNullOrWhiteSpace(value)));

        private static string StatusText(ZoneRuntimeStatus status)
        {
            if (status.StateCode == ZoneRuntimeStateCode.Ready) return "Ready";
            var text = status.StateCode + (string.IsNullOrWhiteSpace(status.SafeErrorCode) ? string.Empty : " · " + status.SafeErrorCode);
            return status.IsShowingLastSuccess ? text + " · 마지막 성공 화면 유지" : text;
        }
    }
}
