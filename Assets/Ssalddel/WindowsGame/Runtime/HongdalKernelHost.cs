using System;
using System.Linq;
using Ssalddel.WindowsGame.Kernel;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 0단계 뼈대: 게임 커널을 조립하는 곳. 팩(Resources)을 읽어 업무 모듈을 꽂는다.
    // 음식 배달이 1호이고 시각의 기준이다. 업종 팩이 없으면 음식 배달만 돈다.
    internal sealed class HongdalKernelHost
    {
        internal const string DeliverySampleResource = "HongdalWindows/delivery-sample";
        internal const string RoadGraphResource = "HongdalWindows/road-graph";
        internal const string FirstChainPackResource = "hongdal-business-first-chain";
        internal const string TerritoryPackResource = "hongdal-territory";
        // 2026-11-13 영역의 판을 행정동으로 통일한다. 행정동 영역 팩과 동별 수요 팩이 함께 있으면 실행 커널이 그 둘을 쓴다
        // (동네 2.5D의 깃발·밝기·동 사업 칸, 구 지도의 법정동 묶음 보기). 하나라도 없거나 검증 촬영이면 예전 법정동 팩이다.
        internal const string TerritoryHjdPackResource = "hongdal-territory-hjd";
        internal const string DongDemandPackResource = "hongdal-dong-demand";
        private const string SessionPortType =
            "Ssalddel.WindowsGame.SimulationBridge.SessionFranchiseSupplyPort, Ssalddel.WindowsGame.SimulationBridge";

        internal GameKernel Kernel { get; private set; }
        internal FoodDeliveryModule Food { get; private set; }
        internal FirstChainPack Pack { get; private set; }

        internal static HongdalKernelHost Create(string deliverySampleJson, string roadGraphJson)
        {
            var host = new HongdalKernelHost
            {
                Kernel = new GameKernel(),
                Food = new FoodDeliveryModule(deliverySampleJson, roadGraphJson),
            };
            host.Kernel.Register(host.Food);
            // 기사 무념·경 시간·등급(2단계, 2026-11-14). 음식 배달 바로 뒤에 꽂아 같은 시각의 상태를 읽는다.
            host.Kernel.Register(new 기사무념Module());
            // r174 결산 확인(걸음 4, 2026-11-14). 읽음 상태만 맡는다. 시계·원장·다른 사업을 건드리지 않는다.
            host.Kernel.Register(new 일일결산Module());
            // 영역: 행정동 깃발. 두 팩을 다 읽은 뒤에 함께 꽂는다(하나만 꽂히는 일이 없게).
            // 기존 검증 촬영(--sagajeong-…)은 예전 법정동 영역을 기준으로 하므로 그대로 둔다.
            var proof = Environment.GetCommandLineArgs().Any(item => item.StartsWith("--sagajeong-", StringComparison.Ordinal));
            var hjdAsset = proof ? null : Resources.Load<TextAsset>(TerritoryHjdPackResource);
            var demandAsset = proof ? null : Resources.Load<TextAsset>(DongDemandPackResource);
            var flags = false;
            if (hjdAsset != null && demandAsset != null)
            {
                try
                {
                    var land = new TerritoryModule(TerritoryPack.Parse(hjdAsset.text));
                    var demand = new DongDemandModule(DongDemandPack.Parse(demandAsset.text));
                    host.Kernel.Register(land);
                    host.Kernel.Register(demand);
                    flags = true;
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("Hongdal kernel: 행정동 영역·동별 수요 팩을 읽지 못해 예전 법정동 영역으로 엽니다. " + exception.Message);
                }
            }
            // 영역(예전: 어느 법정동까지 운영하는가). 팩이 없으면 구 지도는 예전 그림으로 남는다.
            var territoryAsset = flags ? null : Resources.Load<TextAsset>(TerritoryPackResource);
            if (territoryAsset != null)
            {
                try
                {
                    host.Kernel.Register(new TerritoryModule(TerritoryPack.Parse(territoryAsset.text)));
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("Hongdal kernel: 영역 팩을 읽지 못했습니다. " + exception.Message);
                }
            }
            var packAsset = Resources.Load<TextAsset>(FirstChainPackResource);
            if (packAsset == null) return host;
            try
            {
                var pack = FirstChainPack.Parse(packAsset.text);
                host.Kernel.Register(new FranchiseSupplyModule(pack, CreateSessionPort()));
                host.Kernel.Register(new LogisticsCenterModule(pack));
                host.Kernel.Register(new CargoTransportModule(pack));
                host.Kernel.Register(new StoreOperationsModule(pack));
                host.Pack = pack;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Hongdal kernel: 업종 팩을 읽지 못해 음식 배달만 엽니다. " + exception.Message);
            }
            return host;
        }

        // 큰 Simulation 연결 어셈블리는 그 패키지가 있는 프로젝트에서만 컴파일된다. 그래서 이름으로 찾는다.
        internal static IFranchiseSupplyPort CreateSessionPort()
        {
            try
            {
                var type = Type.GetType(SessionPortType);
                return type == null ? null : Activator.CreateInstance(type) as IFranchiseSupplyPort;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Hongdal kernel: 큰 Simulation 연결을 열지 못했습니다. " + exception.Message);
                return null;
            }
        }
    }

    /// <summary>뼈대 관문 검사. 빌드할 때 Editor가 부른다.</summary>
    public static class HongdalKernelChecks
    {
        public static string Run()
        {
            var sample = Resources.Load<TextAsset>(HongdalKernelHost.DeliverySampleResource);
            var roads = Resources.Load<TextAsset>(HongdalKernelHost.RoadGraphResource);
            var pack = Resources.Load<TextAsset>(HongdalKernelHost.FirstChainPackResource);
            if (sample == null || roads == null || pack == null)
                return "Hongdal kernel checks: 0 passed, 1 failed, 0 skipped\n[FAIL] 자료 — 표본·도로·업종 팩 가운데 없는 것이 있습니다";
            var territory = Resources.Load<TextAsset>(HongdalKernelHost.TerritoryPackResource);
            var kernel = KernelChecks.Run(sample.text, roads.text, pack.text, HongdalKernelHost.CreateSessionPort,
                territory == null ? null : territory.text).ToString();
            return kernel + "\n" + MapOrderEventChecks() + "\n" + NationFlowChecks() + "\n" + DecisionCycleChecks() + "\n" +
                DongModuleChecks() + "\n" + DongDemandChecksBlock() + "\n" + RiderMindChecks(sample.text, roads.text) + "\n" +
                일일결산Checks.Run() + "\n" + 나눔카드Checks.Run() + "\n" + OperatingRoundChecks() + "\n" + RouteChecks() + "\n" + MusicAnalysisChecks() + "\n" +
                AutopilotChecks() + "\n" + NoticeChecks() + "\n" + MingdeLightChecks() + "\n" + PlaceChecks() + "\n" + RoleAppReplayChecks() + "\n" + NextTodoChecks() + "\n" + ContinuousMapChecks() + "\n" + PlatformPackageChecks() + "\n" + FiveElementBoardChecks();
        }

        // 기사 무념·경 시간·등급(2단계, 2026-11-14). 순수 규칙 검사와, 음식 배달 모듈 옆에 꽂아 보는 검사.
        private static string RiderMindChecks(string sampleJson, string roadJson)
        {
            var results = 기사무념Checks.Run();
            results.AddRange(기사무념ModuleChecks.Run(sampleJson, roadJson));
            var failed = 0;
            var lines = new System.Text.StringBuilder();
            foreach (var (name, ok, detail) in results)
            {
                if (!ok) failed++;
                lines.Append('\n').Append(ok ? "[PASS] " : "[FAIL] ").Append(name);
                if (!string.IsNullOrEmpty(detail)) lines.Append(" — ").Append(detail);
            }
            return "Hongdal rider mind checks: " + (results.Count - failed) + " passed, " + failed + " failed" + lines;
        }

        // 동별 수요(행정동)와 행정동 영역 팩. 커널 층의 규칙이며 화면과 무관하다.
        private static string DongDemandChecksBlock()
        {
            var land = Resources.Load<TextAsset>(HongdalKernelHost.TerritoryHjdPackResource);
            var demand = Resources.Load<TextAsset>(HongdalKernelHost.DongDemandPackResource);
            return DongDemandChecks.Run(land == null ? null : land.text, demand == null ? null : demand.text);
        }

        // r154·r157 새 주문 표현 규칙. 커널 검사와 따로 센다(화면 표현이며 업무 상태를 바꾸지 않는다).
        private static string MapOrderEventChecks()
        {
            var results = 지도새주문표현Checks.Run();
            var failed = 0;
            var lines = new System.Text.StringBuilder();
            foreach (var (name, ok, detail) in results)
            {
                if (!ok) failed++;
                lines.Append('\n').Append(ok ? "[PASS] " : "[FAIL] ").Append(name);
                if (!string.IsNullOrEmpty(detail)) lines.Append(" — ").Append(detail);
            }
            return "Hongdal map order event checks: " + (results.Count - failed) + " passed, " + failed + " failed" + lines;
        }

        // r155 N1 전국 해상·항공 흐름 표현 규칙. 화면 표현이며 업무 상태를 바꾸지 않는다.
        private static string NationFlowChecks()
        {
            var results = 전국흐름표현Checks.Run();
            var failed = 0;
            var lines = new System.Text.StringBuilder();
            foreach (var (name, ok, detail) in results)
            {
                if (!ok) failed++;
                lines.Append('\n').Append(ok ? "[PASS] " : "[FAIL] ").Append(name);
                if (!string.IsNullOrEmpty(detail)) lines.Append(" — ").Append(detail);
            }
            return "Hongdal nation flow checks: " + (results.Count - failed) + " passed, " + failed + " failed" + lines;
        }

        // r187 짧은 운영 회차(2026-11-14). 대상 한 건에 묶인 회차의 순서 규칙이며 업무 상태를 바꾸지 않는다.
        private static string OperatingRoundChecks()
        {
            var results = 운영회차Checks.Run();
            var failed = 0;
            var lines = new System.Text.StringBuilder();
            foreach (var (name, ok, detail) in results)
            {
                if (!ok) failed++;
                lines.Append('\n').Append(ok ? "[PASS] " : "[FAIL] ").Append(name);
                if (!string.IsNullOrEmpty(detail)) lines.Append(" — ").Append(detail);
            }
            return "Hongdal operating round checks: " + (results.Count - failed) + " passed, " + failed + " failed" + lines;
        }

        // 켜 두기 성장 · 맡김 뼈대(2026-11-15). 손이 빈 동안 플레이어가 누를 수 있는 명령만 고르는 규칙이며 업무 상태를 바꾸지 않는다.
        private static string AutopilotChecks()
        {
            var results = 맡김Checks.Run();
            var failed = 0;
            var lines = new System.Text.StringBuilder();
            foreach (var (name, ok, detail) in results)
            {
                if (!ok) failed++;
                lines.Append('\n').Append(ok ? "[PASS] " : "[FAIL] ").Append(name);
                if (!string.IsNullOrEmpty(detail)) lines.Append(" — ").Append(detail);
            }
            return "Hongdal autopilot checks: " + (results.Count - failed) + " passed, " + failed + " failed" + lines;
        }

        // 명1 명덕 → 빛(2026-11-15). 쌓인 명덕을 빛의 크기 · 세기 배율로 바꾸는 순수 규칙이며 명덕을 만들지 않는다.
        private static string MingdeLightChecks()
        {
            var results = 명덕빛Checks.Run();
            var failed = 0;
            var lines = new System.Text.StringBuilder();
            foreach (var (name, ok, detail) in results)
            {
                if (!ok) failed++;
                lines.Append('\n').Append(ok ? "[PASS] " : "[FAIL] ").Append(name);
                if (!string.IsNullOrEmpty(detail)) lines.Append(" — ").Append(detail);
            }
            return "Hongdal mingde light checks: " + (results.Count - failed) + " passed, " + failed + " failed" + lines;
        }

        // 홍달 플레이스 순위 · 쪽 · 주문자 투영(2026-11-15). 시험 자료로 순위표를 만드는 순수 규칙이며 업무 상태를 바꾸지 않는다.
        private static string PlaceChecks()
        {
            var results = 플레이스Checks.Run();
            var failed = 0;
            var lines = new System.Text.StringBuilder();
            foreach (var (name, ok, detail) in results)
            {
                if (!ok) failed++;
                lines.Append('\n').Append(ok ? "[PASS] " : "[FAIL] ").Append(name);
                if (!string.IsNullOrEmpty(detail)) lines.Append(" — ").Append(detail);
            }
            return "Hongdal place checks: " + (results.Count - failed) + " passed, " + failed + " failed" + lines;
        }

        // 홍달 기사 휴대폰의 재연 대본(2026-11-15). 단계가 바뀐 뒤 휴대폰이 무엇을 누르고 보일지의 순수 규칙이며 업무 상태를 바꾸지 않는다.
        private static string RoleAppReplayChecks()
        {
            var results = 역할앱재연Checks.Run();
            var failed = 0;
            var lines = new System.Text.StringBuilder();
            foreach (var (name, ok, detail) in results)
            {
                if (!ok) failed++;
                lines.Append('\n').Append(ok ? "[PASS] " : "[FAIL] ").Append(name);
                if (!string.IsNullOrEmpty(detail)) lines.Append(" — ").Append(detail);
            }
            return "Hongdal role app replay checks: " + (results.Count - failed) + " passed, " + failed + " failed" + lines;
        }

        // 다음 할 일 · 이번 턴 계획(2026-11-16). 신호에서 고를 일을 모으고 계획을 담고 넘기는 순수 규칙이며 업무 상태를 바꾸지 않는다.
        private static string NextTodoChecks()
        {
            var results = 다음할일Checks.Run();
            var failed = 0;
            var lines = new System.Text.StringBuilder();
            foreach (var (name, ok, detail) in results)
            {
                if (!ok) failed++;
                lines.Append('\n').Append(ok ? "[PASS] " : "[FAIL] ").Append(name);
                if (!string.IsNullOrEmpty(detail)) lines.Append(" — ").Append(detail);
            }
            return "Hongdal next todo checks: " + (results.Count - failed) + " passed, " + failed + " failed" + lines;
        }

        // 이어지는 지도(2026-11-16). 전국 · 권역 · 구 · 동을 한 배율 축으로 잇는 순수 규칙이며 업무 상태를 바꾸지 않는다.
        private static string ContinuousMapChecks()
        {
            var results = 이어지는지도Checks.Run();
            var failed = 0;
            var lines = new System.Text.StringBuilder();
            foreach (var (name, ok, detail) in results)
            {
                if (!ok) failed++;
                lines.Append('\n').Append(ok ? "[PASS] " : "[FAIL] ").Append(name);
                if (!string.IsNullOrEmpty(detail)) lines.Append(" — ").Append(detail);
            }
            return "Hongdal continuous map checks: " + (results.Count - failed) + " passed, " + failed + " failed" + lines;
        }

        // 배달 플랫폼 패키지 뼈대(2026-11-16). 패키지 표 · 아래 줄 · 지사 = 동 묶음 · 시간대 필요 기사의 순수 규칙이며 업무 상태를 바꾸지 않는다.
        private static string PlatformPackageChecks()
        {
            var results = 배달플랫폼Checks.Run();
            var failed = 0;
            var lines = new System.Text.StringBuilder();
            foreach (var (name, ok, detail) in results)
            {
                if (!ok) failed++;
                lines.Append('\n').Append(ok ? "[PASS] " : "[FAIL] ").Append(name);
                if (!string.IsNullOrEmpty(detail)) lines.Append(" — ").Append(detail);
            }
            return "Hongdal platform package checks: " + (results.Count - failed) + " passed, " + failed + " failed" + lines;
        }

        // 00 경영 — 오행 하향식(2026-11-17). 다섯 열 · 경로 · 주문 단계의 순수 규칙이며 업무 상태를 바꾸지 않는다.
        private static string FiveElementBoardChecks()
        {
            var results = 오행경영Checks.Run();
            var failed = 0;
            var lines = new System.Text.StringBuilder();
            foreach (var (name, ok, detail) in results)
            {
                if (!ok) failed++;
                lines.Append('\n').Append(ok ? "[PASS] " : "[FAIL] ").Append(name);
                if (!string.IsNullOrEmpty(detail)) lines.Append(" — ").Append(detail);
            }
            return "Hongdal five-element board checks: " + (results.Count - failed) + " passed, " + failed + " failed" + lines;
        }

        // 떴다 사라지는 알림(2026-11-15). 무엇을 얼마 동안 보일지의 순수 규칙이며 업무 상태를 바꾸지 않는다.
        private static string NoticeChecks()
        {
            var results = 알림Checks.Run();
            var failed = 0;
            var lines = new System.Text.StringBuilder();
            foreach (var (name, ok, detail) in results)
            {
                if (!ok) failed++;
                lines.Append('\n').Append(ok ? "[PASS] " : "[FAIL] ").Append(name);
                if (!string.IsNullOrEmpty(detail)) lines.Append(" — ").Append(detail);
            }
            return "Hongdal notice checks: " + (results.Count - failed) + " passed, " + failed + " failed" + lines;
        }

        // 길찾기와 기사 순찰(R2 · R4, 2026-11-14). 손으로 만든 작은 그래프로 보는 순수 규칙이며 업무 상태를 바꾸지 않는다.
        // 음악 파일의 분석 (2026-11-14). 곡 파일을 읽지 않고, 박을 아는 소리를 만들어 본다.
        private static string MusicAnalysisChecks()
        {
            var results = 음악분석Checks.Run();
            var failed = 0;
            var lines = new System.Text.StringBuilder();
            foreach (var (name, ok, detail) in results)
            {
                if (!ok) failed++;
                lines.Append('\n').Append(ok ? "[PASS] " : "[FAIL] ").Append(name);
                if (!string.IsNullOrEmpty(detail)) lines.Append(" — ").Append(detail);
            }
            return "Hongdal music analysis checks: " + (results.Count - failed) + " passed, " + failed + " failed" + lines;
        }

        private static string RouteChecks()
        {
            var results = 길찾기Checks.Run();
            var failed = 0;
            var lines = new System.Text.StringBuilder();
            foreach (var (name, ok, detail) in results)
            {
                if (!ok) failed++;
                lines.Append('\n').Append(ok ? "[PASS] " : "[FAIL] ").Append(name);
                if (!string.IsNullOrEmpty(detail)) lines.Append(" — ").Append(detail);
            }
            return "Hongdal route checks: " + (results.Count - failed) + " passed, " + failed + " failed" + lines;
        }

        // 결정 순회 뼈대(2026-11-13). 화면 순서 규칙이며 업무 상태를 바꾸지 않는다.
        private static string DecisionCycleChecks()
        {
            var results = 결정순회Checks.Run();
            var failed = 0;
            var lines = new System.Text.StringBuilder();
            foreach (var (name, ok, detail) in results)
            {
                if (!ok) failed++;
                lines.Append('\n').Append(ok ? "[PASS] " : "[FAIL] ").Append(name);
                if (!string.IsNullOrEmpty(detail)) lines.Append(" — ").Append(detail);
            }
            return "Hongdal decision cycle checks: " + (results.Count - failed) + " passed, " + failed + " failed" + lines;
        }

        // 3D 기반 A1: 디오라마시점의 계산이 Unity 카메라의 방향과 같은지 본다.
        // 원근이 없는 카메라에서 화면의 오른쪽·위쪽 좌표는 카메라의 오른쪽·위쪽 벡터와의 내적이다.
        private static (string Name, bool Ok, string Detail) LensCameraCheck()
        {
            var worst = 0.0;
            foreach (var (yaw, pitch) in new[] { (30.0, 55.0), (120.0, 55.0), (210.0, 40.0), (300.0, 70.0), (0.0, 90.0) })
            {
                var lens = new 디오라마시점(yaw, pitch);
                var rotation = Quaternion.Euler((float)pitch, (float)yaw, 0);
                var right = rotation * Vector3.right;
                var up = rotation * Vector3.up;
                foreach (var (east, north) in new[] { (100.0, 0.0), (0.0, 100.0), (-250.5, 80.25), (40.0, -900.0) })
                {
                    var world = new Vector3((float)east, 0, (float)north);
                    var (u, v) = lens.Screen(east, north);
                    worst = Math.Max(worst, Math.Max(Math.Abs(Vector3.Dot(world, right) - u), Math.Abs(-Vector3.Dot(world, up) - v)));
                }
            }
            return ("시점 계산이 Unity 카메라 방향과 같음 (다섯 방향)", worst < 0.01, $"가장 큰 차이 {worst:0.00000} m");
        }

        // 행정동 모듈 디오라마(r54 첫 조각, 2026-11-13). 지도 표현 규칙이며 업무 상태를 바꾸지 않는다.
        private static string DongModuleChecks()
        {
            var results = 행정동모듈Checks.Run();
            results.Add(LensCameraCheck());
            var failed = 0;
            var lines = new System.Text.StringBuilder();
            foreach (var (name, ok, detail) in results)
            {
                if (!ok) failed++;
                lines.Append('\n').Append(ok ? "[PASS] " : "[FAIL] ").Append(name);
                if (!string.IsNullOrEmpty(detail)) lines.Append(" — ").Append(detail);
            }
            return "Hongdal dong module checks: " + (results.Count - failed) + " passed, " + failed + " failed" + lines;
        }
    }
}
