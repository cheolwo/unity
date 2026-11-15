using System;
using System.Collections.Generic;
using Ssalddel.WindowsGame;

internal static class Program
{
    private static int Main()
    {
        var checks = new List<(string Name, Action Check)>
        {
            ("초기화 전 배치 거절", BeforeReset),
            ("겹침 거절과 실패 시 개수 보존", AxisAlignedOverlap),
            ("90도 회전의 실제 폭과 높이", NinetyDegreeExtent),
            ("직각 교차 충돌", NinetyDegreeCollision),
            ("45도 교차 충돌", FortyFiveDegreeCollision),
            ("가늘고 평행한 대각선의 AABB 거짓 충돌 회피", ParallelDiagonalSeparation),
            ("서로 다른 각도의 AABB 거짓 충돌 회피", DifferentAngleSeparation),
            ("회전한 꼭짓점의 화면 이탈 거절", RotatedViewportEscape),
            ("가로세로 화면 경계와 완전히 밖인 영역", ViewportEdges),
            ("유한·양수 영역 입력 검증", InvalidAreas),
            ("잘못된 뷰포트 거절", InvalidViewport),
            ("잘못된 여유값 거절", InvalidMargins),
            ("Reset은 점유 목록을 지움", ResetClearsReservations),
            ("부분적으로 화면 밖인 고정 UI 회피", FixedUiClipping),
            ("고정 UI끼리 겹쳐도 등록", FixedUiCanOverlap),
            ("여유 0과 기본 여유의 접촉 처리", MarginAndTouching),
            ("정확한 여유 경계", ExactMargin),
            ("같은 이름도 서로 먼 위치에 배치 가능", SameTextAtDifferentPlaces),
            ("회전 각도의 부호와 한 바퀴 정규화", AngleNormalization),
            ("잘못된 고정 영역은 점유를 바꾸지 않음", InvalidFixedUi),
            ("호출 순서가 우선순위", CallerPriority),
            ("유한 입력의 연산 넘침 거절", OverflowingGeometry),
            ("작은 양수 영역도 실제 겹침을 거절", TinyPositiveGeometry),
        };

        var failed = 0;
        foreach (var check in checks)
        {
            try
            {
                check.Check();
                Console.WriteLine("[PASS] " + check.Name);
            }
            catch (Exception exception)
            {
                failed++;
                Console.Error.WriteLine("[FAIL] " + check.Name + " — " + exception.Message);
            }
        }
        Console.WriteLine($"Gu label placement checks: {checks.Count - failed} passed, {failed} failed");
        return failed == 0 ? 0 : 1;
    }

    private static 구지도이름배치Policy Ready(double width = 100, double height = 100)
    {
        var policy = new 구지도이름배치Policy();
        policy.Reset(width, height);
        return policy;
    }

    private static void Require(bool condition, string reason = "기대 결과와 다릅니다")
    {
        if (!condition) throw new InvalidOperationException(reason);
    }

    private static void BeforeReset()
    {
        var policy = new 구지도이름배치Policy();
        Require(!policy.Reserve(new 구지도이름영역(20, 20, 10, 10)));
        Require(!policy.TryReserve(new 구지도이름영역(20, 20, 10, 10)));
        Require(policy.Count == 0);
    }

    private static void AxisAlignedOverlap()
    {
        var policy = Ready();
        Require(policy.TryReserve(new 구지도이름영역(30, 30, 20, 10)));
        Require(!policy.TryReserve(new 구지도이름영역(35, 32, 20, 10)));
        Require(policy.Count == 1);
    }

    private static void NinetyDegreeExtent()
    {
        var policy = Ready();
        Require(policy.TryReserve(new 구지도이름영역(50, 50, 10, 70, 90)));
        Require(policy.TryReserve(new 구지도이름영역(50, 70, 10, 70, 90)));
        Require(!policy.TryReserve(new 구지도이름영역(8, 20, 4, 20, 90)));
    }

