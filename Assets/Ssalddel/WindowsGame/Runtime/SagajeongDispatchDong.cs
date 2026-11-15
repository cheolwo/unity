using System;
using System.Linq;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    public sealed partial class SagajeongDispatchView
    {
        private const int VisibleDongCards = 4;
        private const float DongCardStride = 74;
        private const float DongCardHeight = 68;
        private Rect DongPanel => new(970, 209, 250,
            75 + Mathf.Clamp(shownDongs.Length, 1, VisibleDongCards) * DongCardStride);
        private bool dongPanelOpen;
        private string dongDistrict;
        private string dongZoneName;
        private Vector2 dongScroll;
        private LegalDongList legalDongs;
        private LegalDong[] shownDongs = Array.Empty<LegalDong>();
        private LegalDong viewedDong;

        [Serializable]
        private sealed class LegalDongList
        {
            public LegalDong[] items;
        }

        [Serializable]
        private sealed class LegalDong
        {
            public string id;
            public string district;
            public string name;
            public string description;
            public double latitude;
            public double longitude;
            [NonSerialized] public Texture2D preview;
        }

        private bool MapOverlayCovers(Vector2 point)
        {
            if (SettingsVisible) return true;          // 설정 화면이 떠 있으면 지도는 눌리지 않는다 (SagajeongDispatchSettings.cs)
            if (DongPickerVisible) return true;        // 행정동 이동 창(r236)이 떠 있으면 지도는 누르기 · 휠을 받지 않는다 (SagajeongDispatchDongPicker.cs)
            if (PlaceCovers(point)) return true;       // 홍달 플레이스의 앱 판 · 위 띠 · 번호 핀 (SagajeongDispatchPlace.cs)
            if (LevelPromptCovers(point) || LevelBannerCovers(point) || BuildingCardCovers(point) || NationCovers(point)) return true;
            if (expansionView && !mapOnly)
            {
                if (ExpansionCovers(point) || AttributionPanel.Contains(point) || DongDeskCovers(point) || StoreDeskCovers(point) || MiniMapCovers(point) || HelpCovers(point) ||
                    showDesk && DeskPanel.Contains(point)) return true;
                if (zonePanelOpen) return ZonePanel.Contains(point);
                if (dongPanelOpen) return DongPanel.Contains(point);
                return showDetails && FocusCovers(point);
            }
            if (PanelVisibilityCovers(point) || AttributionPanel.Contains(point)) return true;
            if (mapOnly) return false;
            if ((showTime && TimePanel.Contains(point)) || (showDesk && DeskPanel.Contains(point)) ||
                MapToolbar.Contains(point) || MapStatus.Contains(point) || SessionNoticePanel.Contains(point)) return true;
            if (zonePanelOpen) return ZonePanel.Contains(point);
            if (dongPanelOpen) return DongPanel.Contains(point);
            return showDetails && FocusCovers(point);
        }

        private void DrawDongPickerButton()
        {
            var button = new Rect(813, 136, 118, 28);
            Paint(button, dongPanelOpen ? Rim : Tile, 7);
            Text(button, "법정동 선택", 13, Paper, true, TextAnchor.MiddleCenter);
            if (!Hit(button)) return;

            if (dongPanelOpen && dongDistrict == null && dongZoneName == null)
                dongPanelOpen = false;
            else
                OpenDongPanel(null);
        }

        private void OpenDongPanel(string district, DeliveryZone zone = null)
        {
            dongPanelOpen = true;
            zonePanelOpen = false;
            dongDistrict = district;
            dongZoneName = zone?.name;
            dongScroll = Vector2.zero;
            actorFocus = null;

            LoadLegalDongs();

            var items = legalDongs?.items ?? Array.Empty<LegalDong>();
            shownDongs = zone != null ? items.Where(dong => zone.memberDongIds.Contains(dong.id)).ToArray() :
                district == null ? items : items.Where(dong => dong.district == district).ToArray();
        }

        private void LoadLegalDongs()
        {
            if (legalDongs != null) return;

            legalDongs = ReadMapResource<LegalDongList>("SagajeongDispatch/legal-dong");
            foreach (var dong in legalDongs?.items ?? Array.Empty<LegalDong>())
            {
                var code = dong.id.Substring(dong.id.LastIndexOf(':') + 1);
                dong.preview = Resources.Load<Texture2D>("SagajeongDispatch/dong-previews/" + code);
            }
        }

        private void DrawDongPanel()
        {
            if (!dongPanelOpen) return;

            var x = DongPanel.x + 13;
            var y = DongPanel.y;
            Paint(DongPanel, Rim, 10);
            Paint(new Rect(DongPanel.x + 1, y + 1, DongPanel.width - 2, DongPanel.height - 2), Canvas, 9);
            var title = dongZoneName != null ? "배달권 법정동" :
                dongDistrict == null ? "법정동 지도" : dongDistrict + " 법정동";
            Text(new Rect(x, y + 12, 174, 22), title, 16, Paper, true);
            var close = new Rect(DongPanel.xMax - 59, y + 12, 47, 27);
            Paint(close, Tile, 6);
            Text(close, "닫기", 12, Paper, align: TextAnchor.MiddleCenter);
            if (Hit(close))
            {
                dongPanelOpen = false;
                return;
            }

            var scrollable = shownDongs.Length > VisibleDongCards;
            var caption = dongZoneName ?? (scrollable ? "목록을 내려 더 많은 동네 보기" : "동네 정보와 주변 도로 미리보기");
            Text(new Rect(x, y + 40, 223, 21), caption, 12, Soft);
            if (shownDongs.Length == 0)
            {
                Text(new Rect(x, y + 76, 223, 24), "열 수 있는 법정동이 없습니다.", 13, Soft);
                return;
            }

            LegalDong selected = null;
            if (scrollable)
            {
                const float listWidth = 228;
                var width = listWidth - Mathf.Max(17, GUI.skin.verticalScrollbar.fixedWidth + 2);
                var visibleHeight = (VisibleDongCards - 1) * DongCardStride + DongCardHeight;
                var contentHeight = (shownDongs.Length - 1) * DongCardStride + DongCardHeight;
                dongScroll = GUI.BeginScrollView(new Rect(x, y + 64, listWidth, visibleHeight), dongScroll,
                    new Rect(0, 0, width, contentHeight), false, true);
                selected = DrawDongCards(0, 0, width);
                GUI.EndScrollView();
            }
            else
            {
                selected = DrawDongCards(x, y + 64, 224);
            }

            if (selected == null) return;
            dongPanelOpen = false;
            LoadMapLevel(MapLevel.Dong, selected);
        }

        private LegalDong DrawDongCards(float x, float y, float width)
        {
            for (var index = 0; index < shownDongs.Length; index++)
            {
                var dong = shownDongs[index];
                var card = new Rect(x, y + index * DongCardStride, width, DongCardHeight);
                Paint(card, viewedDong?.id == dong.id ? Rim : Tile, 7);
                var thumb = new Rect(card.x + 7, card.y + 6, 96, 56);
                if (dong.preview != null)
                    GUI.DrawTexture(thumb, dong.preview);
                else
                    Text(thumb, "미리보기 없음", 10, Soft, align: TextAnchor.MiddleCenter);
                var textX = thumb.xMax + 8;
                var textWidth = card.xMax - textX - 3;
                Text(new Rect(textX, card.y + 3, textWidth, 20), dong.name, 15, Paper, true);
                Text(new Rect(textX, card.y + 23, textWidth, 14), dong.district, 10, Soft);
                Text(new Rect(textX, card.y + 36, textWidth, 15), dong.description, 11, Paper);
                Text(new Rect(textX, card.y + 51, textWidth, 14), "지도 보기 ›", 11, Mint);
                if (Hit(card)) return dong;
            }
            return null;
        }
    }
}
