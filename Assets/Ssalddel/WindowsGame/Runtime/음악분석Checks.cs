using System;
using System.Collections.Generic;

namespace Ssalddel.WindowsGame
{
    // 음악 분석(음악분석Policy)의 검사 (2026-11-14). 곡 파일을 읽지 않는다. 박을 아는 소리를 여기서 만들어 분석한다.
    internal static class 음악분석Checks
    {
        private const int Rate = 22050;

        internal static List<(string Name, bool Ok, string Detail)> Run()
        {
            var results = new List<(string Name, bool Ok, string Detail)>();
            void Check(string name, bool ok, string detail = "") => results.Add((name, ok, detail));

            // FFT: 64칸에 8번째 칸의 사인을 넣으면 8번째 칸이 가장 크고, 크기는 32다.
            var re = new double[64];
            var im = new double[64];
            for (var i = 0; i < 64; i++) re[i] = Math.Sin(2 * Math.PI * 8 * i / 64);
            음악분석Policy.Fft(re, im);
            var peak = 0;
            for (var k = 1; k < 32; k++) if (Magnitude(re, im, k) > Magnitude(re, im, peak)) peak = k;
            Check("FFT — 사인의 칸", peak == 8 && Math.Abs(Magnitude(re, im, 8) - 32) < 1e-6, "칸 " + peak + ", 크기 " + Magnitude(re, im, 8).ToString("0.###"));

            var click = Analyse(Clicks(120, 0.25, 12, Rate), Rate);
            Check("클릭 120 BPM — 박자", click.HasBeat && Math.Abs(click.Bpm - 120) < 1, Describe(click));
            Check("클릭 120 BPM — 첫 박 0.25초", BeatError(click, 0.25) < 0.035, "어긋남 " + BeatError(click, 0.25).ToString("0.000") + "초");

            var slow = Analyse(Clicks(90, 0.4, 14, Rate), Rate);
            Check("클릭 90 BPM", slow.HasBeat && Math.Abs(slow.Bpm - 90) < 1 && BeatError(slow, 0.4) < 0.035, Describe(slow));

            // 킥은 박마다, 열린 하이햇은 반 박마다(더 시끄럽다). 박은 킥에 놓여야 한다.
            var dance = Analyse(Dance(128, 0.3, 16, Rate, 7), Rate);
            Check("킥 · 하이햇 128 BPM — 박은 킥에", dance.HasBeat && Math.Abs(dance.Bpm - 128) < 1 && BeatError(dance, 0.3) < 0.035,
                Describe(dance) + ", 어긋남 " + BeatError(dance, 0.3).ToString("0.000") + "초");

            // 44.1 kHz는 반으로 줄여 분석해도 같은 박이다.
            var wide = Clicks(120, 0.25, 12, 44100);
            var halved = Analyse(음악분석Policy.Halve(wide), 22050);
            Check("44.1 kHz를 반으로 줄여도 같은 박", halved.HasBeat && Math.Abs(halved.Bpm - 120) < 1 && BeatError(halved, 0.25) < 0.035, Describe(halved));

            var silence = Analyse(new float[Rate * 6], Rate);
            Check("무음 — 박 없음", !silence.HasBeat && silence.Bpm == 0, Describe(silence));
            var noise = Analyse(Noise(10, Rate, 3, 0.3f), Rate);
            Check("잡음 — 박 없음", !noise.HasBeat, Describe(noise));
            var tone = Analyse(Tone(440, 8, Rate, 0.5f), Rate);
            Check("긴 음 하나 — 박 없음", !tone.HasBeat, Describe(tone));

            var bass = Analyse(Tone(60, 4, Rate, 0.5f), Rate);
            var treble = Analyse(Tone(4000, 4, Rate, 0.5f), Rate);
            Check("저음 · 고음의 칸", Mean(bass.Low) > Mean(bass.High) + 0.5f && Mean(treble.High) > Mean(treble.Low) + 0.5f,
                "60 Hz 저음 " + Mean(bass.Low).ToString("0.00") + " · 고음 " + Mean(bass.High).ToString("0.00") +
                ", 4 kHz 저음 " + Mean(treble.Low).ToString("0.00") + " · 고음 " + Mean(treble.High).ToString("0.00"));

            var loudQuiet = Tone(220, 4, Rate, 0.5f);
            for (var i = loudQuiet.Length / 2; i < loudQuiet.Length; i++) loudQuiet[i] *= 0.1f;
            var steps = Analyse(loudQuiet, Rate);
            Check("센 곳이 더 세다", steps.At(steps.Level, 1.0) > 0.9f && steps.At(steps.Level, 3.0) < 0.2f,
                "1초 " + steps.At(steps.Level, 1.0).ToString("0.00") + ", 3초 " + steps.At(steps.Level, 3.0).ToString("0.00"));

            var map = new 음악지도 { SampleRate = Rate, Hop = 256, Bpm = 120, FirstBeat = 0.25, Confidence = 1 };
            Check("박의 위치", Math.Abs(map.BeatAt(0.75) - 1) < 1e-9 && Math.Abs(map.BeatAt(0.25)) < 1e-9, "0.75초 = " + map.BeatAt(0.75).ToString("0.###") + "박");
            Check("퍼짐 — 제 박에서 1, 다음 박에는 작게, 다시 오기 전 차오름",
                Math.Abs(음악분석Policy.Pulse(0, 0, 2) - 1f) < 1e-6 && 음악분석Policy.Pulse(1, 0, 2) < 0.1f &&
                음악분석Policy.Pulse(1.95, 0, 2) > 0.5f && Math.Abs(음악분석Policy.Pulse(1, 1, 2) - 1f) < 1e-6 &&
                Math.Abs(음악분석Policy.Pulse(4, 0, 4) - 1f) < 1e-6,
                "0박 " + 음악분석Policy.Pulse(0, 0, 2).ToString("0.00") + ", 1박 " + 음악분석Policy.Pulse(1, 0, 2).ToString("0.00") +
                ", 1.95박 " + 음악분석Policy.Pulse(1.95, 0, 2).ToString("0.00"));
            Check("박 차례 나누기", 음악분석Policy.Slot(0f, 4) == 0 && 음악분석Policy.Slot((float)Math.PI, 4) == 2 &&
                음악분석Policy.Slot((float)(2 * Math.PI) - 0.01f, 4) == 3 && 음악분석Policy.Slot(-0.1f, 2) == 1 && 음악분석Policy.Slot(1f, 1) == 0);
            return results;
        }

