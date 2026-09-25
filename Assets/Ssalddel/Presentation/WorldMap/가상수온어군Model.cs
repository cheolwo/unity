#if UNITY_EDITOR
using System;

namespace Ssalddel.Unity.Presentation.WorldMap
{
    /// <summary>비권위 로컬 실험. 종별 생태·실제 이동 속도·운영 상태와 무관한 결정적 격자 모델.</summary>
    public sealed class 가상수온어군Model
    {
        public const double StepSeconds = 2;
        private readonly double?[] temperatures = new double?[9];
        public int Current { get; private set; } = 4;
        public int Target { get; private set; } = 4;
        public double Preference { get; private set; } = 27.5;
        public double Progress { get; private set; }
        public bool Running { get; private set; } = true;
        public int Moves { get; private set; }
        public string Reason { get; private set; } = "자료 대기";
        public double Row => Current / 3 + (Target / 3 - Current / 3) * Progress;
        public double Column => Current % 3 + (Target % 3 - Current % 3) * Progress;
        public double Latitude => 37.075 - Row * .05;
        public double Longitude => 130.025 + Column * .05;

        public void SetFrame(해수면수온PreviewCell[] frame)
        {
            if(frame == null || frame.Length != 9) throw new ArgumentException("FishFrameInvalid");
            var values = new double?[9];
            for(var i=0;i<9;i++)
            {
                var c=frame[i];
                if(c==null || Math.Abs(c.latitude-(37.075-i/3*.05))>1e-8 || Math.Abs(c.longitude-(130.025+i%3*.05))>1e-8)
                    throw new ArgumentException("FishGridMismatch");
                if(c.mask==1 && c.hasValue && Finite(c.celsius) && c.celsius>=-2.00001 && c.celsius<=40) values[i]=c.celsius;
            }
            Array.Copy(values,temperatures,9); Reset();
        }
        public void Reset() { Current=Target=4; Progress=0; Moves=0; Reason="중앙에서 실험 시작"; }
        public void SetPreference(double value)
        {
            if(!Finite(value) || value < -2 || value > 40) throw new ArgumentOutOfRangeException(nameof(value));
            Preference=value; Target=Current; Progress=0; Reason="선호 수온 변경 · 재판정";
        }
        public void SetRunning(bool value) { Running=value; }
        public void Advance(double seconds)
        {
            if(!Finite(seconds) || seconds<0) throw new ArgumentOutOfRangeException(nameof(seconds));
            if(!Running || seconds==0) return;
            while(seconds>0)
            {
                if(Target==Current)
                {
                    Target=Choose();
                    if(Target==Current) return;
                }
                var remaining=(1-Progress)*StepSeconds;
                if(seconds+1e-9 < remaining) { Progress+=seconds/StepSeconds; return; }
                seconds=Math.Max(0,seconds-remaining); Current=Target; Progress=0; Moves++;
            }
        }
        private int Choose()
        {
            if(!temperatures[Current].HasValue) { Reason="현재 격자 결측 · 이동 보류"; return Current; }
            var best=Current; var distance=Math.Abs(temperatures[Current].Value-Preference);
            for(var i=0;i<9;i++)
            {
                if(Math.Abs(i/3-Current/3)+Math.Abs(i%3-Current%3)!=1 || !temperatures[i].HasValue) continue;
                var candidate=Math.Abs(temperatures[i].Value-Preference);
                if(candidate+1e-8<distance) { best=i; distance=candidate; }
            }
            Reason=best==Current ? "인접 격자 중 더 적합한 곳 없음 · 대기" : "선호 수온에 더 가까운 인접 바다로 이동";
            return best;
        }
        private static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
    }
}
#endif
