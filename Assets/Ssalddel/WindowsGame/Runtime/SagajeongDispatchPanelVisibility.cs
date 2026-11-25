using UnityEngine;

namespace Ssalddel.WindowsGame
{
    public sealed partial class SagajeongDispatchView
    {
        private static readonly Rect PanelVisibilityStrip = new(390, 13, 302, 28);
        private static readonly Rect MapRestoreStrip = new(591, 13, 101, 28);

        private bool mapOnly;
        private bool showDesk;
        private bool showTime;
        private bool showDetails = true;

        private bool PanelVisibilityCovers(Vector2 point) =>
            (mapOnly ? MapRestoreStrip : PanelVisibilityStrip).Contains(point) ||
            (!expansionView && new Rect(420, 680, 120, 24).Contains(point));

        private void ToggleMapOnly() => mapOnly = !mapOnly;

        private void ToggleDesk()
        {
            mapOnly = false;
            showDesk = !showDesk;
            if (!showDesk) 관찰업무복귀();
        }

        private void ToggleTime()
        {
            mapOnly = false;
            showTime = !showTime;
        }

        private void ToggleDetails()
        {
            mapOnly = false;
            showDetails = !showDetails;
        }

        private void DrawPanelVisibility()
        {
            if (!expansionView && DrawPanelSwitch(new Rect(420, 680, 120, 24), "경영 화면", false))
                ShowExpansionView();
            var strip = mapOnly ? MapRestoreStrip : PanelVisibilityStrip;
            Paint(strip, Canvas, 7);

            var mapButton = new Rect(596, 15, 91, 24);
            if (mapOnly)
            {
                if (DrawPanelSwitch(mapButton, "패널 복원", true))
                    ToggleMapOnly();
                return;
            }

            if (DrawPanelSwitch(new Rect(395, 15, 62, 24), showDesk ? "배차 숨김" : "배차 보기", showDesk))
                ToggleDesk();
            if (DrawPanelSwitch(new Rect(462, 15, 62, 24), showTime ? "시계 숨김" : "시계 보기", showTime))
                ToggleTime();
            if (DrawPanelSwitch(new Rect(529, 15, 62, 24), showDetails ? "상세 숨김" : "상세 보기", showDetails))
                ToggleDetails();
            if (DrawPanelSwitch(mapButton, "지도만 보기", false))
                ToggleMapOnly();
        }

        private void ShowExpansionView()
        {
            expansionView = true;
            mapOnly = false;
            showDesk = false;
            showTime = false;
            showDetails = true;
        }

        private bool DrawPanelSwitch(Rect rect, string label, bool active)
        {
            Paint(rect, active ? Tile : Panel, 6);
            Text(rect, label, 11, active ? Mint : Paper, true, TextAnchor.MiddleCenter);
            return Hit(rect);
        }
    }
}
