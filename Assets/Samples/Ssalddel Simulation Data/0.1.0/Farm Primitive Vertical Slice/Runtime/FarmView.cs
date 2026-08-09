using Ssalddel.Unity.Farm;
using UnityEngine;

namespace Ssalddel.Unity.Samples.Farm
{
    public sealed class FarmView : MonoBehaviour
    {
        [SerializeField] private FarmTileView[] plots = System.Array.Empty<FarmTileView>();
        [SerializeField] private CropView[] crops = System.Array.Empty<CropView>();
        [SerializeField] private SensorView[] sensors = System.Array.Empty<SensorView>();
        [SerializeField] private FarmWorkerView[] workers = System.Array.Empty<FarmWorkerView>();
        [SerializeField] private TextMesh status = null!;

        public void Configure(
            FarmTileView[] plotViews,
            CropView[] cropViews,
            SensorView[] sensorViews,
            FarmWorkerView[] workerViews,
            TextMesh statusText)
        {
            plots = plotViews;
            crops = cropViews;
            sensors = sensorViews;
            workers = workerViews;
            status = statusText;
        }

        public void ShowLoading()
        {
            status.text = "FARM · LOADING AUTHORIZED DATA";
            SetTargetsVisible(false);
        }

        public string[] Render(
            FarmProducerPerspectiveSnapshot snapshot,
            FarmProducerPerspectiveApplicator applicator)
        {
            SetTargetsVisible(true);
            status.text = "FARM · " + snapshot.SourceTypeCode + " · REV " + snapshot.Revision;
            return applicator.Apply(snapshot, plots, crops, sensors, workers);
        }

        public void ShowError(string message)
        {
            status.text = "FARM ERROR · " + message;
            SetTargetsVisible(false);
        }

        public bool ValidateWiring()
        {
            if (status == null || plots.Length == 0 || crops.Length == 0
                || sensors.Length == 0 || workers.Length == 0)
            {
                return false;
            }

            foreach (var plot in plots) if (plot == null || !plot.ValidateWiring()) return false;
            foreach (var crop in crops) if (crop == null || !crop.ValidateWiring()) return false;
            foreach (var sensor in sensors) if (sensor == null || !sensor.ValidateWiring()) return false;
            foreach (var worker in workers) if (worker == null || !worker.ValidateWiring()) return false;
            return true;
        }

        private void SetTargetsVisible(bool visible)
        {
            foreach (var plot in plots) plot.gameObject.SetActive(visible);
            foreach (var crop in crops) crop.gameObject.SetActive(visible);
            foreach (var sensor in sensors) sensor.gameObject.SetActive(visible);
            foreach (var worker in workers) worker.gameObject.SetActive(visible);
        }
    }
}
