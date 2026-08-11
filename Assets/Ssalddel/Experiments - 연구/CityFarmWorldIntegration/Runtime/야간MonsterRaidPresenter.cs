using System;
using System.Linq;
using Ssalddel.Unity.Presentation.World;
using UnityEngine;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration
{
    [DisallowMultipleComponent]
    public sealed class 야간MonsterRaidPresenter : MonoBehaviour
    {
        [SerializeField] private 월드시간대Presenter timeOfDayPresenter = null!;
        [SerializeField] private 절차형VehicleRouteFollower truckFollower = null!;
        [SerializeField] private Transform truckInterceptAnchor = null!;
        [SerializeField] private GameObject raidVisualRoot = null!;
        [SerializeField] private Transform[] monsters = Array.Empty<Transform>();
        [SerializeField] private Transform[] spawnAnchors = Array.Empty<Transform>();
        [SerializeField] private Transform[] blockAnchors = Array.Empty<Transform>();
        [SerializeField] private Transform[] escapeAnchors = Array.Empty<Transform>();
        [SerializeField] private GameObject[] carriedCargo = Array.Empty<GameObject>();

        private Vector3[] truckDayPositions = Array.Empty<Vector3>();

        public string SourceMode => "Simulation";
        public int MonsterCount => monsters.Length;
        public int CarriedCargoCount => carriedCargo.Length;
        public bool IsRaidVisible => raidVisualRoot != null && raidVisualRoot.activeSelf;
        public bool IsLootVisible => carriedCargo.Length > 0
            && carriedCargo.All(value => value != null && value.activeSelf);

        public void Configure(
            월드시간대Presenter worldTime,
            절차형VehicleRouteFollower outboundTruck,
            Transform interceptAnchor,
            GameObject visualRoot,
            Transform[] monsterActors,
            Transform[] emergenceAnchors,
            Transform[] roadBlockAnchors,
            Transform[] retreatAnchors,
            GameObject[] cargoProps)
        {
            timeOfDayPresenter = worldTime;
            truckFollower = outboundTruck;
            truckInterceptAnchor = interceptAnchor;
            raidVisualRoot = visualRoot;
            monsters = monsterActors ?? Array.Empty<Transform>();
            spawnAnchors = emergenceAnchors ?? Array.Empty<Transform>();
            blockAnchors = roadBlockAnchors ?? Array.Empty<Transform>();
            escapeAnchors = retreatAnchors ?? Array.Empty<Transform>();
            carriedCargo = cargoProps ?? Array.Empty<GameObject>();
            truckDayPositions = new[] { truckFollower.RouteStart.position };
            ApplyDayPreview();
        }

        public bool ValidateWiring()
            => timeOfDayPresenter != null
               && timeOfDayPresenter.ValidateWiring()
               && truckFollower != null
               && truckFollower.ValidateWiring()
               && truckInterceptAnchor != null
               && raidVisualRoot != null
               && monsters.Length == 3
               && spawnAnchors.Length == monsters.Length
               && blockAnchors.Length == monsters.Length
               && escapeAnchors.Length == monsters.Length
               && carriedCargo.Length == 2
               && carriedCargo.All(value => value != null);

        public void ApplyDayPreview()
        {
            timeOfDayPresenter.ApplyNowForTests(12.5f / 24f);
            if (raidVisualRoot != null) raidVisualRoot.SetActive(false);
            if (truckFollower != null)
            {
                truckFollower.enabled = true;
                if (truckDayPositions.Length > 0)
                    truckFollower.transform.position = truckDayPositions[0];
            }
            SetCargoVisible(false);
        }

        public void ApplyNightPreview(float progress = .62f)
        {
            if (!ValidateWiring())
                throw new InvalidOperationException("NightMonsterRaidWiringInvalid");

            timeOfDayPresenter.ApplyNowForTests(19.75f / 24f);
            raidVisualRoot.SetActive(true);
            truckFollower.enabled = false;
            truckFollower.transform.position = truckInterceptAnchor.position;
            progress = Mathf.Clamp01(progress);

            for (var index = 0; index < monsters.Length; index++)
            {
                var start = spawnAnchors[index].position;
                var block = blockAnchors[index].position;
                var escape = escapeAnchors[index].position;
                monsters[index].position = progress <= .62f
                    ? Vector3.Lerp(start, block, progress / .62f)
                    : Vector3.Lerp(block, escape, (progress - .62f) / .38f);
                var lookTarget = progress <= .62f ? truckInterceptAnchor.position : escape;
                var direction = lookTarget - monsters[index].position;
                direction.y = 0f;
                if (direction.sqrMagnitude > .0001f)
                    monsters[index].rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }

            SetCargoVisible(progress >= .48f);
        }

        private void SetCargoVisible(bool visible)
        {
            foreach (var cargo in carriedCargo)
                if (cargo != null) cargo.SetActive(visible);
        }
    }
}
