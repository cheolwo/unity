using System;
using System.Linq;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    [DisallowMultipleComponent]
    public sealed class Npc업무행동Presenter : MonoBehaviour
    {
        [SerializeField] private Npc업무행동View[] views = Array.Empty<Npc업무행동View>();
        [SerializeField] private bool presentationOnly = true;

        private readonly Npc업무행동ProjectionStore _store = new();

        public Npc업무행동View[] Views => views.ToArray();
        public Npc업무행동ProjectionData[] CurrentProjections
        {
            get
            {
                if (_store.Current.Count > 0)
                    return _store.Current.Select(value => value.Clone()).ToArray();
                return views
                    .Select(value => value?.CurrentProjection)
                    .Where(value => value != null && value.Validate())
                    .Select(value => value!)
                    .ToArray();
            }
        }
        public bool PresentationOnly => presentationOnly;

        public void Configure(
            Npc업무행동View[] targetViews,
            Npc업무행동ProjectionData[] initialProjections)
        {
            views = targetViews?.ToArray() ?? Array.Empty<Npc업무행동View>();
            presentationOnly = true;
            ApplyAuthoritativeProjections(initialProjections);
        }

        public void ApplyAuthoritativeProjections(
            Npc업무행동ProjectionData[] projections)
        {
            if (!ValidateViews())
                throw new InvalidOperationException("NpcWorkActionPresenterWiringInvalid");
            _store.Apply(projections ?? throw new ArgumentNullException(nameof(projections)));
            foreach (var projection in _store.Current)
            {
                var view = views.SingleOrDefault(value =>
                    string.Equals(value.ActorStableId, projection.ActorStableId, StringComparison.Ordinal));
                if (view != null) view.ApplyAuthoritativeProjection(projection);
            }
        }

        public bool ValidateWiring()
            => presentationOnly && ValidateViews();

        private void OnEnable()
        {
            if (_store.Current.Count > 0 || !ValidateViews()) return;
            var restored = views
                .Select(value => value.CurrentProjection)
                .Where(value => value != null && value.Validate())
                .Select(value => value!)
                .ToArray();
            if (restored.Length > 0) _store.Apply(restored);
        }

        public void TickPresentation(float deltaTime)
        {
            foreach (var view in views)
                view.TickPresentation(deltaTime);
        }

        private void Update() => TickPresentation(Time.deltaTime);

        private bool ValidateViews()
            => views.Length > 0
                && views.All(value => value != null && value.ValidateWiring())
                && views.Select(value => value.ActorStableId)
                    .Distinct(StringComparer.Ordinal).Count() == views.Length;
    }
}
