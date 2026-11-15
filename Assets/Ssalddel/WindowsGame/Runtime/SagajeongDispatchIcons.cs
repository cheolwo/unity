using System;
using System.Collections;
using Ssalddel.WindowsGame.Kernel;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 그림 중심 왼쪽 창 — 그1: 아이콘 한 벌 (2026-11-15).
    //  - 제안서(Handoffs/2026-11-14-Claude-제안-왼쪽창-그림중심.md) 4절의 A: Claude가 직접 그린 선 아이콘 29개다.
    //    실제 가게 로고 · 사람 사진 · 알려진 캐릭터가 아니다.
    //  - 흰 선으로 한 장(512 × 256, 한 칸 64 px, 8열 4줄)에 모아 Resources/hongdal-icons.bytes(PNG)로 넣었다.
    //    Unity의 가져오기 설정(압축 · 밉맵)에 기대지 않도록 PNG 바이트를 읽어 직접 푼다.
    //  - Icon(rect, name, colour)이 대상 색으로 칠해 그린다. 이름이 없거나 그림을 못 읽으면 아무것도 그리지 않고 false를 돌려준다.
    //    그래서 부르는 쪽은 아이콘이 없어도 글로 읽히게 둔다.
    //  - 창 모양은 아직 바꾸지 않는다(그2부터, 설정 스위치 뒤).
    public sealed partial class SagajeongDispatchView
    {
        private const string IconResource = "hongdal-icons";
        private const int IconColumns = 8;
        private const int IconRows = 4;
        private const int IconCell = 64;

        // 그림의 칸 순서와 같다(왼쪽 위부터 가로로).
        private static readonly string[] IconNames =
        {
            "cup", "milk", "bean", "machine", "grinder", "ice", "barista", "rider",
            "scooter", "truck", "box", "won", "receipt", "clock", "check", "alert",
            "store", "warehouse", "pin", "hq", "history", "home", "route", "eye",
            "search", "tool", "summary", "people", "follow",
        };

        private Texture2D iconAtlas;
        private bool iconAtlasTried;
        private bool iconProofSheet;          // 방향 촬영에서만: 아이콘 한 벌을 화면 가운데에 늘어놓는다

        // 그2 (2026-11-15): 「왼쪽 창: 그림 중심(시험)」 설정. 사용자가 제안서 6절의 넷을 고르기 전이라 기본은 지금 창(글 중심)이다.
        // 켜면 매장 운영 창의 머리에 매장 그림(라일락)과 막힘 「!」, 탭에 아이콘이 붙는다. 검증 촬영(--sagajeong-*)에서는 취향을 쓰지 않는다.
        private const string PictureDeskPreference = "hongdal.windows.pictureDesk";
        private static readonly string[] StoreTabIcons = { "summary", "milk", "machine", "people", "history" };
        private bool pictureDesk;
        private bool pictureDeskRead;
        private bool pictureDeskDrawn;         // 이번 그리기에서 머리 그림을 그렸나 (촬영이 읽는다)

        private bool PictureDesk
        {
            get
            {
                if (pictureDeskRead) return pictureDesk;
                pictureDeskRead = true;
                pictureDesk = mapEffectsPersistent && PlayerPrefs.GetInt(PictureDeskPreference, 0) == 1;
                return pictureDesk;
            }
        }

        private void SetPictureDesk(bool on)
        {
            if (PictureDesk == on) return;
            pictureDesk = on;
            if (mapEffectsPersistent)
            {
                PlayerPrefs.SetInt(PictureDeskPreference, on ? 1 : 0);
                PlayerPrefs.Save();
            }
            Debug.Log("Hongdal picture desk: " + (on ? "그림 중심(시험)" : "글 중심"));
        }

        // 그 자리에 울리는 신호 가운데 첫 것(위험 먼저). 없으면 null.
        private Signal PlaceSignal(string place)
        {
            if (string.IsNullOrEmpty(place)) return null;
            foreach (var signal in signals)
                if (signal.PlaceId == place) return signal;
            return null;
        }

        // 매장 창 머리의 그림: 대상 색의 둥근 칸에 매장 아이콘, 막혔으면 오른쪽 위에 지도와 같은 「!」.
        private void DrawStoreHeadPicture(Rect tile, string place)
        {
            Paint(tile, Fade(Lilac, 0.16f), 10);
            Paint(new Rect(tile.x, tile.yMax - 3, tile.width, 3), Fade(Lilac, 0.55f), 1);
            pictureDeskDrawn = Icon(new Rect(tile.x + 6, tile.y + 5, tile.width - 12, tile.height - 12), "store", Lilac);
            var signal = PlaceSignal(place);
            if (signal == null) return;
            var centre = new Vector2(tile.xMax - 2, tile.y + 2);
            Disc(centre, 8f, Canvas);
            Disc(centre, 6.5f, SignalColor(signal));
            Text(Centered(centre, 14, 14), "!", 10, Canvas, true, TextAnchor.MiddleCenter);
        }

        private Texture2D EnsureIcons()
        {
            if (iconAtlasTried) return iconAtlas;
            iconAtlasTried = true;
            var asset = Resources.Load<TextAsset>(IconResource);
            if (asset == null)
            {
                Debug.LogWarning("Hongdal icons: resource missing");
                return null;
            }
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(asset.bytes, true) || texture.width != IconColumns * IconCell || texture.height != IconRows * IconCell)
            {
                Debug.LogWarning("Hongdal icons: unreadable atlas");
                Destroy(texture);
                return null;
            }
            texture.name = "Hongdal icons";
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            iconAtlas = texture;
            Debug.Log($"Hongdal icons: {IconNames.Length} in {texture.width} x {texture.height}");
            return iconAtlas;
        }

        // 아이콘 하나를 rect에 colour로 그린다. 그렸으면 true.
        private bool Icon(Rect rect, string name, Color colour)
        {
            var index = Array.IndexOf(IconNames, name);
            if (index < 0) return false;
            var atlas = EnsureIcons();
            if (atlas == null) return false;
            var column = index % IconColumns;
            var row = index / IconColumns;
            var cell = new Rect(column / (float)IconColumns, 1f - (row + 1) / (float)IconRows, 1f / IconColumns, 1f / IconRows);
            var keep = GUI.color;
            GUI.color = new Color(colour.r, colour.g, colour.b, colour.a * keep.a);
            GUI.DrawTextureWithTexCoords(rect, atlas, cell, true);
            GUI.color = keep;
            return true;
        }

        // 방향 촬영의 한 장면: 아이콘 한 벌을 크기 셋 · 색 여럿으로 늘어놓는다. 촬영 밖에서는 그리지 않는다.
        private void DrawIconProofSheet()
        {
            if (!iconProofSheet) return;
            var box = new Rect(250, 120, 780, 430);
            Paint(box, Fade(Canvas, 0.95f), 12);
            Text(new Rect(box.x + 20, box.y + 12, 700, 28), "아이콘 한 벌 · 그1 · 직접 그림 · 내부 검토용", 14, Paper, true);
            var tints = new[] { Paper, Mint, Lilac, Gold };
            for (var k = 0; k < IconNames.Length; k++)
            {
                var x = box.x + 22 + (k % 10) * 74;
                var y = box.y + 52 + (k / 10) * 122;
                Icon(new Rect(x + 13, y, 48, 48), IconNames[k], tints[k % tints.Length]);
                Icon(new Rect(x + 8, y + 56, 24, 24), IconNames[k], Soft);
                Icon(new Rect(x + 40, y + 60, 16, 16), IconNames[k], Red);
                Text(new Rect(x - 4, y + 84, 82, 20), IconNames[k], 10, Soft, align: TextAnchor.MiddleCenter);
            }
        }

        private IEnumerator ViewProofIcons(string directory, ViewProofReceipt receipt)
        {
            var atlas = EnsureIcons();
            receipt.iconCount = atlas == null ? 0 : IconNames.Length;
            receipt.iconAtlasWidth = atlas == null ? 0 : atlas.width;
            receipt.iconAtlasHeight = atlas == null ? 0 : atlas.height;
            receipt.iconMissingRefused = !Icon(new Rect(0, 0, 1, 1), "없는 이름", Paper) && StoreTabIcons.Length == StoreDeskTabs.Length &&
                Array.TrueForAll(StoreTabIcons, name => Array.IndexOf(IconNames, name) >= 0);       // 없는 이름은 그리지 않는다(GUI를 부르기 전에 돌아온다)
            iconProofSheet = true;
            yield return null;
            yield return null;
            yield return SaveFrame(directory, "70-icons.png");
            iconProofSheet = false;
            yield return null;
        }
    }
}
