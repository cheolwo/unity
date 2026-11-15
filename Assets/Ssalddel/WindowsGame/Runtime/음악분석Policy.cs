using System;

namespace Ssalddel.WindowsGame
{
    // 음악 파일의 분석 (2026-11-14, 사용자: mp3 · wav를 넘기면 진동수 · 진폭을 분석해 빛을 맞추고 싶다).
    // 곡 전체를 미리 한 번 분석한다(실시간 추정이 아니다). 결과는 곡 지도(음악지도)다.
    //  - 시간 칸(약 11.6 ms)마다의 세기와 저음 · 중음 · 고음의 세기(0~1), 소리가 새로 시작하는 정도(온셋, 0~1).
    //  - 박자(BPM)와 첫 박의 시각. 템포가 일정한 곡을 가정한다. 박이 분명하지 않으면 신뢰도가 낮고, 그때 빛은 세기에만 반응한다.
    //  - 원음은 남기지 않는다. 분석이 끝나면 곡 지도만 남는다.
    // UnityEngine에 기대지 않는 순수 규칙이다(검사: 음악분석Checks).
    internal sealed class 음악지도
    {
        public int SampleRate;                    // 분석에 쓴 표본 빈도 (Hz)
        public int Hop;                           // 시간 칸 하나의 표본 수
        public double Duration;                   // 곡의 길이 (초)
        public float[] Level = Array.Empty<float>();     // 칸마다의 세기 (0~1)
        public float[] Low = Array.Empty<float>();       // 저음 30~150 Hz (0~1)
        public float[] Mid = Array.Empty<float>();       // 중음 150~2000 Hz (0~1)
        public float[] High = Array.Empty<float>();      // 고음 2000~8000 Hz (0~1)
        public float[] Onset = Array.Empty<float>();     // 새 소리가 시작하는 정도 (0~1)
        public double Bpm;                        // 0이면 박을 찾지 못했다
        public double FirstBeat;                  // 첫 박의 시각 (초, 0 이상 한 박 미만)
        public double Confidence;                 // 박의 분명함 (0~1)

        public double SecondsPerFrame => SampleRate <= 0 ? 0 : (double)Hop / SampleRate;
        public bool HasBeat => Bpm > 0 && Confidence >= 음악분석Policy.BeatConfidence;

        // 곡의 seconds초가 몇 번째 박인가(첫 박 = 0, 실수). 박이 없으면 0.
        public double BeatAt(double seconds) => HasBeat ? (seconds - FirstBeat) * Bpm / 60.0 : 0;

        // 칸마다의 값을 그 시각에서 읽는다(앞뒤 칸 사이를 잇는다).
        public float At(float[] series, double seconds)
        {
            if (series == null || series.Length == 0 || SecondsPerFrame <= 0) return 0f;
            var position = seconds / SecondsPerFrame;
            if (position <= 0) return series[0];
            var index = (int)position;
            if (index >= series.Length - 1) return series[series.Length - 1];
            var t = (float)(position - index);
            return series[index] * (1f - t) + series[index + 1] * t;
        }
    }

    internal static class 음악분석Policy
    {
        internal const double MinBpm = 60, MaxBpm = 200, CenterBpm = 120;
        internal const double BeatConfidence = 0.25;      // 이보다 낮으면 박에 맞추지 않고 세기에만 반응한다
        internal const double LowFrom = 30, LowTo = 150, MidTo = 2000, HighTo = 8000;
        internal const int MaxAnalysisRate = 30000;       // 이보다 높은 표본 빈도는 반으로 줄여서 분석한다

        // 표본 빈도에 맞춘 창과 칸: 창 약 46 ms, 칸 약 11.6 ms.
        internal static int FrameFor(int sampleRate) => sampleRate <= MaxAnalysisRate ? 1024 : 2048;
        internal static int HopFor(int sampleRate) => sampleRate <= MaxAnalysisRate ? 256 : 512;