    private static void NinetyDegreeCollision()
    {
        var policy = Ready();
        Require(policy.Reserve(new 구지도이름영역(50, 50, 40, 4)));
        Require(!policy.TryReserve(new 구지도이름영역(50, 50, 40, 4, 90), 0));
    }

    private static void FortyFiveDegreeCollision()
    {
        var policy = Ready();
        Require(policy.Reserve(new 구지도이름영역(50, 50, 40, 4, 45)));
        Require(!policy.TryReserve(new 구지도이름영역(50, 50, 40, 4, -45), 0));
    }

    private static void ParallelDiagonalSeparation()
    {
        var policy = Ready();
        Require(policy.Reserve(new 구지도이름영역(50, 50, 30, 2, 45)));
        Require(policy.TryReserve(new 구지도이름영역(50, 55, 30, 2, 45), 0),
            "AABB는 겹쳐도 대각선 이름의 실제 영역은 떨어져 있습니다");
    }

    private static void DifferentAngleSeparation()
    {
        var policy = Ready();
        Require(policy.Reserve(new 구지도이름영역(50, 50, 40, 2, 45)));
        Require(policy.TryReserve(new 구지도이름영역(69, 58, 40, 2, 60), 0),
            "서로 다른 각도의 실제 사각형은 겹치지 않습니다");
    }

    private static void RotatedViewportEscape()
    {
        var policy = Ready();
        Require(!policy.TryReserve(new 구지도이름영역(10, 50, 20, 20, 45)));
        Require(!policy.TryReserve(new 구지도이름영역(90, 50, 20, 20, 45)));
        Require(!policy.TryReserve(new 구지도이름영역(50, 10, 20, 20, 45)));
        Require(!policy.TryReserve(new 구지도이름영역(50, 90, 20, 20, 45)));
        Require(policy.Count == 0);
    }

    private static void ViewportEdges()
    {
        var policy = Ready();
        Require(policy.TryReserve(new 구지도이름영역(5, 5, 10, 10), 0));
        Require(policy.TryReserve(new 구지도이름영역(95, 95, 10, 10), 0));
        Require(!policy.TryReserve(new 구지도이름영역(105, 50, 10, 10), 0));
        Require(!policy.TryReserve(new 구지도이름영역(-0.1, 50, 1, 1), 0));
    }

    private static void InvalidAreas()
    {
        var policy = Ready();
        var invalid = new[]
        {
            new 구지도이름영역(20, 20, 0, 10), new 구지도이름영역(20, 20, 10, -1),
            new 구지도이름영역(double.NaN, 20, 10, 10), new 구지도이름영역(20, double.PositiveInfinity, 10, 10),
            new 구지도이름영역(20, 20, double.NaN, 10), new 구지도이름영역(20, 20, 10, double.NegativeInfinity),
            new 구지도이름영역(20, 20, 10, 10, double.NaN), new 구지도이름영역(20, 20, 10, 10, double.PositiveInfinity),
        };
        foreach (var area in invalid) Require(!policy.TryReserve(area));
        Require(policy.Count == 0);
    }

    private static void InvalidViewport()
    {
        foreach (var value in new[] { 0d, -1d, double.NaN, double.PositiveInfinity })
        {
            var policy = new 구지도이름배치Policy();
            var rejected = false;
            try { policy.Reset(value, 100); }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            Require(rejected);
            rejected = false;
            try { policy.Reset(100, value); }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            Require(rejected);
        }
    }

    private static void InvalidMargins()
    {
        var policy = Ready();
        foreach (var margin in new[] { -1d, double.NaN, double.PositiveInfinity })
            Require(!policy.TryReserve(new 구지도이름영역(20, 20, 10, 10), margin));
        Require(policy.Count == 0);
    }