        private static 음악지도 Analyse(float[] mono, int rate) => 음악분석Policy.Analyse(mono, rate);

        private static double Magnitude(double[] re, double[] im, int k) => Math.Sqrt(re[k] * re[k] + im[k] * im[k]);

        private static string Describe(음악지도 map) =>
            map.Bpm.ToString("0.0") + " BPM, 첫 박 " + map.FirstBeat.ToString("0.000") + "초, 분명함 " + map.Confidence.ToString("0.00");

        // 찾은 박의 줄과 아는 첫 박 사이의 가장 가까운 어긋남(초). 박이 없으면 큰 값.
        private static double BeatError(음악지도 map, double first)
        {
            if (map.Bpm <= 0) return 99;
            var period = 60 / map.Bpm;
            var d = (map.FirstBeat - first) % period;
            if (d < 0) d += period;
            return Math.Min(d, period - d);
        }

        private static float Mean(float[] values)
        {
            if (values.Length == 0) return 0f;
            double sum = 0;
            foreach (var value in values) sum += value;
            return (float)(sum / values.Length);
        }

        private static void Add(float[] target, int at, float[] sound)
        {
            for (var i = 0; i < sound.Length && at + i < target.Length; i++)
                if (at + i >= 0) target[at + i] += sound[i];
        }

        private static float[] Clicks(double bpm, double first, double seconds, int rate)
        {
            var result = new float[(int)(seconds * rate)];
            var click = new float[(int)(0.03 * rate)];
            for (var i = 0; i < click.Length; i++) click[i] = (float)(0.8 * Math.Sin(2 * Math.PI * 1000 * i / rate) * Math.Exp(-i / (0.005 * rate)));
            for (var t = first; t < seconds; t += 60 / bpm) Add(result, (int)(t * rate), click);
            return result;
        }

        private static float[] Dance(double bpm, double first, double seconds, int rate, int seed)
        {
            var random = new Random(seed);
            var result = new float[(int)(seconds * rate)];
            var kick = new float[(int)(0.3 * rate)];
            double phase = 0;
            for (var i = 0; i < kick.Length; i++)
            {
                var t = (double)i / rate;
                phase += 2 * Math.PI * (55 + 70 * Math.Exp(-t * 35)) / rate;
                kick[i] = (float)(0.9 * Math.Sin(phase) * Math.Exp(-t * 8));
            }
            var beat = 60 / bpm;
            for (var t = first; t < seconds; t += beat)
            {
                Add(result, (int)(t * rate), kick);
                var hat = new float[(int)(0.12 * rate)];
                double last = 0;
                for (var i = 0; i < hat.Length; i++)
                {
                    var white = random.NextDouble() * 2 - 1;
                    hat[i] = (float)(0.5 * (white - last) * Math.Exp(-i / (0.06 * rate)));
                    last = white;
                }
                Add(result, (int)((t + beat / 2) * rate), hat);
            }
            return result;
        }

        private static float[] Noise(double seconds, int rate, int seed, float amplitude)
        {
            var random = new Random(seed);
            var result = new float[(int)(seconds * rate)];
            for (var i = 0; i < result.Length; i++)
            {
                // 고른 수 넷의 합으로 정규분포에 가깝게 만든다.
                var sum = random.NextDouble() + random.NextDouble() + random.NextDouble() + random.NextDouble() - 2;
                result[i] = (float)(amplitude * sum * 1.73);
            }
            return result;
        }

        private static float[] Tone(double hz, double seconds, int rate, float amplitude)
        {
            var result = new float[(int)(seconds * rate)];
            for (var i = 0; i < result.Length; i++) result[i] = (float)(amplitude * Math.Sin(2 * Math.PI * hz * i / rate));
            return result;
        }
    }
}
