using System;

namespace Ssalddel.Unity.Runtime.WorldMap
{
    /// <summary>
    /// 연속 카메라 입력이 끝난 뒤 최종 관찰 문맥만 요청하도록 대기시키는 순수 상태 관문이다.
    /// 자료 요청 세대는 표현 순서만 통제하며 운영·Simulation revision을 변경하지 않는다.
    /// </summary>
    public sealed class 공간관찰요청안정관문
    {
        public const float 기본안정대기초 = .3f;

        private readonly float 안정대기초;
        private string 대기요청Key;
        private float 마지막입력시각;
        private long 요청세대;

        public 공간관찰요청안정관문(float 안정대기초 = 기본안정대기초)
        {
            if (안정대기초 < 0f)
                throw new ArgumentOutOfRangeException(nameof(안정대기초));
            this.안정대기초 = 안정대기초;
        }

        public bool 대기중 => !string.IsNullOrWhiteSpace(대기요청Key);
        public string 대기중인요청Key => 대기요청Key;
        public long 마지막요청세대 => 요청세대;

        public void 입력됨(string 요청Key, float 현재시각)
        {
            if (string.IsNullOrWhiteSpace(요청Key))
                throw new ArgumentException("SpatialObservationRequestKeyMissing", nameof(요청Key));
            대기요청Key = 요청Key;
            마지막입력시각 = 현재시각;
        }

        public bool 안정요청꺼내기(float 현재시각, out 공간관찰안정요청 요청)
        {
            요청 = default;
            if (!대기중 || 현재시각 - 마지막입력시각 + .00001f < 안정대기초)
                return false;

            요청세대++;
            요청 = new 공간관찰안정요청(
                대기요청Key,
                요청세대,
                마지막입력시각,
                현재시각);
            대기요청Key = null;
            return true;
        }

        public void 취소() => 대기요청Key = null;
    }

    public readonly struct 공간관찰안정요청
    {
        public 공간관찰안정요청(
            string 요청Key,
            long 요청세대,
            float 마지막입력시각,
            float 안정확정시각)
        {
            this.요청Key = 요청Key;
            this.요청세대 = 요청세대;
            this.마지막입력시각 = 마지막입력시각;
            this.안정확정시각 = 안정확정시각;
        }

        public string 요청Key { get; }
        public long 요청세대 { get; }
        public float 마지막입력시각 { get; }
        public float 안정확정시각 { get; }
    }
}
