using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Unity.PresentationContracts.Cargo;
using UnityEngine;
using UnityEngine.UI;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration
{
    [Serializable]
    public sealed class WorldQualityStageBinding
    {
        public string ZoneCode = string.Empty;
        public Image Background = null!;
        public Text Label = null!;
    }

    [DisallowMultipleComponent]
    public sealed class WorldVisualQualityGateView : MonoBehaviour
    {
        [SerializeField] private Canvas canvas = null!;
        [SerializeField] private Text title = null!;
        [SerializeField] private Text status = null!;
        [SerializeField] private Text boundary = null!;
        [SerializeField] private CargoJourneyView journey = null!;
        [SerializeField] private CargoJourneyAnchorView[] anchors =
            Array.Empty<CargoJourneyAnchorView>();
        [SerializeField] private WorldQualityStageBinding[] stages =
            Array.Empty<WorldQualityStageBinding>();
        [SerializeField] private string cargoStableId = string.Empty;
        [SerializeField] private string currentZoneCode = string.Empty;
        [SerializeField] private string marketStateCode = string.Empty;

        public string CargoStableId => cargoStableId;
        public string CurrentZoneCode => currentZoneCode;
        public string MarketStateCode => marketStateCode;
        public int StageCount => stages?.Length ?? 0;
        public Canvas Canvas => canvas;

        public void Configure(
            Canvas targetCanvas,
            Text titleText,
            Text statusText,
            Text boundaryText,
            CargoJourneyView cargoJourney,
            CargoJourneyAnchorView[] anchorViews,
            WorldQualityStageBinding[] stageBindings)
        {
            canvas = targetCanvas;
            title = titleText;
            status = statusText;
            boundary = boundaryText;
            journey = cargoJourney;
            anchors = anchorViews ?? Array.Empty<CargoJourneyAnchorView>();
            stages = stageBindings ?? Array.Empty<WorldQualityStageBinding>();
        }

        public void Apply()
        {
            if (!ValidateWiring())
                throw new InvalidOperationException("WORLD5QualityGateWiringInvalid");

            var byZone = anchors.ToDictionary(value => value.ZoneCode, StringComparer.Ordinal);
            foreach (var stage in stages)
            {
                var anchor = byZone[stage.ZoneCode];
                stage.Background.color = ColorFor(anchor.StateCode);
                stage.Label.text = DisplayZone(stage.ZoneCode) + "\n" + anchor.StateCode.ToUpperInvariant();
            }

            cargoStableId = journey.CargoStableId;
            currentZoneCode = journey.CurrentZoneCode;
            marketStateCode = byZone[CargoJourneyZoneCodes.UrbanMarket].StateCode;
            title.text = "POTATO SUPPLY CHAIN";
            status.text = cargoStableId + "  |  CURRENT: " + DisplayZone(currentZoneCode)
                + "  |  MARKET: " + marketStateCode.ToUpperInvariant();
            boundary.text = "PRESENTATION ONLY  |  ARRIVAL DOES NOT CONFIRM WORK";
        }

        public bool ValidateWiring()
        {
            var zones = KnownZones();
            var stageZones = KnownZones();
            return canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera
                && canvas.worldCamera != null
                && title != null && status != null && boundary != null
                && journey != null && journey.ValidateApplied()
                && anchors != null && anchors.Length == zones.Count
                && anchors.All(value => value != null && value.ValidateApplied()
                    && zones.Remove(value.ZoneCode))
                && zones.Count == 0
                && stages != null && stages.Length == 4
                && stages.All(value => value != null
                    && !string.IsNullOrWhiteSpace(value.ZoneCode)
                    && value.Background != null && value.Label != null)
                && stages.All(value => stageZones.Remove(value.ZoneCode))
                && stageZones.Count == 0;
        }

        public bool ValidateApplied()
            => ValidateWiring()
               && cargoStableId == journey.CargoStableId
               && currentZoneCode == journey.CurrentZoneCode
               && marketStateCode == CargoJourneyAnchorStateCodes.Planned
               && !string.IsNullOrWhiteSpace(title.text)
               && !string.IsNullOrWhiteSpace(status.text)
               && !string.IsNullOrWhiteSpace(boundary.text);

        private static HashSet<string> KnownZones()
            => new HashSet<string>(new[]
            {
                CargoJourneyZoneCodes.FarmYard,
                CargoJourneyZoneCodes.TransportCorridor,
                CargoJourneyZoneCodes.UrbanLogistics,
                CargoJourneyZoneCodes.UrbanMarket,
            }, StringComparer.Ordinal);

        private static string DisplayZone(string zoneCode)
            => zoneCode switch
            {
                CargoJourneyZoneCodes.FarmYard => "FARM YARD",
                CargoJourneyZoneCodes.TransportCorridor => "TRANSPORT",
                CargoJourneyZoneCodes.UrbanLogistics => "LOGISTICS",
                CargoJourneyZoneCodes.UrbanMarket => "MARKET",
                _ => zoneCode.ToUpperInvariant(),
            };

        private static Color ColorFor(string stateCode)
            => stateCode switch
            {
                CargoJourneyAnchorStateCodes.Previous => new Color(.20f, .38f, .58f, .94f),
                CargoJourneyAnchorStateCodes.Current => new Color(.94f, .52f, .08f, .96f),
                CargoJourneyAnchorStateCodes.Next => new Color(.18f, .62f, .34f, .94f),
                CargoJourneyAnchorStateCodes.Planned => new Color(.42f, .34f, .58f, .92f),
                _ => throw new InvalidOperationException("WORLD5StageStateUnknown:" + stateCode),
            };
    }
}