    private static void ResetClearsReservations()
    {
        var policy = Ready();
        Require(policy.Reserve(new 구지도이름영역(50, 50, 100, 100)));
        Require(!policy.TryReserve(new 구지도이름영역(50, 50, 10, 10)));
        policy.Reset(200, 200);
        Require(policy.Count == 0);
        Require(policy.TryReserve(new 구지도이름영역(150, 150, 20, 20)));
    }

    private static void FixedUiClipping()
    {
        var policy = Ready();
        Require(policy.Reserve(new 구지도이름영역(50, -10, 100, 40)));
        Require(!policy.TryReserve(new 구지도이름영역(50, 10, 20, 10)));
        Require(policy.TryReserve(new 구지도이름영역(50, 30, 20, 10)));
    }

    private static void FixedUiCanOverlap()
    {
        var policy = Ready();
        Require(policy.Reserve(new 구지도이름영역(30, 20, 40, 30)));
        Require(policy.Reserve(new 구지도이름영역(40, 20, 40, 30)));
        Require(policy.Count == 2);
        Require(!policy.TryReserve(new 구지도이름영역(60, 20, 10, 10)));
    }

    private static void MarginAndTouching()
    {
        var policy = Ready();
        Require(policy.Reserve(new 구지도이름영역(20, 20, 10, 10)));
        Require(!policy.TryReserve(new 구지도이름영역(30, 20, 10, 10)));
        Require(policy.TryReserve(new 구지도이름영역(30, 20, 10, 10), 0));
    }

    private static void ExactMargin()
    {
        var policy = Ready();
        Require(policy.Reserve(new 구지도이름영역(20, 20, 10, 10)));
        Require(!policy.TryReserve(new 구지도이름영역(31.9, 20, 10, 10), 2));
        Require(policy.TryReserve(new 구지도이름영역(32, 20, 10, 10), 2));
    }

    private static void SameTextAtDifferentPlaces()
    {
        // 이름 문자열로 중복 제거하지 않는다. 같은 이름이라도 화면의 실제 위치가 기준이다.
        var policy = Ready();
        Require(policy.TryReserve(new 구지도이름영역(20, 20, 20, 10)));
        Require(policy.TryReserve(new 구지도이름영역(75, 75, 20, 10)));
        Require(policy.Count == 2);
    }

    private static void AngleNormalization()
    {
        var policy = Ready();
        Require(policy.Reserve(new 구지도이름영역(50, 50, 40, 5, -45)));
        Require(!policy.TryReserve(new 구지도이름영역(50, 50, 40, 5, 315), 0));
        policy.Reset(100, 100);
        Require(policy.Reserve(new 구지도이름영역(50, 50, 40, 5, 0)));
        Require(!policy.TryReserve(new 구지도이름영역(50, 50, 40, 5, 720), 0));
    }

    private static void InvalidFixedUi()
    {
        var policy = Ready();
        Require(!policy.Reserve(new 구지도이름영역(double.NaN, 0, 10, 10)));
        Require(!policy.Reserve(new 구지도이름영역(10, 10, -1, 10)));
        Require(policy.Count == 0);
    }

    private static void CallerPriority()
    {
        var policy = Ready();
        Require(policy.TryReserve(new 구지도이름영역(40, 40, 20, 10)));
        Require(!policy.TryReserve(new 구지도이름영역(40, 40, 20, 10)));
        Require(policy.Count == 1);
    }

    private static void OverflowingGeometry()
    {
        var policy = Ready();
        Require(!policy.Reserve(new 구지도이름영역(double.MaxValue, 50, double.MaxValue, 10)));
        Require(!policy.TryReserve(new 구지도이름영역(50, 50, double.PositiveInfinity, 10)));
        Require(policy.Count == 0);
    }

    private static void TinyPositiveGeometry()
    {
        var policy = Ready();
        var area = new 구지도이름영역(20, 20, 0.0000000001, 0.0000000001, 45);
        Require(policy.Reserve(area));
        Require(!policy.TryReserve(area, 0));
        Require(policy.Count == 1);
    }
}
