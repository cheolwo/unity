using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Ssalddel.WindowsGame
{
    // 음악에 맞춘 빛 (2026-11-14, 사용자: mp3 · wav를 넘기면 진동수 · 진폭을 분석해 빛을 동기화하고 싶다 → 「설정 칸에서 파일 고르기」).
    //  - 설정 화면(ESC, SagajeongDispatchSettings.cs)의 「음악 · 소리」 탭에서 곡을 넣는다: 게임 창에 mp3 · wav를 끌어다 놓거나(음악끌어놓기)
    //    「곡 넣기」로 Windows 파일 창에서 고른다(2026-11-14 같은 날 사용자: ESC → 설정 화면 → 음악 → 파일을 넣으면 분석해 박자에 맞춰 빛이 움직이게).
    //    넣은 곡은 게임이 틀고, 곡 전체를 한 번 미리 분석한다(음악분석Policy).
    //  - 박이 분명한 곡: 빛 한 점이 박에 맞춰 퍼졌다 움츠러든다. 기사는 두 박에 한 번, 음식점 · 배송지는 네 박에 한 번이고,
    //    빛마다 정해진 엇갈림(LightPhase)으로 몇 번째 박에 퍼질지가 나뉜다(이웃한 빛이 번갈아 퍼진다). 저음이 셀수록 크게 퍼진다.
    //    땅의 빛은 자리마다의 숨을 멈추고 저음에 맞춰 함께 밝아진다(r210의 첫 후보: 같은 대상의 지붕 빛과 땅의 빛이 어긋나지 않게).
    //  - 박이 흐린 곡(잔잔한 곡, 박자가 바뀌는 곡): 원래의 숨을 줄이고 곡의 세기에 맞춰 함께 퍼진다.
    //  - 주문이 기다리는 매장의 금빛 깜빡임은 음악과 상관없이 그대로다(알림이다).
    //  - 원음은 저장 · 전송하지 않는다. 분석이 끝나면 곡 지도만 남는다. 기억하는 것은 마지막으로 넣은 곡의 경로와 소리 크기뿐이다(이 PC의 PlayerPrefs).
    //  - 표현만 바뀐다. 시간 · 돈 · 주문은 그대로다. 검증 촬영(--sagajeong-…)과 방향 촬영에서는 곡을 기억하지도 불러오지도 않는다.
    public sealed partial class SagajeongDispatchView
    {
        private const string MusicPathKey = "hongdal.music.path";
        private const string MusicVolumeKey = "hongdal.music.volume";
        private const float MusicVolumeDefault = 0.7f;
        private const int MusicChunk = 65536;             // GetData로 한 번에 읽는 표본 수(채널마다)
        private static readonly int ActivityPulseId = Shader.PropertyToID("_ActivityPulse");

        private AudioSource musicSource;
        private AudioClip musicClip;
        private 음악지도 musicMap;
        private Task<음악지도> musicAnalysis;
        private System.Diagnostics.Stopwatch musicWatch;
        private long musicAnalysisMs;
        private string musicPath;                         // 고른 곡 (없으면 null)
        private string musicError;                        // 설정 칸에 보일 오류 (없으면 null)
        private bool musicLoading;
        private bool musicWantPlay;
        private bool musicPaused;
        private bool musicPickRequested;
        private bool musicRestoreTried;
        private bool musicProofQuiet;                     // 방향 촬영에서만: 넣은 곡을 틀지 않는다(소리를 내지 않는다)
        private float musicVolume = MusicVolumeDefault;
        private int musicSerial;
        private double? musicProofTime;                   // 방향 촬영에서만: 곡의 이 시각(초)으로 빛을 맞춘다
        private double musicBeatNow;                      // 지금 곡의 박 위치 (첫 박 = 0)
        private float musicBass;                          // 지금의 저음 (0~1)
        private float musicLevel;                         // 지금의 세기 (0~1)

        private bool MusicPlaying => musicSource != null && musicSource.isPlaying;
        private bool MusicDrivesLight => musicMap != null && musicClip != null && (MusicPlaying || musicProofTime != null);
        private bool MusicOnBeat => MusicDrivesLight && musicMap.HasBeat;

        private static bool MusicRemembers => TidyScreen && !Array.Exists(Environment.GetCommandLineArgs(),
            value => value.StartsWith(ViewProofFlag, StringComparison.Ordinal));

        // 매 프레임 (LateUpdate의 처음). 지난 곡 되찾기, 파일 창, 분석의 끝, 지금의 박과 저음.
        private void SyncMusic()
        {
            if (!musicRestoreTried)
            {
                musicRestoreTried = true;
                RestoreMusic();
                if (TidyScreen) 음악끌어놓기.Install();          // 게임 창에 파일을 끌어다 놓을 수 있게 한다
            }
            TakeDroppedMusic();
            if (musicPickRequested)
            {
                musicPickRequested = false;
                PickMusic();
            }
            if (musicAnalysis != null && musicAnalysis.IsCompleted)
            {
                var finished = musicAnalysis;
                musicAnalysis = null;           // 분석에 쓴 한 줄 표본은 여기서 놓는다
                musicAnalysisMs = musicWatch?.ElapsedMilliseconds ?? 0;
                if (finished.IsFaulted)
                {
                    musicError = "곡을 분석하지 못했습니다: " + finished.Exception.GetBaseException().Message;
                    musicMap = null;
                }
                else
                {
                    musicMap = finished.Result;
                    Debug.Log($"Hongdal music: {musicMap.Bpm:0.0} BPM, first beat {musicMap.FirstBeat:0.000} s, " +
                        $"confidence {musicMap.Confidence:0.00}, {musicMap.Duration:0.0} s, {musicAnalysisMs} ms");
                }
            }
            UpdateMusicNow();
        }

        private void UpdateMusicNow()
        {
            if (!MusicDrivesLight)
            {
                musicBass = 0f;
                musicLevel = 0f;
                return;
            }
            var seconds = musicProofTime ?? (musicClip.frequency <= 0 ? 0 : (double)musicSource.timeSamples / musicClip.frequency);
            musicBeatNow = musicMap.BeatAt(seconds);
            musicBass = musicMap.At(musicMap.Low, seconds);
            musicLevel = musicMap.At(musicMap.Level, seconds);
        }

        // 빛의 숨을 그 프레임의 음악에 맞춘다 (SyncRoamers, 셰이더의 시계를 놓은 바로 뒤).
        private void SyncMusicLight()
        {
            if (!MusicDrivesLight)
            {
                Shader.SetGlobalFloat(ActivityPulseId, 0f);
                return;
            }
            if (musicMap.HasBeat) Shader.SetGlobalFloat(ActivityBeatId, 0f);      // 땅은 자리마다의 숨을 멈춘다
            Shader.SetGlobalFloat(ActivityPulseId, 0.3f * (musicMap.HasBeat ? musicBass : musicLevel));
        }

        // 음악이 빛을 이끄는 동안의 숨(-1~1). LightBeat와 같은 자리에 쓴다.
        private float MusicLightBeat(float x, float z, float rate, float lag, float phase)
        {
            if (!musicMap.HasBeat)
            {
                var calm = Mathf.Sin(lightBeat * rate + (x * 0.02f + z * 0.014f) * lag + phase);
                return Mathf.Clamp(0.35f * calm + 0.65f * (2f * musicLevel - 1f), -1f, 1f);
            }
            var slots = rate >= 4f ? 2 : 4;              // 기사는 두 박에 한 번, 음식점 · 배송지는 네 박에 한 번
            var pulse = 음악분석Policy.Pulse(musicBeatNow, 음악분석Policy.Slot(phase, slots), slots);
            return Mathf.Clamp(2f * pulse * (0.75f + 0.5f * musicBass) - 1f, -1f, 1f);
        }

        // 설정 칸 「음악」 줄의 두 번째 단추.
        private void ToggleMusic()
        {
            if (musicPath == null)
            {
                musicPickRequested = true;
                return;
            }
            if (musicClip == null)
            {
                if (musicLoading) musicWantPlay = !musicWantPlay;
                else StartMusic(musicPath, true);
                return;
            }
            EnsureMusicSource();
            if (MusicPlaying)
            {
                musicSource.Pause();
                musicPaused = true;
            }
            else if (musicPaused)
            {
                musicSource.UnPause();
                musicPaused = false;
            }
            else
            {
                musicSource.clip = musicClip;
                musicSource.Play();
            }
        }

        // 설정 화면의 한 줄: 지금의 상태.
        private string MusicState()
        {
            if (musicError != null) return musicError;
            if (musicPath == null) return "곡을 넣으면 빛이 그 곡의 박자에 맞춰 퍼집니다.";
            if (musicLoading) return "곡을 읽는 중";
            if (musicAnalysis != null) return "박을 찾는 중";
            if (musicMap == null) return "재생을 누르면 나옵니다";
            if (musicMap.HasBeat) return musicMap.Bpm.ToString("0") + " BPM · 박에 맞춤" + (MusicPlaying ? " · 재생 중" : "");
            return "박이 흐려 세기에 맞춤" + (MusicPlaying ? " · 재생 중" : "");
        }

        // 게임 창에 끌어다 놓은 파일: 첫 mp3 · wav를 넣고, 설정 화면의 음악 탭을 열어 보인다.
        private void TakeDroppedMusic()
        {
            var dropped = 음악끌어놓기.TakeDropped();
            if (dropped == null) return;
            string chosen = null;
            foreach (var path in dropped)
            {
                if (MusicTypeOf(path) == AudioType.UNKNOWN) continue;
                chosen = path;
                break;
            }
            settingsOpen = true;
            settingsTab = SettingsTabMusic;
            if (chosen == null)
            {
                musicError = "mp3 · wav 파일만 넣을 수 있습니다.";
                return;
            }
            StartMusic(chosen, !musicProofQuiet);
            RememberMusic(chosen);
        }

        private void RememberMusic(string path)
        {
            if (!MusicRemembers) return;
            PlayerPrefs.SetString(MusicPathKey, path);
            PlayerPrefs.Save();
        }

        private void SetMusicVolume(float volume)
        {
            musicVolume = Mathf.Clamp01(volume);
            if (musicSource != null) musicSource.volume = musicVolume;
            if (MusicRemembers) PlayerPrefs.SetFloat(MusicVolumeKey, musicVolume);      // 저장은 끝날 때 Unity가 한다
        }

        private void RestoreMusic()
        {
            if (!MusicRemembers) return;
            musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumeKey, MusicVolumeDefault));
            var path = PlayerPrefs.GetString(MusicPathKey, string.Empty);
            if (string.IsNullOrEmpty(path)) return;
            if (!File.Exists(path))
            {
                musicError = "지난번 곡을 찾지 못했습니다. 다시 넣어 주세요.";
                return;
            }
            musicPath = path;            // 불러오기는 재생을 누를 때 한다
        }

        private void PickMusic()
        {
            string path;
            try
            {
                path = 음악파일창.Open(musicPath);
            }
            catch (Exception exception)
            {
                musicError = "파일 창을 열지 못했습니다: " + exception.Message;
                return;
            }
            if (string.IsNullOrEmpty(path)) return;          // 취소
            StartMusic(path, true);
            RememberMusic(path);
        }

        private void EnsureMusicSource()
        {
            if (musicSource != null) return;
            if (GetComponent<AudioListener>() == null && FindAnyObjectByType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;
            musicSource.spatialBlend = 0f;
            musicSource.volume = musicVolume;
        }

        private void StopMusic()
        {
            musicSerial++;
            musicProofTime = null;
            if (musicSource != null)
            {
                musicSource.Stop();
                musicSource.clip = null;
            }
            if (musicClip != null) Destroy(musicClip);
            musicClip = null;
            musicMap = null;
            musicAnalysis = null;
            musicLoading = false;
            musicPaused = false;
            Shader.SetGlobalFloat(ActivityPulseId, 0f);
        }

        private void StartMusic(string path, bool play)
        {
            StopMusic();
            musicPath = path;
            musicError = null;
            musicWantPlay = play;
            musicLoading = true;
            StartCoroutine(LoadMusic(path, musicSerial));
        }

        private static AudioType MusicTypeOf(string path)
        {
            var extension = Path.GetExtension(path)?.ToLowerInvariant();
            return extension switch
            {
                ".mp3" => AudioType.MPEG,
                ".wav" => AudioType.WAV,
                _ => AudioType.UNKNOWN,
            };
        }

        // 곡을 읽어 틀 준비를 하고, 한 줄(모노) 표본을 뽑아 분석을 뒤에서 돌린다.
        private IEnumerator LoadMusic(string path, int serial)
        {
            var type = MusicTypeOf(path);
            if (type == AudioType.UNKNOWN || !File.Exists(path))
            {
                FailMusic(serial, type == AudioType.UNKNOWN ? "mp3 · wav 곡만 읽습니다." : "곡 파일이 없습니다.");
                yield break;
            }
            AudioClip clip = null;
            string problem = null;
            using (var request = UnityWebRequestMultimedia.GetAudioClip(new Uri(Path.GetFullPath(path)).AbsoluteUri, type))
            {
                if (request.downloadHandler is DownloadHandlerAudioClip handler)
                {
                    handler.streamAudio = false;
                    handler.compressed = false;          // 표본을 읽으려면 풀어 둔 채여야 한다
                }
                yield return request.SendWebRequest();
                if (serial != musicSerial) yield break;
                if (request.result != UnityWebRequest.Result.Success) problem = request.error;
                else
                {
                    try
                    {
                        clip = DownloadHandlerAudioClip.GetContent(request);
                    }
                    catch (Exception exception)
                    {
                        problem = exception.Message;
                    }
                }
            }
            if (clip == null)
            {
                FailMusic(serial, "곡을 읽지 못했습니다" + (string.IsNullOrEmpty(problem) ? "." : ": " + problem));
                yield break;
            }
            if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
            var deadline = Time.realtimeSinceStartupAsDouble + 30;
            while (clip.loadState == AudioDataLoadState.Loading && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            if (serial != musicSerial)
            {
                Destroy(clip);
                yield break;
            }
            if (clip.loadState != AudioDataLoadState.Loaded || clip.samples <= 0 || clip.channels <= 0)
            {
                Destroy(clip);
                FailMusic(serial, "곡을 풀지 못했습니다.");
                yield break;
            }

            // 채널을 섞어 한 줄로, 30 kHz보다 높으면 반으로 줄인다. 큰 곡은 몇 번에 나누어 읽는다.
            var channels = clip.channels;
            var total = clip.samples;
            var halve = clip.frequency > 음악분석Policy.MaxAnalysisRate;
            var mono = new float[halve ? total / 2 : total];
            var buffer = new float[MusicChunk * channels];
            var held = 0f;
            var chunks = 0;
            for (var start = 0; start < total; start += MusicChunk)
            {
                var count = Math.Min(MusicChunk, total - start);
                if (count < MusicChunk) buffer = new float[count * channels];
                if (!clip.GetData(buffer, start))
                {
                    Destroy(clip);
                    FailMusic(serial, "곡의 표본을 읽지 못했습니다.");
                    yield break;
                }
                for (var i = 0; i < count; i++)
                {
                    var sum = 0f;
                    for (var c = 0; c < channels; c++) sum += buffer[i * channels + c];
                    var value = sum / channels;
                    var at = start + i;
                    if (!halve) mono[at] = value;
                    else if ((at & 1) == 0) held = value;
                    else if ((at >> 1) < mono.Length) mono[at >> 1] = 0.5f * (held + value);
                }
                if (++chunks % 16 == 0)
                {
                    yield return null;
                    if (serial != musicSerial)
                    {
                        Destroy(clip);
                        yield break;
                    }
                }
            }
            var rate = halve ? clip.frequency / 2 : clip.frequency;
            musicWatch = System.Diagnostics.Stopwatch.StartNew();
            musicAnalysis = Task.Run(() => 음악분석Policy.Analyse(mono, rate));
            musicClip = clip;
            musicLoading = false;
            EnsureMusicSource();
            musicSource.clip = clip;
            if (musicWantPlay) musicSource.Play();
            Debug.Log($"Hongdal music: loaded {Path.GetFileName(path)} ({clip.frequency} Hz, {channels} ch, {clip.length:0.0} s)");
        }

        private void FailMusic(int serial, string message)
        {
            if (serial != musicSerial) return;
            musicLoading = false;
            musicError = message;
            Debug.LogWarning("Hongdal music: " + message);
        }

        // 방향 촬영: 촬영 폴더 옆 music-proof의 시험 곡(120 BPM, 첫 박 0.5초, 직접 만든 소리) 두 벌을 같은 길로 읽어 분석하고,
        // 박에 맞춰 퍼지는 빛을 곡의 시각을 정해 가며 찍는다. 소리는 내지 않는다. 곡을 기억하지 않는다. 시계와 원장은 그대로여야 한다.
        private IEnumerator ViewProofMusic(string directory, ViewProofReceipt receipt)
        {
            holdProofClock = true;
            musicProofQuiet = true;
            actorFocus = null;
            selectedBuilding = null;
            lightMenu = null;
            dongDeskOpen = false;
            settingsOpen = false;
            var elapsedBefore = engine.Kernel.ElapsedMs;
            var ledgerBefore = engine.Kernel.Ledger.Count;
            var rememberedBefore = PlayerPrefs.GetString(MusicPathKey, string.Empty);
            var parent = Path.GetDirectoryName(Path.GetFullPath(directory).TrimEnd('\\', '/')) ?? ".";
            var folder = Path.Combine(parent, "music-proof");
            var mp3 = Path.Combine(folder, "proof-120.mp3");
            var wav = Path.Combine(folder, "proof-120.wav");
            receipt.musicFound = File.Exists(mp3) && File.Exists(wav);
            if (receipt.musicFound)
            {
                StartMusic(mp3, false);
                yield return Until(() => !musicLoading && musicAnalysis == null, Time.realtimeSinceStartupAsDouble + 20);
                receipt.musicMp3Loaded = musicClip != null;
                if (musicMap != null)
                {
                    receipt.musicMp3Bpm = (float)musicMap.Bpm;
                    receipt.musicMp3FirstBeat = (float)musicMap.FirstBeat;
                    receipt.musicMp3Confidence = (float)musicMap.Confidence;
                    receipt.musicMp3Ok = musicMap.HasBeat && Math.Abs(musicMap.Bpm - 120) < 1.5 && ProofBeatError(musicMap, 0.5) < 0.06;
                }
                if (musicError != null) receipt.musicMp3Error = musicError;

                StartMusic(wav, false);
                yield return Until(() => !musicLoading && musicAnalysis == null, Time.realtimeSinceStartupAsDouble + 20);
                if (musicMap != null)
                {
                    receipt.musicWavBpm = (float)musicMap.Bpm;
                    receipt.musicWavFirstBeat = (float)musicMap.FirstBeat;
                    receipt.musicWavConfidence = (float)musicMap.Confidence;
                    receipt.musicWavSeconds = (float)musicMap.Duration;
                    receipt.musicWavAnalysisMs = musicAnalysisMs;
                    receipt.musicWavOk = musicMap.HasBeat && Math.Abs(musicMap.Bpm - 120) < 1.5 && ProofBeatError(musicMap, 0.5) < 0.04;
                }
                if (musicError != null) receipt.musicWavError = musicError;
            }

            if (receipt.musicWavOk)
            {
                var period = 60.0 / musicMap.Bpm;
                // 박에 맞는 순간과 한 박 뒤: 기사(두 박에 한 번)의 빛 한 점이 퍼졌다 움츠러드는가.
                musicProofTime = musicMap.FirstBeat + 8 * period;
                UpdateMusicNow();
                receipt.musicPulsePeak = MusicLightBeat(0f, 0f, RiderBeat, RiderLag, 0f);
                receipt.musicBassAtBeat = musicBass;
                musicProofTime = musicMap.FirstBeat + 9 * period;
                UpdateMusicNow();
                receipt.musicPulseRest = MusicLightBeat(0f, 0f, RiderBeat, RiderLag, 0f);
                // 장면: 음식점 빛이 몰린 곳(빛이 몰렸을 때의 장면과 같은 자리)에 가상 자리를 촬영에서만 더 켜고 기사 빛도 더 세운다.
                // 고용 · 제휴가 아니다. 네 박을 열여섯 장으로 찍는다(한 장에 1/4박, 120 BPM에서 0.125초).
                var extra = ViewProofMusicScene();
                receipt.musicSceneStores = extra.Count;
                receipt.musicSceneRiders = roamers.Count;
                for (var shot = 0; shot < 16; shot++)
                {
                    musicProofTime = musicMap.FirstBeat + (8 + shot / 4.0) * period;
                    yield return null;
                    yield return null;
                    yield return SaveFrame(directory, "49-music-t" + shot.ToString("00") + ".png");
                }
                managedPartnerSites.ExceptWith(extra);
                managedStamp = int.MinValue;          // 다음 프레임에 관리 건물을 다시 맞춘다
                roamProofExtra = 0;
                SyncRoamers();
                roamLightDue = 30f;                   // 촬영에서 켠 땅의 빛을 식힌다
                StepRoamLight();
                ShowRoamLight();
                musicProofTime = musicMap.FirstBeat + 8 * period;
                // 설정 화면이 떠 있는 동안 게임 시간이 멈추고, 닫으면 전의 속도로 이어지는가.
                receipt.settingsClockSpeedBefore = campaignMode && running ? campaignSpeed : 0;
                settingsOpen = true;
                settingsTab = SettingsTabMusic;
                yield return null;
                yield return null;
                receipt.settingsClockPaused = campaignSpeed == 0 && settingsHeldSpeed == receipt.settingsClockSpeedBefore;
                yield return SaveFrame(directory, "50-music-settings.png");
                settingsOpen = false;
                yield return null;
                yield return null;
                receipt.settingsClockResumed = campaignSpeed == receipt.settingsClockSpeedBefore && settingsHeldSpeed == 0;

                // ESC: 게임 창에 ESC 키를 보내 설정 화면이 열리고 닫히는지 본다(창이 받는 키 메시지를 그대로 보낸다).
                receipt.settingsEscReady = 음악끌어놓기.Ready;
                if (음악끌어놓기.PostTestEscape())
                {
                    yield return Until(() => settingsOpen, Time.realtimeSinceStartupAsDouble + 2);
                    receipt.settingsEscOpened = settingsOpen;
                    yield return new WaitForSecondsRealtime(0.2f);
                    음악끌어놓기.PostTestEscape();
                    yield return Until(() => !settingsOpen, Time.realtimeSinceStartupAsDouble + 2);
                    receipt.settingsEscClosed = receipt.settingsEscOpened && !settingsOpen;
                }
                settingsOpen = false;

                // 끌어다 놓기: 탐색기에서 끌어다 놓았을 때와 같은 메시지(WM_DROPFILES)를 게임 창에 보내 wav가 들어가 분석되는지 본다.
                StopMusic();
                musicPath = null;
                receipt.musicDropReady = 음악끌어놓기.Ready;
                if (음악끌어놓기.PostTestDrop(wav))
                {
                    yield return Until(() => musicPath != null && !musicLoading && musicAnalysis == null, Time.realtimeSinceStartupAsDouble + 15);
                    receipt.musicDropOk = musicPath != null && string.Equals(Path.GetFullPath(musicPath), Path.GetFullPath(wav), StringComparison.OrdinalIgnoreCase) &&
                        musicMap != null && musicMap.HasBeat && Math.Abs(musicMap.Bpm - 120) < 1.5 && settingsOpen && settingsTab == SettingsTabMusic;
                    if (musicMap != null) musicProofTime = musicMap.FirstBeat + 8 * period;
                    yield return null;
                    yield return null;
                    yield return SaveFrame(directory, "51-music-drop.png");
                }
                settingsOpen = false;
            }

            StopMusic();
            musicPath = null;
            musicError = null;
            musicProofQuiet = false;
            yield return null;
            receipt.musicUnchanged = engine.Kernel.ElapsedMs == elapsedBefore && engine.Kernel.Ledger.Count == ledgerBefore &&
                PlayerPrefs.GetString(MusicPathKey, string.Empty) == rememberedBefore;
            holdProofClock = false;
        }

        // 촬영에서만 켜는 장면. 고른 동에서 이미 켠 음식점 자리들의 가운데에 가까운 후보 40곳과, 동마다 기사 빛 다섯을 더 세운다.
        private List<int> ViewProofMusicScene()
        {
            var extra = new List<int>();
            var region = BusinessDong()?.Entry.regionId;
            if (!string.IsNullOrEmpty(region) && managedCandidates.TryGetValue(region, out var candidates) && candidates.Count > 0)
            {
                var centre = Vector2.zero;
                var mine = 0;
                foreach (var building in managedPartnerSites)
                {
                    if (building < 0 || building >= buildingShapes.Length || buildingShapes[building].Record.region != region) continue;
                    centre += BuildingMid(building);
                    mine++;
                }
                centre = mine > 0 ? centre / mine : BuildingMid(candidates[0]);
                var near = new List<int>();
                foreach (var building in candidates)
                    if (building >= 0 && building < buildingShapes.Length && !managedPartnerSites.Contains(building) && !roleByBuilding.ContainsKey(building))
                        near.Add(building);
                near.Sort((m, n) => (BuildingMid(m) - centre).sqrMagnitude.CompareTo((BuildingMid(n) - centre).sqrMagnitude));
                for (var i = 0; i < near.Count && extra.Count < 40; i++) extra.Add(near[i]);
                managedPartnerSites.UnionWith(extra);
                ViewProofCentre(buildingIndex.origin.latitude + centre.y / buildingIndex.metresPerDegreeLatitude,
                    buildingIndex.origin.longitude + centre.x / buildingIndex.metresPerDegreeLongitude, mapZoom);
            }
            roamProofExtra = 5;
            SyncRoamers();
            for (var step = 0; step < 120; step++)
            {
                StepRoamers(0.25f);
                StepRoamLight();
            }
            ShowRoamLight();
            return extra;
        }

        private static double ProofBeatError(음악지도 map, double first)
        {
            if (map.Bpm <= 0) return 99;
            var period = 60 / map.Bpm;
            var gap = (map.FirstBeat - first) % period;
            if (gap < 0) gap += period;
            return Math.Min(gap, period - gap);
        }
    }

    // Windows의 파일 열기 창 (comdlg32). 창이 닫힐 때까지 기다린다. 취소하면 null.
    internal static class 음악파일창
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        [StructLayout(LayoutKind.Sequential)]
        private struct OpenFileName
        {
            public int lStructSize;
            public IntPtr hwndOwner;
            public IntPtr hInstance;
            public IntPtr lpstrFilter;
            public IntPtr lpstrCustomFilter;
            public int nMaxCustFilter;
            public int nFilterIndex;
            public IntPtr lpstrFile;
            public int nMaxFile;
            public IntPtr lpstrFileTitle;
            public int nMaxFileTitle;
            public IntPtr lpstrInitialDir;
            public IntPtr lpstrTitle;
            public int Flags;
            public short nFileOffset;
            public short nFileExtension;
            public IntPtr lpstrDefExt;
            public IntPtr lCustData;
            public IntPtr lpfnHook;
            public IntPtr lpTemplateName;
            public IntPtr pvReserved;
            public int dwReserved;
            public int FlagsEx;
        }

        [DllImport("comdlg32.dll", EntryPoint = "GetOpenFileNameW", ExactSpelling = true, SetLastError = true)]
        private static extern bool GetOpenFileNameW(ref OpenFileName dialog);

        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern IntPtr GetActiveWindow();

        private const int Explorer = 0x80000, FileMustExist = 0x1000, PathMustExist = 0x800, NoChangeDir = 0x8, HideReadOnly = 0x4;
        private const int FileChars = 2048;

        internal static string Open(string previous)
        {
            var filter = Marshal.StringToHGlobalUni("음악 파일 (*.mp3, *.wav)\0*.mp3;*.wav\0모든 파일 (*.*)\0*.*\0\0");
            var title = Marshal.StringToHGlobalUni("곡 고르기");
            var folder = IntPtr.Zero;
            var file = Marshal.AllocHGlobal(FileChars * 2);
            try
            {
                Marshal.WriteInt16(file, 0);
                var start = string.IsNullOrEmpty(previous) ? null : Path.GetDirectoryName(previous);
                if (!string.IsNullOrEmpty(start) && Directory.Exists(start)) folder = Marshal.StringToHGlobalUni(start);
                var dialog = new OpenFileName
                {
                    lStructSize = Marshal.SizeOf<OpenFileName>(),
                    hwndOwner = GetActiveWindow(),
                    lpstrFilter = filter,
                    nFilterIndex = 1,
                    lpstrFile = file,
                    nMaxFile = FileChars,
                    lpstrInitialDir = folder,
                    lpstrTitle = title,
                    Flags = Explorer | FileMustExist | PathMustExist | NoChangeDir | HideReadOnly,
                };
                return GetOpenFileNameW(ref dialog) ? Marshal.PtrToStringUni(file) : null;
            }
            finally
            {
                Marshal.FreeHGlobal(filter);
                Marshal.FreeHGlobal(title);
                Marshal.FreeHGlobal(file);
                if (folder != IntPtr.Zero) Marshal.FreeHGlobal(folder);
            }
        }
#else
        internal static string Open(string previous) => throw new PlatformNotSupportedException("Windows에서만 열 수 있습니다");
#endif
    }
}