        // 한 줄(모노) 표본으로 곡 지도를 만든다.
        // 박은 저음 · 중음 · 고음의 온셋을 같은 무게로 더한 줄에서 찾는다(고음의 칸이 많아 하이햇이 박을 차지하는 것을 막는다).
        // 박의 자리가 반 박 어긋나 보이면 저음(킥)이 더 센 쪽으로 옮긴다.
        internal static 음악지도 Analyse(float[] mono, int sampleRate)
        {
            var map = new 음악지도 { SampleRate = Math.Max(1, sampleRate), Hop = HopFor(sampleRate) };
            if (mono == null || mono.Length == 0 || sampleRate <= 0) return map;
            map.Duration = (double)mono.Length / sampleRate;
            var frame = FrameFor(sampleRate);
            var hop = map.Hop;
            var frames = Math.Max(1, (mono.Length + hop - 1) / hop);
            var level = new float[frames];
            var low = new float[frames];
            var mid = new float[frames];
            var high = new float[frames];
            var flux = new float[frames];
            var fluxLow = new float[frames];
            var fluxMid = new float[frames];
            var fluxHigh = new float[frames];

            var window = new double[frame];
            for (var i = 0; i < frame; i++) window[i] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (frame - 1));
            var re = new double[frame];
            var im = new double[frame];
            var half = frame / 2;
            var binHz = (double)sampleRate / frame;
            var lowA = Math.Max(1, (int)Math.Ceiling(LowFrom / binHz));
            var lowB = Math.Min(half, (int)Math.Floor(LowTo / binHz));
            var midB = Math.Min(half, (int)Math.Floor(MidTo / binHz));
            var highB = Math.Min(half, (int)Math.Floor(HighTo / binHz));
            var previous = new double[half + 1];
            var current = new double[half + 1];

            for (var f = 0; f < frames; f++)
            {
                // 창의 가운데가 칸의 시각(f × hop)이 되게 한다. 곡 밖은 0이다.
                var start = f * hop - half;
                for (var i = 0; i < frame; i++)
                {
                    var at = start + i;
                    var sample = at >= 0 && at < mono.Length ? mono[at] : 0f;
                    re[i] = sample * window[i];
                    im[i] = 0;
                }
                double sum2 = 0;
                for (var i = 0; i < hop; i++)
                {
                    var at = f * hop + i;
                    if (at < mono.Length) sum2 += mono[at] * (double)mono[at];
                }
                level[f] = (float)Math.Sqrt(sum2 / hop);
                Fft(re, im);
                double l = 0, m = 0, h = 0, all = 0, riseLow = 0, riseMid = 0, riseHigh = 0;
                for (var k = 1; k <= highB; k++)
                {
                    var power = re[k] * re[k] + im[k] * im[k];
                    current[k] = Math.Log(1 + 100 * Math.Sqrt(power));
                    var rise = current[k] - previous[k];
                    if (rise < 0) rise = 0;
                    all += rise;
                    if (k >= lowA && k <= lowB) { l += power; riseLow += rise; }
                    else if (k > lowB && k <= midB) { m += power; riseMid += rise; }
                    else if (k > midB) { h += power; riseHigh += rise; }
                }
                low[f] = (float)Math.Sqrt(l);
                mid[f] = (float)Math.Sqrt(m);
                high[f] = (float)Math.Sqrt(h);
                if (f > 0)
                {
                    flux[f] = (float)all;
                    fluxLow[f] = (float)riseLow;
                    fluxMid[f] = (float)riseMid;
                    fluxHigh[f] = (float)riseHigh;
                }
                var swap = previous;
                previous = current;
                current = swap;
            }

            var reach = Math.Max(1, (int)Math.Round(0.2 * sampleRate / hop));
            var lifted = Lift(flux, reach);
            var kick = Normalise(Lift(fluxLow, reach), 2f);
            var midOnset = Normalise(Lift(fluxMid, reach), 2f);
            var highOnset = Normalise(Lift(fluxHigh, reach), 4f);
            // 소리가 새로 시작하는 일이 거의 없는 곡(긴 음 하나, 잔잔한 패드)은 박을 믿지 않는다.
            var presence = Math.Max(0, Math.Min(1, Percentile98(lifted) / 16.0));

            // 세 칸은 저마다 맞추되, 가장 센 칸보다 26 dB 넘게 작은 칸은 1로 키우지 않는다(저음이 없는 곡에서 잡음이 저음처럼 보이지 않게).
            var bandFloor = 0.05f * Math.Max(Percentile98(low), Math.Max(Percentile98(mid), Percentile98(high)));
            map.Level = Normalise(level);
            map.Low = Normalise(low, bandFloor);
            map.Mid = Normalise(mid, bandFloor);
            map.High = Normalise(high, bandFloor);
            map.Onset = Normalise(lifted, 8f);

            var bands = new float[frames];
            for (var f = 0; f < frames; f++) bands[f] = (kick[f] + midOnset[f] + highOnset[f]) / 3f;
            var beatLine = Smooth(Normalise(bands, 0.05f));
            var framesPerSecond = (double)sampleRate / hop;
            var (bpm, strength) = Tempo(beatLine, framesPerSecond);
            if (bpm <= 0) return map;
            var period = framesPerSecond * 60.0 / bpm;
            var phase = PhaseOf(beatLine, period, out var atBeats, out var overall);
            var kickLine = Smooth(kick);
            var other = (phase + period / 2) % period;
            var onBeat = GridMean(kickLine, phase, period);
            var offBeat = GridMean(kickLine, other, period);
            if (offBeat > 1.25 * onBeat && offBeat > 0.05) phase = other;
            map.Bpm = bpm;
            map.FirstBeat = phase / framesPerSecond;
            var ratio = overall <= 1e-9 ? 0 : atBeats / overall;
            map.Confidence = Math.Max(0, Math.Min(1, (ratio - 1) / 3.0)) * Math.Min(1, strength * 4) * presence;
            return map;
        }

