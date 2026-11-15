using System;
using System.IO;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 2026-11-14 첫 화면 정리(1단계). 사용자 방향: 처음에는 적게 보이고, 플레이하면서 접하는 정보가 늘어난다.
    // 시작부터 옵션을 조정하는 화면을 보일 필요는 없다.
    //  - 옵션은 「설정」 하나로 모았다: 신호 소리, 음악(넣은 곡에 빛을 맞춘다, SagajeongDispatchMusic.cs), 지도 효과, 지도 모양(2.5D·원근·평면), 지도 방향(90°씩), 동 보기(붙여·띄워·이 동만·역 중심, 동 칩),
    //    지도 도구(법정동·배달권), 화면(기존 화면·지도만 보기), 회차(처음부터 새로 시작).
    //  - 설정은 지도 가운데의 큰 화면이다(2026-11-14 같은 날, 사용자: ESC를 누르면 설정에 들어가고 그 화면에 음악이 있게). ESC나 오른쪽 위 「설정」으로 열고
    //    ESC · ×로 닫는다. 왼쪽 탭: 음악 · 소리 / 화면 · 지도 / 회차. 열려 있는 동안 아래의 지도와 단추는 눌리지 않고, 고른 것과 열린 칸은 그대로 둔다.
    //    열려 있는 동안 게임 시간은 멈추고, 닫으면 전의 속도로 이어진다(같은 날, 사용자: 「시간이 멈추게」). 음악과 빛의 숨은 그대로 흐른다.
    //  - 왼쪽의 운영 현황(지금의 배달 운영 · 사업 층)은 접힌 채로 시작한다. 왼쪽 아래 단추로 편다.
    //    접어 둔 동안에도 방금 한 일의 결과와 오류는 단추 옆 한 줄에 보인다.
    //  - 개발 정보(모듈·건물 수)는 설정의 「회차」 탭 아래에만 적는다. 「내부 검토용」 표시는 머리글 아래 줄로 옮겼다.
    //  - 오른쪽의 결정 순회는 한 줄 버튼으로 시작하고(SagajeongDispatchCycle.cs), 사업 칸은 닫힌 채 시작하며,
    //    아래 사업 띠에는 열린 사업만 둔다(SagajeongDispatchExpansion.cs).
    // 규칙·시계·업무 상태는 바꾸지 않는다. 보이는 것만 줄였고, 뺀 것은 모두 설정 칸에서 그대로 쓸 수 있다.
    // 검증 촬영(--sagajeong-…)은 예전 첫 화면을 기준으로 하므로 그대로 둔다(TidyScreen = false).
    // 다음 단계: 무엇이 언제 나타나는지를 게임 상태에 묶는다(나타나는 조건을 규칙 파일 하나에).
    public sealed partial class SagajeongDispatchView
    {
        private static readonly bool TidyScreen = Array.TrueForAll(Environment.GetCommandLineArgs(),
            value => !value.StartsWith("--sagajeong-", StringComparison.Ordinal));

        private static readonly Rect SettingsScreen = new(250, 96, 780, 528);      // 설정 화면(지도 가운데)
        private static readonly Rect DailyReportPanel = new(986, 106, 272, 430);   // 「오늘 결산」 칸(오른쪽, 사업·영역·결정 카드와 같은 자리)
        private static readonly Rect SettingsButton = new(1178, 10, 80, 28);
        private static readonly Rect SideToggleButton = new(22, 626, 96, 26);
        private static readonly Rect SideNoteLine = new(226, 626, 354, 26);      // 「운영 현황」·「오늘 결산」 단추 오른쪽
        private const int SettingsTabMusic = 0, SettingsTabMap = 1, SettingsTabRound = 2;
        private static readonly string[] SettingsTabTitles = { "음악 · 소리", "화면 · 지도", "회차" };
        private const float ScreenLabelWidth = 112f;
        private const float ScreenRowWidth = 470f;
        private const float ScreenRow = 38f;

        private bool settingsOpen;
        private int settingsTab = SettingsTabMusic;
        private bool settingsKeyHeld;
        private float settingsKeyAt = -10f;
        private int settingsHeldSpeed;                    // 설정 화면을 여는 동안 멈춰 둔 진행 속도 (0이면 멈춰 두지 않았다)
        private bool sidePanelsOpen;
        private float restartArmedUntil;
        private string sideNoteSeen;
        private float sideNoteAt = -100f;

        // 설정 화면이 떠 있는가. 화면 모양(기존 화면 · 지도만 보기)과 상관없이 뜬다.
        private bool SettingsVisible => TidyScreen && settingsOpen;

        // 왼쪽의 운영 현황 두 칸(지금의 배달 운영 · 사업 층)이 보이는가.
        private bool SidePanelsVisible => !TidyScreen || sidePanelsOpen;

        // 저장을 불러온 뒤 아직 이어가지 않았을 때만 새 회차를 권한다.
        private bool RestartOffered => restoredSession && !running && pending == null && error == null;

        private bool SideNoteVisible => !sidePanelsOpen &&
            (error != null || !string.IsNullOrEmpty(notice) && Time.unscaledTime - sideNoteAt < SideNoteSeconds);

        private bool SideCovers(Vector2 point) =>
            (SidePanelsVisible || NationMapVisible) && ExpansionOverview.Contains(point) ||
            !NationMapVisible && (SideToggleButton.Contains(point) || DailyReportButton.Contains(point) ||
                RoundCardVisible && RoundCard.Contains(point) || SideNoteVisible && SideNoteLine.Contains(point));

        private void ToggleSettings()
        {
            settingsOpen = !settingsOpen;
            restartArmedUntil = 0f;
        }

        // 설정 화면이 떠 있는 동안 게임 시간을 멈추고, 닫으면 전의 속도로 돌린다. 전국 지도의 시계 정지(HoldNationClock)와 같은 길이다.
        // 이미 멈춰 있었으면(정지를 눌렀거나 전국 지도가 멈춰 둠) 손대지 않는다. 운영이 돌고 있을 때만이다. (OnGUI의 처음, ESC 다음)
        private void SyncSettingsClock()
        {
            if (SettingsVisible)
            {
                if (settingsHeldSpeed != 0 || !campaignMode || !running || error != null || CampaignPolicyOpen || campaignJumpPending ||
                    campaignSpeed == 0) return;
                var speed = campaignSpeed;
                SetCampaignSpeed(0);
                if (campaignSpeed == 0) settingsHeldSpeed = speed;
                return;
            }
            if (settingsHeldSpeed == 0) return;
            if (!running || campaignSpeed != 0)
            {
                settingsHeldSpeed = 0;           // 새 회차를 열었거나 다른 길로 이미 움직였다
                return;
            }
            SetCampaignSpeed(settingsHeldSpeed);
            if (campaignSpeed != 0) settingsHeldSpeed = 0;
        }

        // ESC: 설정 화면을 열고 닫는다. 누르고 있는 동안 되풀이되는 입력은 한 번으로 본다. (OnGUI의 처음)
        private void HandleSettingsKey()
        {
            var current = Event.current;
            if (!TidyScreen || current.keyCode != KeyCode.Escape) return;
            if (current.type == EventType.KeyUp)
            {
                settingsKeyHeld = false;
                return;
            }
            if (current.type != EventType.KeyDown) return;
            current.Use();
            if (CloseDongPickerByEscape() || ClosePlaceByEscape())       // 행정동 이동 창(r236) · 홍달 플레이스가 열려 있으면 닫기가 먼저다. 설정은 열지 않는다
            {
                settingsKeyHeld = true;
                settingsKeyAt = Time.unscaledTime;
                return;
            }
            var repeat = settingsKeyHeld && Time.unscaledTime - settingsKeyAt < 1f;
            settingsKeyHeld = true;
            settingsKeyAt = Time.unscaledTime;
            if (!repeat) ToggleSettings();
        }

        private void ShowLegacyView()
        {
            expansionView = false;
            showDesk = true;
            showTime = true;
        }

        private void SetDiorama(bool on)
        {
            if (dioramaOn == on) return;
            dioramaOn = on;
            mapPointerDown = false;
            mapDragging = false;
            mapClick = false;
            Debug.Log("Hongdal diorama: " + (dioramaOn ? "on" : "off"));
        }

        private void OpenZonePanel()
        {
            LoadLegalDongs();
            actorFocus = null;
            dongPanelOpen = false;
            zonePanelOpen = true;
            LoadZoneBook();
        }

        // 왼쪽 아래: 운영 현황을 펴고 접는 단추와, 접어 둔 동안의 한 줄 알림.
        private void DrawSideToggle()
        {
            if (NationMapVisible) return;       // 전국 지도에서는 왼쪽이 자료 층이다
            if (ExpansionButton(SideToggleButton, sidePanelsOpen ? "운영 현황 접기" : "운영 현황", true, Tile))
                sidePanelsOpen = !sidePanelsOpen;
            if (notice != sideNoteSeen)
            {
                sideNoteSeen = notice;
                sideNoteAt = Time.unscaledTime;
            }
            if (!SideNoteVisible) return;
            var noteAlpha = SideNoteAlpha;     // 알림을 쓰면 끝에서 흐려진다 (SagajeongDispatchNotices.cs)
            Paint(SideNoteLine, Fade(Canvas, 0.92f * noteAlpha), 7);
            Text(new Rect(SideNoteLine.x + 10, SideNoteLine.y, SideNoteLine.width - 20, SideNoteLine.height), error ?? notice, 11,
                error == null ? Fade(Paper, noteAlpha) : Red);
        }

        private void ScreenLabel(float x, float y, string label) =>
            Text(new Rect(x, y, ScreenLabelWidth, 28), label, 13, Soft);

        // 한 줄에서 이름표 오른쪽을 count칸으로 나눈 index번째 칸.
        private static Rect ScreenCell(float x, float y, int index, int count)
        {
            const float gap = 6f;
            var total = ScreenRowWidth - ScreenLabelWidth;
            var width = (total - gap * (count - 1)) / count;
            return new Rect(x + ScreenLabelWidth + index * (width + gap), y, width, 28);
        }

        private void ScreenHint(float x, float y, string text) =>
            Text(new Rect(x + ScreenLabelWidth, y, ScreenRowWidth - ScreenLabelWidth, 28), text, 11, Fade(Soft, 0.7f));

        // 설정 화면. 모든 것 위에 그린다(OnGUI의 끝). 아래의 단추는 그동안 눌리지 않는다(GUI.enabled, MapOverlayCovers).
        private void DrawSettingsScreen()
        {
            Paint(new Rect(0, 0, Width, Height), Fade(Canvas, 0.62f));
            var box = SettingsScreen;
            Paint(box, Rim, 14);
            Paint(new Rect(box.x + 1, box.y + 1, box.width - 2, box.height - 2), Canvas, 13);
            Text(new Rect(box.x + 24, box.y + 16, 200, 32), "설정", 22, Paper, true);
            if (settingsHeldSpeed != 0)
                Text(new Rect(box.x + 96, box.y + 18, 320, 28), $"게임 시간 멈춤 · 닫으면 {settingsHeldSpeed}배로 이어집니다", 12, Gold, true);
            Text(new Rect(box.xMax - 220, box.y + 18, 160, 28), "ESC로 닫기", 11, Fade(Soft, 0.8f), align: TextAnchor.MiddleRight);
            var close = new Rect(box.xMax - 48, box.y + 16, 30, 30);
            Text(close, "×", 22, Soft, align: TextAnchor.MiddleCenter);
            if (Hit(close))
            {
                settingsOpen = false;
                return;
            }
            for (var tab = 0; tab < SettingsTabTitles.Length; tab++)
            {
                var rect = new Rect(box.x + 16, box.y + 70 + tab * 46, 172, 40);
                var chosen = settingsTab == tab;
                if (chosen)
                {
                    Paint(rect, Tile, 8);
                    Paint(new Rect(rect.x, rect.y + 9, 3, rect.height - 18), Mint, 1);
                }
                Text(new Rect(rect.x + 16, rect.y, rect.width - 20, rect.height), SettingsTabTitles[tab], 14, chosen ? Paper : Soft, chosen);
                if (!Hit(rect)) continue;
                settingsTab = tab;
                restartArmedUntil = 0f;
            }
            Paint(new Rect(box.x + 204, box.y + 64, 1, box.height - 88), Rim);
            var x = box.x + 228;
            var y = box.y + 70;
            switch (settingsTab)
            {
                case SettingsTabMap:
                    DrawSettingsMapTab(x, y);
                    break;
                case SettingsTabRound:
                    DrawSettingsRoundTab(x, y, box);
                    break;
                default:
                    DrawSettingsMusicTab(x, y);
                    break;
            }
        }

        // 음악 · 소리: 곡 넣기(끌어다 놓기 · 파일 창), 지금의 곡과 분석, 재생 · 소리 크기, 지금의 박과 저음, 신호 소리.
        private void DrawSettingsMusicTab(float x, float y)
        {
            const float width = ScreenRowWidth + 54f;
            Text(new Rect(x, y, 200, 26), "음악", 16, Paper, true);
            y += 32;
            var drop = new Rect(x, y, width, 88);
            Paint(drop, Mint, 10);
            Paint(new Rect(drop.x + 1, drop.y + 1, drop.width - 2, drop.height - 2), Panel, 9);
            Text(new Rect(drop.x + 18, drop.y + 12, drop.width - 190, 28),
                음악끌어놓기.Ready ? "mp3 · wav 파일을 이 창에 끌어다 놓으세요" : "mp3 · wav 파일을 넣으세요", 14, Paper, true);
            Text(new Rect(drop.x + 18, drop.y + 42, drop.width - 190, 34),
                "곡 전체를 한 번 분석해 그 곡의 박자와 저음에 빛을 맞춥니다.", 11, Soft, wrap: true);
            if (ExpansionButton(new Rect(drop.xMax - 156, drop.y + 26, 138, 36), "곡 넣기", true, Tile)) musicPickRequested = true;
            y += 100;

            Text(new Rect(x, y, width, 26), musicPath == null ? "곡 없음" : Path.GetFileNameWithoutExtension(musicPath), 16,
                musicPath == null ? Fade(Soft, 0.7f) : Paper, true);
            Text(new Rect(x, y + 26, width, 22), MusicState(), 12, musicError == null ? Soft : Red);
            y += 58;

            if (ExpansionButton(new Rect(x, y, 112, 32), MusicPlaying ? "멈춤" : "재생", musicPath != null, Tile)) ToggleMusic();
            Text(new Rect(x + 132, y, 70, 32), "소리 크기", 12, Soft);
            var volume = VolumeSlider(new Rect(x + 206, y + 4, 230, 24), musicVolume);
            if (!Mathf.Approximately(volume, musicVolume)) SetMusicVolume(volume);
            Text(new Rect(x + 446, y, 60, 32), Mathf.RoundToInt(musicVolume * 100) + "%", 12, Paper);
            y += 46;

            // 지금 곡에서 읽은 박(네 박)과 저음. 분석이 맞는지 눈으로 본다.
            Text(new Rect(x, y, 40, 22), "박", 12, Soft);
            for (var beat = 0; beat < 4; beat++)
            {
                var glow = MusicOnBeat ? 음악분석Policy.Pulse(musicBeatNow, beat, 4) : 0f;
                var size = 10f + 6f * glow;
                var centre = new Vector2(x + 52 + beat * 26, y + 11);
                Paint(new Rect(centre.x - size * 0.5f, centre.y - size * 0.5f, size, size), Color.Lerp(Tile, Gold, glow), size * 0.5f);
            }
            Text(new Rect(x + 168, y, 40, 22), "저음", 12, Soft);
            Paint(new Rect(x + 210, y + 7, 160, 8), Tile, 4);
            if (MusicDrivesLight) Paint(new Rect(x + 210, y + 7, 160 * Mathf.Clamp01(musicBass), 8), Mint, 4);
            y += 34;
            Text(new Rect(x, y, width, 34), "곡은 이 PC 안에서만 분석합니다. 원음은 저장하지 않고, 마지막에 넣은 곡의 위치와 소리 크기만 기억합니다.",
                11, Fade(Soft, 0.8f), wrap: true);
            y += 46;

            Paint(new Rect(x, y, width, 1), Rim);
            y += 14;
            Text(new Rect(x, y, 200, 26), "소리", 16, Paper, true);
            y += 32;
            ScreenLabel(x, y, "신호 소리");
            if (EffectsSegment(ScreenCell(x, y, 0, 2), "켬", signalSound)) signalSound = true;
            if (EffectsSegment(ScreenCell(x, y, 1, 2), "끔", !signalSound)) signalSound = false;
        }

        // 소리 크기 막대(0~1). 누르거나 끌어서 바꾼다.
        private static float VolumeSlider(Rect track, float value)
        {
            var current = Event.current;
            var id = GUIUtility.GetControlID(FocusType.Passive);
            switch (current.GetTypeForControl(id))
            {
                case EventType.MouseDown when GUI.enabled && current.button == 0 && track.Contains(current.mousePosition):
                    GUIUtility.hotControl = id;
                    value = Mathf.Clamp01((current.mousePosition.x - track.x) / track.width);
                    current.Use();
                    break;
                case EventType.MouseDrag when GUIUtility.hotControl == id:
                    value = Mathf.Clamp01((current.mousePosition.x - track.x) / track.width);
                    current.Use();
                    break;
                case EventType.MouseUp when GUIUtility.hotControl == id:
                    GUIUtility.hotControl = 0;
                    current.Use();
                    break;
            }
            Paint(new Rect(track.x, track.center.y - 3, track.width, 6), Tile, 3);
            Paint(new Rect(track.x, track.center.y - 3, track.width * value, 6), Mint, 3);
            Paint(new Rect(track.x + track.width * value - 8, track.center.y - 8, 16, 16), Paper, 8);
            return value;
        }

        // 화면 · 지도: 지도 효과, 지도 모양, 지도 방향, 동 보기, 지도 도구, 화면.
        private void DrawSettingsMapTab(float x, float y)
        {
            ScreenLabel(x, y, "지도 효과");
            if (EffectsSegment(ScreenCell(x, y, 0, 2), "간단히", !mapEffects)) SetMapEffects(false);
            if (EffectsSegment(ScreenCell(x, y, 1, 2), "효과 사용", mapEffects)) SetMapEffects(true);
            y += ScreenRow;

            // 전국 지도에는 동네 도구가 없다.
            if (mapLevel != MapLevel.Nation)
            {
                ScreenLabel(x, y, "지도 모양");
                if (dioramaRoot != null && mapLevel == MapLevel.Dong)
                {
                    // 3D 기반 A6: 원근 보기. 기본은 지금까지의 평행 보기(2.5D)다.
                    if (EffectsSegment(ScreenCell(x, y, 0, 3), "2.5D", dioramaOn && !dioramaPerspective))
                    {
                        SetDiorama(true);
                        SetDioramaPerspective(false);
                    }
                    if (EffectsSegment(ScreenCell(x, y, 1, 3), "원근", dioramaOn && dioramaPerspective))
                    {
                        SetDiorama(true);
                        SetDioramaPerspective(true);
                    }
                    if (EffectsSegment(ScreenCell(x, y, 2, 3), "평면", !dioramaOn)) SetDiorama(false);
                }
                else
                    ScreenHint(x, y, "동네 지도에서 고릅니다");
                y += ScreenRow;

                // 3D 기반 A5: 원근 없이 지도를 90°씩 돌려 본다. 기본은 지금까지의 방향이다.
                ScreenLabel(x, y, "지도 방향");
                if (DioramaVisible)
                {
                    for (var turn = 0; turn < 4; turn++)
                    {
                        var yaw = (float)디오라마시점.DefaultYawDegrees + 90f * turn;
                        var chosen = Mathf.Abs(Mathf.DeltaAngle((float)dioramaLens.YawDegrees, yaw)) < 1f;
                        if (EffectsSegment(ScreenCell(x, y, turn, 4), turn == 0 ? "기본" : 90 * turn + "°", chosen))
                            SetDioramaLens(yaw, dioramaBasePitch);
                    }
                }
                else
                    ScreenHint(x, y, "2.5D 동네 지도에서 고릅니다");
                y += ScreenRow;

                // 품6: 길가 불빛. 자리 자료가 없는 표현이다(SagajeongDispatchStreetLamps.cs). 가까이 볼 때만 보인다.
                ScreenLabel(x, y, "길가 불빛(표현)");
                if (DioramaVisible)
                {
                    if (EffectsSegment(ScreenCell(x, y, 0, 2), "켜기 · 가까이에서만 보임", LampsOn)) SetLamps(true);
                    if (EffectsSegment(ScreenCell(x, y, 1, 2), "끄기", !LampsOn)) SetLamps(false);
                }
                else
                    ScreenHint(x, y, "2.5D 동네 지도에서 고릅니다");
                y += ScreenRow;

                ScreenLabel(x, y, "동 보기");
                if (DongViewAvailable)
                {
                    DrawDongViewControl(new Rect(x + ScreenLabelWidth - 4, y - 2, ScreenRowWidth - ScreenLabelWidth + 8, 68));
                    y += 74;
                }
                else
                {
                    ScreenHint(x, y, "2.5D 동네 지도에서 고릅니다");
                    y += ScreenRow;
                }

                ScreenLabel(x, y, "지도 도구");
                if (ExpansionButton(ScreenCell(x, y, 0, 2), "법정동", state != null, Tile))
                {
                    settingsOpen = false;
                    OpenDongPanel(null);
                    return;
                }
                if (ExpansionButton(ScreenCell(x, y, 1, 2), "배달권", state != null, Tile))
                {
                    settingsOpen = false;
                    OpenZonePanel();
                    return;
                }
                y += ScreenRow;
            }

            // 그2: 왼쪽 창의 모양. 사용자가 고르기 전이라 기본은 지금 창(글 중심)이다(SagajeongDispatchIcons.cs).
            ScreenLabel(x, y, "왼쪽 창");
            if (EffectsSegment(ScreenCell(x, y, 0, 2), "글 중심", !PictureDesk)) SetPictureDesk(false);
            if (EffectsSegment(ScreenCell(x, y, 1, 2), "그림 중심(시험)", PictureDesk)) SetPictureDesk(true);
            y += ScreenRow;

            ScreenLabel(x, y, "화면");
            if (ExpansionButton(ScreenCell(x, y, 0, 2), "기존 화면", true, Tile))
            {
                settingsOpen = false;
                ShowLegacyView();
                return;
            }
            if (ExpansionButton(ScreenCell(x, y, 1, 2), mapOnly ? "지도만 보기 끄기" : "지도만 보기", true, Tile))
            {
                settingsOpen = false;
                ToggleMapOnly();
            }
        }

        // 회차: 처음부터 새로 시작(두 번 눌러야 한다)과 개발 정보.
        private void DrawSettingsRoundTab(float x, float y, Rect box)
        {
            // 새 회차는 저장을 덮는다. 두 번 눌러야 한다.
            ScreenLabel(x, y, "회차");
            var restart = ScreenCell(x, y, 0, 1);
            var offered = RestartOffered;
            var armed = offered && Time.unscaledTime < restartArmedUntil;
            Paint(restart, !offered ? Panel : armed ? Gold : Tile, 6);
            Text(restart, armed ? "한 번 더 누르면 새로 시작" : "처음부터 새로 시작", 12,
                !offered ? Fade(Soft, 0.7f) : armed ? Canvas : Paper, true, TextAnchor.MiddleCenter);
            if (offered && Hit(restart))
            {
                if (armed)
                {
                    restartArmedUntil = 0f;
                    settingsOpen = false;
                    RequestStart(true);
                    return;
                }
                restartArmedUntil = Time.unscaledTime + 4f;
            }
            y += 34;
            Text(new Rect(x + ScreenLabelWidth, y, ScreenRowWidth - ScreenLabelWidth, 28),
                offered ? "저장한 회차를 새 회차로 덮습니다." : "저장을 불러온 직후에만 고를 수 있습니다.", 11, Fade(Soft, 0.8f), wrap: true);

            // 켜 두기 성장 · 맡김(뼈대). 끄면 맡김은 아무 명령도 보내지 않는다.
            y += 46;
            ScreenLabel(x, y, "맡겨두기");
            var autoOn = ScreenCell(x, y, 0, 2);
            var autoOff = ScreenCell(x, y, 1, 2);
            Paint(autoOn, autopilotOn ? Mint : Tile, 6);
            Text(autoOn, "켜기 · 손이 비면", 12, autopilotOn ? Canvas : Paper, true, TextAnchor.MiddleCenter);
            if (Hit(autoOn)) SetAutopilot(true);
            Paint(autoOff, autopilotOn ? Tile : Mint, 6);
            Text(autoOff, "끄기 · 모두 직접", 12, autopilotOn ? Paper : Canvas, true, TextAnchor.MiddleCenter);
            if (Hit(autoOff)) SetAutopilot(false);
            y += 34;
            Text(new Rect(x + ScreenLabelWidth, y, ScreenRowWidth - ScreenLabelWidth, 40),
                "손이 2분 비면 출발 전 점검 · 기다린 주문 배차 · 지난 방침 이어 가기를 맡김이 합니다(시험값). 정산 · 예외 · 확장은 하지 않습니다.",
                11, Fade(Soft, 0.8f), wrap: true);

            // 개발 정보. 첫 화면에는 두지 않는다.
            const float width = ScreenRowWidth + 54f;
            var foot = box.yMax - 74;
            Paint(new Rect(x, foot - 8, width, 1), Rim);
            if (DongModulesLoaded)
            {
                Text(new Rect(x, foot, width, 18), $"모듈 {DongModulesLoadedCount}/{dongModules.Count} · 건물 {dioramaBuildings:N0}동",
                    11, Fade(Soft, 0.8f));
                Text(new Rect(x, foot + 18, width, 30), dongManifest.label ?? "내부 검토용", 11, Fade(Soft, 0.8f), wrap: true);
            }
            else
                Text(new Rect(x, foot, width, 30), "가상 운영 · 실제 주문 및 결제와 연결되지 않습니다.", 11, Fade(Soft, 0.8f), wrap: true);
        }
    }
}
