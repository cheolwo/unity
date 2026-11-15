using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    public sealed partial class SagajeongDispatchView
    {
        private static readonly Rect ZonePanel = new(864, 209, 356, 371);
        private const int ZoneNameLimit = 16;
        private readonly HashSet<string> zoneMembers = new(StringComparer.Ordinal);
        private bool zonePanelOpen;
        private bool confirmZoneRemoval;
        private int zoneIndex = -1;
        private string zoneName = "";
        private string zoneMessage;
        private string zoneDiskText;
        private DeliveryZoneBook zoneBook;
        private GUIStyle zoneFieldStyle;
        private static string ZonePath => Path.Combine(Application.persistentDataPath, "delivery-zones.v1.json");

        [Serializable]
        private sealed class DeliveryZone
        {
            public string id;
            public string name;
            public string[] memberDongIds;
        }

        [Serializable]
        private sealed class DeliveryZoneBook
        {
            public int version = 1;
            public DeliveryZone[] zones = Array.Empty<DeliveryZone>();
        }

        private void DrawZoneButton()
        {
            if (!ZoneButton(new Rect(975, 621, 115, 30), "배달권 설정", zonePanelOpen ? Rim : Tile, Paper))
                return;

            if (zonePanelOpen)
            {
                zonePanelOpen = false;
                return;
            }

            LoadLegalDongs();
            actorFocus = null;
            dongPanelOpen = false;
            zonePanelOpen = true;
            LoadZoneBook();
        }

        private void LoadZoneBook()
        {
            zoneBook = null;
            zoneMessage = null;
            try
            {
                zoneDiskText = ReadZoneFile();
                var book = zoneDiskText == null ? new DeliveryZoneBook() : JsonUtility.FromJson<DeliveryZoneBook>(zoneDiskText);
                if (book?.version != 1 || book.zones == null || book.zones.Any(zone =>
                        string.IsNullOrEmpty(zone?.id) || string.IsNullOrWhiteSpace(zone.name) || zone.memberDongIds == null))
                {
                    zoneMessage = "저장한 배달권 형식을 읽을 수 없습니다.";
                    return;
                }

                zoneBook = book;
                EditZone(book.zones.Length == 0 ? -1 : Mathf.Clamp(zoneIndex, 0, book.zones.Length - 1));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
            {
                zoneMessage = "저장한 배달권을 읽을 수 없습니다.";
            }
        }

        private static string ReadZoneFile()
        {
            try { return File.ReadAllText(ZonePath); }
            catch (FileNotFoundException) { return null; }
            catch (DirectoryNotFoundException) { return null; }
        }

        private void EditZone(int index)
        {
            zoneIndex = index;
            var zone = index < 0 ? null : zoneBook.zones[index];
            zoneName = zone?.name ?? "";
            zoneMembers.Clear();
            if (zone != null) zoneMembers.UnionWith(zone.memberDongIds);
            confirmZoneRemoval = false;
            zoneMessage = null;
        }

        private void DrawZonePanel()
        {
            if (!zonePanelOpen) return;

            var x = ZonePanel.x + 14;
            var y = ZonePanel.y;
            Paint(ZonePanel, Rim, 10);
            Paint(new Rect(ZonePanel.x + 1, y + 1, ZonePanel.width - 2, ZonePanel.height - 2), Canvas, 9);
            Text(new Rect(x, y + 9, 238, 27), "배달권 설정", 17, Paper, true);
            if (ZoneButton(new Rect(x + 275, y + 11, 51, 25), "닫기", Tile, Paper))
            {
                zonePanelOpen = false;
                return;
            }

            Text(new Rect(x, y + 38, 328, 18), "법정동을 골라 가상 배달권을 만드세요.", 12, Soft);
            if (zoneBook == null)
            {
                Text(new Rect(x, y + 68, 326, 24), zoneMessage, 13, Gold);
                return;
            }

            DrawZonePicker(x, y);
            Text(new Rect(x, y + 100, 74, 29), "권역 이름", 12, Soft);
            zoneFieldStyle ??= new GUIStyle(GUI.skin.textField) { font = font, fontSize = 14 };
            var editedName = GUI.TextField(new Rect(x + 74, y + 101, 252, 28), zoneName, ZoneNameLimit, zoneFieldStyle);
            if (editedName != zoneName)
            {
                zoneName = editedName;
                zoneMessage = null;
                confirmZoneRemoval = false;
            }

            Text(new Rect(x, y + 137, 326, 22), $"구성 법정동  ·  {zoneMembers.Count}곳 선택", 13, Paper, true);
            DrawZoneMembers(x, y + 162);
            if (ZoneButton(new Rect(x, y + 296, 75, 30), "저장", Mint, Canvas)) SaveZone();
            if (zoneIndex >= 0)
            {
                if (ZoneButton(new Rect(x + 84, y + 296, 127, 30), "저장한 구성 보기", Tile, Paper))
                    OpenDongPanel(null, zoneBook.zones[zoneIndex]);
                if (ZoneButton(new Rect(x + 220, y + 296, 106, 30),
                    confirmZoneRemoval ? "삭제 확인" : "배달권 삭제", Tile, Gold))
                    RemoveZone();
            }
            Text(new Rect(x, y + 335, 326, 22), zoneMessage ?? "법정동은 한 배달권에만 넣을 수 있습니다.", 12,
                zoneMessage == null ? Soft : Gold);
        }

        private void DrawZonePicker(float x, float y)
        {
            var count = zoneBook.zones.Length;
            var pickerColor = count == 0 ? Soft : Paper;
            if (ZoneButton(new Rect(x, y + 64, 44, 27), "이전", Tile, pickerColor) && count > 0)
                EditZone(zoneIndex < 0 ? count - 1 : (zoneIndex + count - 1) % count);
            if (ZoneButton(new Rect(x + 51, y + 64, 44, 27), "다음", Tile, pickerColor) && count > 0)
                EditZone((zoneIndex + 1) % count);
            var label = zoneIndex < 0 ? $"새 배달권 · 저장 {count}개" : $"저장한 배달권 {zoneIndex + 1} / {count}";
            Text(new Rect(x + 104, y + 64, 139, 27), label, 11, Soft);
            if (ZoneButton(new Rect(x + 246, y + 64, 80, 27), "새 배달권", Tile, Paper))
                EditZone(-1);
        }

        private void DrawZoneMembers(float x, float y)
        {
            var dongs = legalDongs?.items ?? Array.Empty<LegalDong>();
            var currentId = zoneIndex < 0 ? null : zoneBook.zones[zoneIndex].id;
            for (var index = 0; index < dongs.Length; index++)
            {
                var dong = dongs[index];
                var cell = new Rect(x + index % 2 * 166, y + index / 2 * 31, 160, 26);
                var other = zoneBook.zones.FirstOrDefault(zone => zone.id != currentId && zone.memberDongIds.Contains(dong.id));
                var chosen = zoneMembers.Contains(dong.id);
                var label = other == null ? (chosen ? "✓ " : "+ ") + dong.name : dong.name + " · 다른 권역";
                if (!ZoneButton(cell, label, chosen ? Rim : Tile, other != null ? Soft : chosen ? Mint : Paper) ||
                    other != null) continue;

                if (chosen) zoneMembers.Remove(dong.id);
                else zoneMembers.Add(dong.id);
                confirmZoneRemoval = false;
                zoneMessage = null;
            }
        }

        private void SaveZone()
        {
            var name = zoneName.Trim();
            if (name.Length == 0 || zoneMembers.Count == 0)
            {
                zoneMessage = "권역 이름과 법정동을 한 곳 이상 선택하세요.";
                return;
            }

            var zones = zoneBook.zones.ToList();
            if (zones.Where((_, index) => index != zoneIndex).Any(zone =>
                    string.Equals(zone.name, name, StringComparison.OrdinalIgnoreCase)))
            {
                zoneMessage = "같은 이름의 배달권이 있습니다.";
                return;
            }

            var index = zoneIndex < 0 ? zones.Count : zoneIndex;
            var zone = new DeliveryZone
            {
                id = zoneIndex < 0 ? Guid.NewGuid().ToString("N") : zones[index].id,
                name = name,
                memberDongIds = zoneMembers.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
            };
            if (zoneIndex < 0) zones.Add(zone);
            else zones[index] = zone;
            if (!PersistZones(zones.ToArray())) return;
            EditZone(index);
            zoneMessage = "배달권을 저장했습니다.";
        }

        private void RemoveZone()
        {
            if (!confirmZoneRemoval)
            {
                confirmZoneRemoval = true;
                zoneMessage = "삭제 확인을 누르면 이 배달권을 지웁니다.";
                return;
            }

            var remaining = zoneBook.zones.Where((_, index) => index != zoneIndex).ToArray();
            if (!PersistZones(remaining)) return;
            EditZone(remaining.Length == 0 ? -1 : Mathf.Min(zoneIndex, remaining.Length - 1));
            zoneMessage = "배달권을 지웠습니다.";
        }

        private bool PersistZones(DeliveryZone[] zones)
        {
            var book = new DeliveryZoneBook { zones = zones };
            var next = JsonUtility.ToJson(book, true);
            var temporary = ZonePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                Directory.CreateDirectory(Application.persistentDataPath);
                using var guard = new FileStream(ZonePath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                if (!string.Equals(ReadZoneFile(), zoneDiskText, StringComparison.Ordinal))
                {
                    zoneMessage = "저장한 구성이 바뀌었습니다. 설정을 다시 열어 주세요.";
                    return false;
                }

                File.WriteAllText(temporary, next);
                if (zoneDiskText == null) File.Move(temporary, ZonePath);
                else File.Replace(temporary, ZonePath, null);
                zoneDiskText = next;
                zoneBook = book;
                return true;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                zoneMessage = "배달권을 저장할 수 없습니다. 구성을 다시 확인하세요.";
                return false;
            }
            finally
            {
                try { File.Delete(temporary); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
            }
        }

        private bool ZoneButton(Rect rect, string title, Color background, Color foreground)
        {
            Paint(rect, background, 6);
            Text(rect, title, 12, foreground, true, TextAnchor.MiddleCenter);
            return Hit(rect);
        }
    }
}