        // 제자리 FFT (길이는 2의 거듭제곱).
        internal static void Fft(double[] re, double[] im)
        {
            var n = re.Length;
            for (int i = 1, j = 0; i < n; i++)
            {
                var bit = n >> 1;
                for (; (j & bit) != 0; bit >>= 1) j ^= bit;
                j ^= bit;
                if (i >= j) continue;
                var tr = re[i]; re[i] = re[j]; re[j] = tr;
                var ti = im[i]; im[i] = im[j]; im[j] = ti;
            }
            for (var length = 2; length <= n; length <<= 1)
            {
                var angle = -2 * Math.PI / length;
                var wr = Math.Cos(angle);
                var wi = Math.Sin(angle);
                for (var i = 0; i < n; i += length)
                {
                    double cr = 1, ci = 0;
                    for (var k = 0; k < length / 2; k++)
                    {
                        var a = i + k;
                        var b = a + length / 2;
                        var tr = re[b] * cr - im[b] * ci;
                        var ti = re[b] * ci + im[b] * cr;
                        re[b] = re[a] - tr;
                        im[b] = im[a] - ti;
                        re[a] += tr;
                        im[a] += ti;
                        var next = cr * wr - ci * wi;
                        ci = cr * wi + ci * wr;
                        cr = next;
                    }
                }
            }
        }

        // 둘레(앞뒤 reach칸)의 평균을 빼서 새로 솟는 것만 남긴다.
        internal static float[] Lift(float[] values, int reach)
        {
            var n = values.Length;
            var sums = new double[n + 1];
            for (var i = 0; i < n; i++) sums[i + 1] = sums[i] + values[i];
            var result = new float[n];
            for (var f = 0; f < n; f++)
            {
                var a = Math.Max(0, f - reach);
                var b = Math.Min(n, f + reach + 1);
                var value = values[f] - (float)((sums[b] - sums[a]) / (b - a));
                result[f] = value > 0 ? value : 0f;
            }
            return result;
        }

        internal static float Percentile98(float[] values)
        {
            if (values.Length == 0) return 0f;
            var sorted = (float[])values.Clone();
            Array.Sort(sorted);
            return sorted[Math.Min(sorted.Length - 1, (int)(sorted.Length * 0.98))];
        }

        // 큰 값 몇 개에 끌려가지 않게, 위에서 2%쯤 되는 값을 1로 맞춘다. floor보다 작은 값은 1로 키우지 않는다(잡음을 부풀리지 않는다).
        internal static float[] Normalise(float[] values, float floor = 0f)
        {
            var result = new float[values.Length];
            if (values.Length == 0) return result;
            var top = Math.Max(floor, Percentile98(values));
            if (top <= 1e-9f)
            {
                foreach (var value in values) top = Math.Max(top, value);
            }
            if (top <= 1e-9f) return result;
            for (var i = 0; i < values.Length; i++) result[i] = Math.Min(1f, values[i] / top);
            return result;
        }

        internal static float[] Smooth(float[] values)
        {
            var result = new float[values.Length];
            for (var i = 0; i < values.Length; i++)
            {
                double sum = 0, weight = 0;
                for (var d = -2; d <= 2; d++)
                {
                    var at = i + d;
                    if (at < 0 || at >= values.Length) continue;
                    var w = d == 0 ? 6 : Math.Abs(d) == 1 ? 4 : 1;
                    sum += values[at] * w;
                    weight += w;
                }
                result[i] = (float)(sum / weight);
            }
            return result;
        }

        // 온셋의 자기상관에서 박의 길이를 찾는다. 두 배 · 네 배 길이의 자기상관도 조금 더해(박이 거듭 맞는지)
        // 3/4 · 4/3 같은 엇갈린 템포를 고르지 않게 하고, 120 BPM 둘레를 조금 더 믿는다.
        // 돌려주는 strength는 그 박 길이에서의 자기상관 ÷ 0에서의 자기상관(0~1).
        internal static (double Bpm, double Strength) Tempo(float[] onset, double framesPerSecond)
        {
            var n = onset.Length;
            if (n < 8 || framesPerSecond <= 0) return (0, 0);
            double mean = 0;
            for (var i = 0; i < n; i++) mean += onset[i];
            mean /= n;
            var centred = new double[n];
            for (var i = 0; i < n; i++) centred[i] = onset[i] - mean;
            double zero = 0;
            for (var i = 0; i < n; i++) zero += centred[i] * centred[i];
            if (zero <= 1e-12) return (0, 0);
            zero /= n;
            var shortest = Math.Max(1, (int)Math.Floor(framesPerSecond * 60 / MaxBpm));
            var longest = Math.Min(n / 2, (int)Math.Ceiling(framesPerSecond * 60 / MinBpm));
            if (longest <= shortest + 1) return (0, 0);
            var cache = new double[4 * longest + 2];
            var known = new bool[cache.Length];
            double Acf(int lag)
            {
                if (lag < 1 || lag >= n || lag >= cache.Length) return 0;
                if (known[lag]) return cache[lag];
                double sum = 0;
                for (var i = 0; i + lag < n; i++) sum += centred[i] * centred[i + lag];
                known[lag] = true;
                return cache[lag] = sum / (n - lag);
            }
            var best = -1;
            var bestScore = double.MinValue;
            for (var lag = shortest; lag <= longest; lag++)
            {
                var bpm = framesPerSecond * 60 / lag;
                var octave = Math.Log(bpm / CenterBpm, 2);
                var score = (Acf(lag) + 0.5 * Acf(2 * lag) + 0.25 * Acf(4 * lag)) * Math.Exp(-0.5 * octave * octave);
                if (score <= bestScore) continue;
                bestScore = score;
                best = lag;
            }
            if (best < 0 || Acf(best) <= 0) return (0, 0);
            // 이웃 두 칸으로 꼭짓점을 다듬는다.
            var refined = (double)best;
            var y0 = Acf(best - 1);
            var y1 = Acf(best);
            var y2 = Acf(best + 1);
            var curve = y0 - 2 * y1 + y2;
            if (curve < 0) refined += Math.Max(-0.5, Math.Min(0.5, 0.5 * (y0 - y2) / curve));
            return (framesPerSecond * 60 / refined, Math.Max(0, Math.Min(1, y1 / zero)));
        }

        // phase칸에서 시작해 period칸마다 놓인 자리들의 평균값.
        internal static double GridMean(float[] values, double phase, double period)
        {
            if (values.Length < 2 || period <= 0) return 0;
            double sum = 0;
            var count = 0;
            for (var at = phase; at < values.Length - 1; at += period)
            {
                var index = (int)at;
                var t = at - index;
                sum += values[index] * (1 - t) + values[index + 1] * t;
                count++;
            }
            return count == 0 ? 0 : sum / count;
        }

        // 박 길이(칸)가 정해졌을 때 박이 놓이는 자리(첫 박, 칸)를 찾는다. 그 자리들의 온셋 평균이 가장 큰 곳이다.
        internal static double PhaseOf(float[] onset, double period, out double atBeats, out double overall)
        {
            atBeats = 0;
            overall = 0;
            if (onset.Length == 0 || period <= 1) return 0;
            for (var i = 0; i < onset.Length; i++) overall += onset[i];
            overall /= onset.Length;
            var bestPhase = 0.0;
            var bestMean = -1.0;
            for (var phase = 0.0; phase < period; phase += 0.25)
            {
                var mean = GridMean(onset, phase, period);
                if (mean <= bestMean) continue;
                bestMean = mean;
                bestPhase = phase;
            }
            atBeats = Math.Max(0, bestMean);
            return bestPhase;
        }

        // 빛 하나가 몇 번째 박에 퍼질지(0 ~ slots-1). phase는 빛마다의 엇갈림(0~2π)이다.
        internal static int Slot(float phase, int slots)
        {
            if (slots <= 1) return 0;
            var unit = phase / (2 * Math.PI);
            unit -= Math.Floor(unit);
            return Math.Min(slots - 1, (int)(unit * slots));
        }

        // 박에 맞춘 퍼짐(0~1). beat는 곡의 박 위치(첫 박 = 0, 실수). slots개의 차례 가운데 slot번째 박에서 1이 되고 스러진다.
        // 박 바로 앞 0.12박 동안은 차오른다(박에 맞춰 보이게).
        internal static float Pulse(double beat, int slot, int slots)
        {
            if (slots < 1) slots = 1;
            var since = beat - slot;
            since -= Math.Floor(since / slots) * slots;
            var decay = Math.Exp(-since * 2.5);
            var ahead = slots - since;
            if (ahead < 0.12) decay = Math.Max(decay, 1 - ahead / 0.12);
            return (float)Math.Max(0, Math.Min(1, decay));
        }

        // 표본 빈도를 반으로 줄인다(이웃 둘의 평균). 44.1 kHz · 48 kHz 곡을 22 · 24 kHz로 분석한다.
        internal static float[] Halve(float[] mono)
        {
            var result = new float[mono.Length / 2];
            for (var i = 0; i < result.Length; i++) result[i] = 0.5f * (mono[2 * i] + mono[2 * i + 1]);
            return result;
        }
    }
}
