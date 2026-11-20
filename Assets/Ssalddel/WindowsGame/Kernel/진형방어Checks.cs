using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ssalddel.WindowsGame.Kernel
{
    /// <summary>게임 화면과 지도 원본을 사용하지 않는 방어 규칙 점검.</summary>
    public static class 진형방어Checks
    {
        private const string Entrance = "entry";
        private const string Target = "target";
        private const long Seed = 20261120;

        public static IReadOnlyList<(string Name, bool Ok, string Detail)> Run()
        {
            var results = new List<(string, bool, string)>();
            foreach (var (kind, label, good, bad, entrance) in FormationExamples())
            {
                Check(results, label + " 성립", () =>
                    Require(Read(good, entrance).Kind == kind, "선택된 모양 " + Read(good, entrance).Kind));
                Check(results, label + " 조건을 벗어나면 미성립", () =>
                    Require(Read(bad, entrance).Kind != kind, "모양이 그대로 유지됨"));
            }
            Check(results, "확충 거리 안에서만 진형이 성립한다", ReachLimits);
            Check(results, "진형은 참여 분대 수가 많고 표에서 가까운 것부터 고른다", FormationPriority);
            Check(results, "오행 상극·같음·상생 배율", ElementRatios);
            Check(results, "방어 등급 경계와 추가 보상 배율", RewardRatios);
            Check(results, "평시에는 알갱이가 없으며 경보가 방어를 연다", QuietBeforeAlarm);
            Check(results, "닫힌 판은 명령을 받지 않는다", EditionGate);
            Check(results, "각성·배달·교육 상태와 옥상 최대 인원을 검사한다", EligibilityAndCapacity);
            Check(results, "배치 미리보기와 중복 요청은 상태를 바꾸지 않는다", PreviewAndReplay);
            Check(results, "건물 안의 버팀과 정화 범위가 옥상과 다르다", Garrison);
            Check(results, "분대장은 방어 경험과 호흡, 이름 순으로 결정한다", Experience);
            Check(results, "원진의 반경 보너스가 경로의 정화 결과에 반영된다", CircleCoversWiderPath);
            Check(results, "버팀 소진으로 물러나면 진형과 범위 보너스가 풀린다", Withdraws);
            Check(results, "진지 공사에는 보급·분대·유효한 자리가 필요하다", FortConstruction);
            Check(results, "막힌 다리는 피해 가고 물길은 지나지 않는다", BridgeDetour);
            Check(results, "울타리 내구를 다 쓰면 알갱이가 통과한다", FenceBreaks);
            Check(results, "저장 후 재개와 연속 실행 결과가 일치한다", RestoreReplay);
            Check(results, "중복 진지와 비어 있는 경로 기록은 기존 저장 상태를 덮지 않는다", RejectInvalidSave);
            Check(results, "방어 종료 뒤 진지·내구를 저장하고 보급으로 보수한다", FortPersistence);
            Check(results, "치른 방어전은 참여한 대원에게 한 번만 더해진다", CountsOnce);
            Check(results, "같은 씨앗의 세 물결과 결과 바이트가 일치한다", SeedRepeat);
            return results;
        }

        public static string RepresentativeResult()
        {
            var setup = Route((-60, 30), (0, 30), (1, 30), (60, 30));
            AddRoof(setup, "roof-1", 0, 0);
            var kernel = Create(setup);
            AddFence(kernel, "fence", "edge-2");
            AddSquad(kernel, "roof");
            Place(kernel, "roof", "roof-1");
            Begin(kernel);
            Finish(kernel);
            return JsonConvert.SerializeObject(State(kernel).Result);
        }

        private static void Check(List<(string, bool, string)> results, string name, Action run)
        {
            try
            {
                run();
                results.Add((name, true, string.Empty));
            }
            catch (Exception error)
            {
                results.Add((name, false, error.Message));
            }
        }

        private static void Require(bool condition, string detail)
        {
            if (!condition) throw new InvalidOperationException(detail);
        }

        private static DefenseVertex[] Vertices(params (double X, double Z)[] points)
        {
            return points.Select((point, i) => new DefenseVertex
            {
                Squad = "s" + i,
                X = point.X,
                Z = point.Z,
                Reach = 210,
                Element = DefenseElement.Metal
            }).ToArray();
        }

        private static FormationRead Read(DefenseVertex[] vertices, DefenseSetup setup)
        {
            return 진형방어Formation.Read(setup, vertices, Entrance);
        }

        private static IEnumerable<(FormationKind Kind, string Label, DefenseVertex[] Good, DefenseVertex[] Bad, DefenseSetup Setup)> FormationExamples()
        {
            var downward = Route((0, -160), (0, 150));
            yield return (FormationKind.Line, "목 직진", Vertices((-80, 0), (0, 0), (80, 0)),
                Vertices((-80, 0), (0, 45), (80, 0)), downward);
            var arrow = Vertices((0, -60), (-35, 20), (35, 20));
            yield return (FormationKind.Arrow, "화 예진", arrow, Vertices((0, 60), (-35, -20), (35, -20)), downward);
            yield return (FormationKind.Square, "토 방진", Vertices((-40, -40), (40, -40), (40, 40), (-40, 40)),
                Vertices((-40, -40), (60, -40), (20, 15), (-40, 40)), downward);
            var circle = Enumerable.Range(0, 5).Select(i => (Math.Cos(i * Math.PI * 2 / 5) * 68,
                Math.Sin(i * Math.PI * 2 / 5) * 68)).ToArray();
            yield return (FormationKind.Circle, "금 원진", Vertices(circle),
                Vertices((-55, 0), (-30, 30), (0, 55), (30, 30), (55, 0)), downward);
            var bend = Route((-85, 0), (0, 0), (0, 100));
            yield return (FormationKind.Curve, "수 곡진", Vertices((-80, 0), (-40, 0), (0, 30), (0, 85)),
                Vertices((-80, 0), (-40, 0), (30, 30), (30, 85)), bend);
        }

        private static void ReachLimits()
        {
            var vertices = Vertices((-80, 0), (0, 0), (80, 0));
            var setup = Route((0, -150), (0, 150));
            foreach (var vertex in vertices) vertex.Reach = 130;
            Require(Read(vertices, setup).Kind == FormationKind.None, "끝 꼭짓점의 점선이 허용됨");
            foreach (var vertex in vertices) vertex.Reach = 170;
            Require(Read(vertices, setup).Kind == FormationKind.Line, "확충 뒤 직진이 서지 않음");
        }

        private static void FormationPriority()
        {
            var bend = Route((-80, 0), (80, 0), (80, 60));
            var vertices = Vertices((-80, 0), (0, 0), (80, 0), (80, 50));
            var selected = Read(vertices, bend);
            Require(selected.Kind == FormationKind.Curve && selected.Squads.Count == 4, "4분대 곡진이 3분대 직진보다 우선하지 않음");

            var squarePath = Route((-40, -55), (-40, 40), (40, 40), (40, -55));
            var square = Vertices((-40, -40), (-40, 40), (40, 40), (40, -40));
            Require(Read(square, squarePath).Kind == FormationKind.Square, "같은 수에서 방진이 곡진보다 우선하지 않음");
        }

        private static void ElementRatios()
        {
            Require(진형방어Policy.ElementMultiplier(DefenseElement.Metal, DefenseElement.Wood) == 1.5, "금극목");
            Require(진형방어Policy.ElementMultiplier(DefenseElement.Metal, DefenseElement.Metal) == 1.0, "금과 금");
            Require(진형방어Policy.ElementMultiplier(DefenseElement.Metal, DefenseElement.Water) == 0.7, "금생수");
        }

        private static void RewardRatios()
        {
            Require(진형방어Policy.Grade(0.95) == "S", "95% 등급");
            Require(진형방어Policy.Grade(0.8) == "A", "80% 등급");
            Require(진형방어Policy.Grade(0.6) == "B", "60% 등급");
            Require(진형방어Policy.Grade(0.599) == "C", "60% 미만 등급");
            Require(진형방어Policy.Reward("A", true, true) == 1.56m, "A+유지+상극");
            Require(진형방어Policy.Reward("S", false, false) == 1.5m, "S 기본");
            Require(진형방어Policy.Reward("C", false, false) == 0.6m, "C 기본");
        }

        private static void QuietBeforeAlarm()
        {
            var kernel = Create(ParallelBridges());
            Reject(kernel, "defense.begin");
            kernel.Tick(20000);
            Require(State(kernel).Battle == null && State(kernel).Result == null, "평시에 방어전이 생성됨");
            Reject(kernel, "defense.alarm", ("entrance", "unknown"), ("element", "Earth"));
            Done(kernel, "defense.alarm", ("entrance", Entrance), ("element", "Earth"));
            kernel.Tick(40000);
            Require(State(kernel).Alarm != null && State(kernel).Battle == null, "시작 전 경보 시각이 진행됨");
            Done(kernel, "defense.begin");
            kernel.Tick(40500);
            Require(State(kernel).Battle.Spawned > 0, "방어전 첫 물결이 시작되지 않음");
        }

        private static void EditionGate()
        {
            var kernel = Create(enabled: false);
            var site = Payload("defense.site", ParallelBridges());
            Require(!kernel.Preview(site).CanConfirm && !kernel.Confirm(site).Ok, "비활성 판에서 시험 지도가 열림");
            kernel.Tick(10000);
            Require(State(kernel).Battle == null, "비활성 판에서 전투가 생성됨");
        }

        private static void EligibilityAndCapacity()
        {
            var setup = ParallelBridges();
            AddRoof(setup, "small", 18, 5, 120);
            var kernel = Create(setup);
            AddSquad(kernel, "ready");
            Reject(kernel, "formation.place", ("squad", "ready"), ("target", "small"), ("kind", "Roof"));

            var sleeping = Person("sleeping");
            sleeping.Awakening = 0;
            var busy = Person("busy");
            busy.Busy = true;
            CannotPlace(kernel, "sleep-squad", sleeping);
            CannotPlace(kernel, "busy-squad", busy);

            Place(kernel, "ready", "roof-1");
            AddSquad(kernel, "second");
            Reject(kernel, "formation.place", ("squad", "second"), ("target", "roof-1"), ("kind", "Roof"));
        }

        private static void CannotPlace(GameKernel kernel, string squad, Defender blocked)
        {
            var command = Payload("squad.set", Squad(squad, blocked, Person(squad + "-a"), Person(squad + "-b")));
            if (kernel.Confirm(command).Ok)
                Reject(kernel, "formation.place", ("squad", squad), ("target", "roof-1"), ("kind", "Roof"));
            Require(!State(kernel).Placements.Any(p => p.Squad == squad), "자격이 없는 대원을 배치함");
        }

        private static void PreviewAndReplay()
        {
            var kernel = Create(ParallelBridges());
            AddSquad(kernel, "staff");
            var command = Command("formation.place", ("squad", "staff"), ("target", "roof-1"), ("kind", "Roof"));
            command.CommandId = "trial-placement";
            var before = kernel.Save();
            Require(kernel.Preview(command).CanConfirm, "배치 미리보기 실패");
            Require(kernel.Save() == before, "미리보기가 저장 상태를 바꿈");
            Require(kernel.Confirm(command).Ok, "배치 확정 실패");
            var applied = kernel.Save();
            Require(kernel.Confirm(command).Replayed, "같은 명령 번호를 새로 처리함");
            Require(kernel.Save() == applied && State(kernel).Placements.Count == 1, "중복 배치가 남음");
        }

        private static void Garrison()
        {
            var setup = Route((-60, 30), (60, 30));
            AddRoof(setup, "roof-1", 0, 0);
            var roof = Create(setup);
            var indoors = Create(setup);
            AddSquad(roof, "staff");
            AddSquad(indoors, "staff");
            Place(roof, "staff", "roof-1");
            Place(indoors, "staff", "roof-1", "Inside");
            Require(State(roof).Placements.Single().Stamina == 3, "호흡이 가장 낮은 대원의 버팀이 3초가 아님");
            Require(State(indoors).Placements.Single().Stamina == 6, "건물 안 버팀이 두 배가 아님");
            Require(진형방어Policy.Formation(State(indoors)).Kind == FormationKind.None, "건물 안에서 진형이 생김");
            Begin(roof);
            Begin(indoors);
            Finish(roof);
            Finish(indoors);
            Require(State(roof).Result.Cleansed > 0 && State(indoors).Result.Cleansed == 0,
                "옥상만 닿아야 하는 거리의 정화 결과가 예상과 다름");
        }

        private static void Experience()
        {
            var novice = Person("novice", 3);
            var rookie = Person("rookie", 1);
            var veteran = Person("veteran", 0);
            veteran.DefenseCount = 5;
            Require(진형방어Policy.Leader(Squad("one", novice, rookie, Person("other"))).Id == novice.Id, "경험 0인 분대의 분대장");
            Require(진형방어Policy.Leader(Squad("one", novice, rookie, veteran)).Id == veteran.Id, "경험이 높은 대원으로 분대장이 바뀌지 않음");
            rookie.DefenseCount = 5;
            Require(진형방어Policy.Leader(Squad("one", novice, rookie, veteran)).Id == rookie.Id, "경험 동률에서 호흡 우선");
            veteran.Breath = rookie.Breath;
            Require(진형방어Policy.Leader(Squad("one", novice, veteran, rookie)).Id == rookie.Id, "경험·호흡 동률에서 이름 우선");
            Require(진형방어Policy.Stars(rookie) == 2, "경험 5회는 별 2개");
        }

        private static GameKernel CircleArena(double routeX, int squadCount)
        {
            var setup = Route((routeX, -180), (routeX, 180));
            for (int i = 0; i < 5; i++)
            {
                double angle = i * Math.PI * 2 / 5;
                AddRoof(setup, "roof-" + i, Math.Cos(angle) * 68, Math.Sin(angle) * 68);
            }
            var kernel = Create(setup);
            for (int i = 0; i < squadCount; i++)
            {
                AddSquad(kernel, "squad-" + i);
                Place(kernel, "squad-" + i, "roof-" + i);
            }
            return kernel;
        }

        private static void CircleCoversWiderPath()
        {
            // 길은 가장 가까운 옥상에서 42m 떨어져 있다. 기본 38m 밖, 원진 49.4m 안이다.
            var circle = CircleArena(110, 5);
            var partial = CircleArena(110, 4);
            Begin(circle);
            Begin(partial);
            Finish(circle);
            Finish(partial);
            Require(State(circle).Result.Cleansed > 0 && State(partial).Result.Cleansed == 0,
                "원의 성립이 실제 정화 거리를 넓히지 않음");
        }

        private static void Withdraws()
        {
            var kernel = CircleArena(68, 5);
            Require(진형방어Policy.Formation(State(kernel)).Kind == FormationKind.Circle, "전투 전 원진이 서지 않음");
            Begin(kernel);
            Finish(kernel);
            var ended = State(kernel);
            Require(ended.Placements.Count < 5 && !ended.Result.KeptFormation, "버팀 소진으로 진형을 잃은 기록이 없음");
            Require(진형방어Policy.Formation(ended).Kind != FormationKind.Circle, "물러난 뒤에도 원진이 유지됨");
        }

        private static void FortConstruction()
        {
            var kernel = Create(ParallelBridges());
            AddSquad(kernel, "builder");
            AddSquad(kernel, "other");
            Reject(kernel, "fort.build", ("id", "bad"), ("kind", "Bridgehead"), ("squad", "builder"), ("target", Entrance));
            Done(kernel, "fort.build", ("id", "bridge-post"), ("kind", "Bridgehead"), ("squad", "builder"), ("target", "a-west"));
            Require(State(kernel).Supply == 0 && State(kernel).Forts.Single().BuildTurnsLeft == 3, "교두 진지의 자재나 공사 턴");
            Reject(kernel, "formation.place", ("squad", "builder"), ("target", "roof-1"), ("kind", "Roof"));
            Reject(kernel, "fort.build", ("id", "too-costly"), ("kind", "Fence"), ("squad", "other"), ("target", "bridge-b"));
            Done(kernel, "defense.turn");
            Done(kernel, "defense.turn");
            Require(State(kernel).Forts.Single().BuildTurnsLeft == 1, "두 턴 뒤 완공까지 한 턴");
            Done(kernel, "defense.turn");
            Require(State(kernel).Forts.Single().BuildTurnsLeft == 0, "세 턴 뒤에도 진지 공사가 끝나지 않음");
            Place(kernel, "builder", "bridge-post", "Fort");
            Require(State(kernel).Placements.Single().Stamina == 6, "교두 진지 안의 버팀은 두 배");
        }

        private static void BridgeDetour()
        {
            var baseline = Create(ParallelBridges());
            Begin(baseline);
            Finish(baseline);
            var direct = State(baseline).Result;
            Require(direct.EdgeEntries.ContainsKey("bridge-a") && !direct.EdgeEntries.ContainsKey("water"), "기본 경로가 짧은 다리를 사용하지 않음");

            var reroute = Create(ParallelBridges());
            AddFence(reroute, "fence-a", "bridge-a");
            Begin(reroute);
            Finish(reroute);
            var result = State(reroute).Result;
            Require(result.EdgeEntries.TryGetValue("bridge-b", out int diverted) && diverted > result.Spawned / 2,
                "울타리가 설치되지 않은 다리로 우회하지 않음");
            Require(!result.EdgeEntries.ContainsKey("water"), "물 고리를 지남");

            var waterOnly = Route((0, 0), (25, 0));
            waterOnly.Edges[0].Water = true;
            var noPath = Create(waterOnly);
            Reject(noPath, "defense.alarm", ("entrance", Entrance), ("element", "Earth"));
        }

        private static void FenceBreaks()
        {
            var kernel = Create(ParallelBridges());
            AddFence(kernel, "fence-a", "bridge-a");
            AddFence(kernel, "fence-b", "bridge-b");
            Begin(kernel);
            Finish(kernel);
            var state = State(kernel);
            Require(state.Forts.Find(fort => fort.Id == "fence-a").Durability == 0, "더 짧은 쪽의 울타리가 무너지지 않음");
            Require(state.Result.EdgeEntries.ContainsKey("bridge-a") && state.Result.Leaked > 0, "막힘이 풀린 다리를 통해 도착하지 않음");
            Require(!state.Result.EdgeEntries.ContainsKey("water"), "두 다리 모두 막히면 물을 지남");
        }

        private static void RestoreReplay()
        {
            var kernel = Create(ParallelBridges());
            AddFence(kernel, "fence-a", "bridge-a");
            AddSquad(kernel, "staff");
            Place(kernel, "staff", "roof-1");
            Begin(kernel);
            kernel.Tick(2755);
            var saved = kernel.Save();
            var middle = State(kernel);
            Require(middle.Battle != null && middle.Battle.Spawned > 0 && middle.Battle.Motes.Count > 0,
                "진행 중인 물결의 저장을 시험해야 함");
            var restarted = Create();
            Require(restarted.Restore(saved), "물결 도중 저장을 읽을 수 없음");
            Require(restarted.Save() == saved, "저장 직후 복원한 내용이 달라짐");
            Finish(kernel);
            Finish(restarted);
            Require(restarted.Save() == kernel.Save(), "재개한 방어와 연속 진행의 최종 저장 상태가 다름");
        }

        private static void RejectInvalidSave()
        {
            var kernel = Create(ParallelBridges());
            AddFence(kernel, "fence-a", "bridge-a");
            Begin(kernel);
            kernel.Tick(1955);
            var module = kernel.Module<진형방어Module>();
            var original = module.Save();

            var duplicateFort = JObject.Parse(original);
            var forts = (JArray)duplicateFort["Forts"];
            forts.Add(forts[0].DeepClone());
            module.Restore(duplicateFort.ToString(Formatting.None));
            Require(module.Save() == original, "중복 진지가 있는 저장을 받아들임");

            var nullEdgeEntries = JObject.Parse(original);
            nullEdgeEntries["Battle"]["EdgeEntries"] = JValue.CreateNull();
            module.Restore(nullEdgeEntries.ToString(Formatting.None));
            Require(module.Save() == original, "경로 기록이 비어 있는 저장을 받아들임");
        }

        private static void FortPersistence()
        {
            var kernel = Create(Route((-60, 0), (0, 0), (1, 0), (60, 0)));
            AddFence(kernel, "fence", "edge-2");
            Begin(kernel);
            Finish(kernel);
            var after = State(kernel).Forts.Single();
            Require(after.Durability > 0 && after.Durability < 진형방어Policy.Durability(FortKind.Fence), "내구 소모가 기록되지 않음");
            var restarted = Create();
            Require(restarted.Restore(kernel.Save()), "방어 종료 저장을 읽을 수 없음");
            Require(State(restarted).Forts.Single().Id == after.Id && State(restarted).Forts.Single().Durability == after.Durability,
                "진지 또는 소모된 내구가 사라짐");
            int supply = State(restarted).Supply;
            Done(restarted, "fort.repair", ("fort", after.Id), ("squad", "crew-fence"));
            Require(State(restarted).Forts.Single().Durability == 진형방어Policy.Durability(FortKind.Fence) &&
                State(restarted).Supply == supply - 진형방어Policy.RepairSupply, "보급을 사용해 내구를 복구하지 않음");
        }

        private static void CountsOnce()
        {
            var kernel = Create(ParallelBridges());
            AddSquad(kernel, "staff");
            AddSquad(kernel, "idle");
            Place(kernel, "staff", "roof-1");
            Begin(kernel);
            Finish(kernel);
            var before = State(kernel);
            Require(before.Squads.Single(squad => squad.Id == "staff").Members.All(person => person.DefenseCount == 1) &&
                before.Squads.Single(squad => squad.Id == "idle").Members.All(person => person.DefenseCount == 0),
                "참가자와 대기자의 경험이 구분되지 않음");
            kernel.Tick(kernel.ElapsedMs + 20000);
            Require(State(kernel).Squads.Single(squad => squad.Id == "staff").Members.All(person => person.DefenseCount == 1),
                "전투가 끝난 뒤 경험이 한 번 더 적힘");
        }

        private static void SeedRepeat()
        {
            var kernel = Create(ParallelBridges());
            Begin(kernel);
            var waves = State(kernel).Battle.WaveSizes;
            Require(waves.Count == 3 && waves.All(count => count > 0), "물결이 세 번이 아님");
            Finish(kernel);
            var result = State(kernel).Result;
            Require(result.Spawned == waves.Sum() && result.Spawned == result.Cleansed + result.Leaked, "알갱이 결과 합이 입력과 다름");
            Require(RepresentativeResult() == RepresentativeResult(), "같은 씨앗의 결과 바이트 차이");
        }

        private static GameKernel Create(DefenseSetup setup = null, bool enabled = true)
        {
            var kernel = new GameKernel();
            kernel.Register(new 진형방어Module(enabled));
            kernel.Start(Seed);
            if (setup != null) Require(kernel.Confirm(Payload("defense.site", setup)).Ok, "시험 지도를 열 수 없음");
            return kernel;
        }

        private static DefenseState State(GameKernel kernel)
        {
            return (DefenseState)kernel.Module<진형방어Module>().Snapshot();
        }

        private static GameCommand Command(string action, params (string Key, string Value)[] values)
        {
            var command = new GameCommand { OsId = 진형방어Module.Id, Action = action };
            foreach (var (key, value) in values) command.With(key, value);
            return command;
        }

        private static GameCommand Payload(string action, object value)
        {
            var command = Command(action);
            command.PayloadJson = JsonConvert.SerializeObject(value);
            return command;
        }

        private static void Done(GameKernel kernel, string action, params (string Key, string Value)[] values)
        {
            var result = kernel.Confirm(Command(action, values));
            Require(result.Ok, action + " — " + result.Message + " (" + result.Code + ")");
        }

        private static void Reject(GameKernel kernel, string action, params (string Key, string Value)[] values)
        {
            var command = Command(action, values);
            Require(!kernel.Preview(command).CanConfirm && !kernel.Confirm(command).Ok, action + " 명령이 거절되어야 함");
        }

        private static DefenseSetup Route(params (double X, double Z)[] points)
        {
            var setup = new DefenseSetup { Target = Target };
            for (int i = 0; i < points.Length; i++)
            {
                var id = i == 0 ? Entrance : i == points.Length - 1 ? Target : "node-" + i;
                setup.Nodes.Add(new DefenseNode { Id = id, X = points[i].X, Z = points[i].Z });
                if (i > 0) setup.Edges.Add(new DefenseEdge { Id = "edge-" + i, A = setup.Nodes[i - 1].Id, B = id });
            }
            return setup;
        }

        private static DefenseSetup ParallelBridges()
        {
            var setup = Route((-120, 0), (-60, 0), (60, 0), (120, 0));
            setup.Nodes[1].Id = "a-west";
            setup.Nodes[2].Id = "a-east";
            setup.Edges.Clear();
            setup.Nodes.Add(new DefenseNode { Id = "b-west", X = -60, Z = 80 });
            setup.Nodes.Add(new DefenseNode { Id = "b-east", X = 60, Z = 80 });
            setup.Edges.Add(new DefenseEdge { Id = "entry-a", A = Entrance, B = "a-west" });
            setup.Edges.Add(new DefenseEdge { Id = "bridge-a", A = "a-west", B = "a-east", Bridge = true });
            setup.Edges.Add(new DefenseEdge { Id = "finish-a", A = "a-east", B = Target });
            setup.Edges.Add(new DefenseEdge { Id = "entry-b", A = Entrance, B = "b-west" });
            setup.Edges.Add(new DefenseEdge { Id = "bridge-b", A = "b-west", B = "b-east", Bridge = true });
            setup.Edges.Add(new DefenseEdge { Id = "finish-b", A = "b-east", B = Target });
            setup.Edges.Add(new DefenseEdge { Id = "water", A = Entrance, B = Target, Water = true });
            AddRoof(setup, "roof-1", -30, 80);
            return setup;
        }

        private static void AddRoof(DefenseSetup setup, string id, double x, double z, double area = 240)
        {
            setup.Sites.Add(new RoofSite { Id = id, X = x, Z = z, Area = area, Height = 10 });
        }

        private static void AddFence(GameKernel kernel, string id, string edge)
        {
            var squad = "crew-" + id;
            AddSquad(kernel, squad);
            Done(kernel, "fort.build", ("id", id), ("kind", "Fence"), ("squad", squad), ("target", edge));
            Done(kernel, "defense.turn");
        }

        private static Defender Person(string id, int breath = 0)
        {
            return new Defender
            {
                Id = id,
                Name = id,
                Awakening = 1,
                ReachLevel = 3,
                Breath = breath,
                Element = DefenseElement.Wood
            };
        }

        private static DefenseSquad Squad(string id, params Defender[] people)
        {
            return new DefenseSquad
            {
                Id = id,
                Members = people.Length == 0
                    ? new List<Defender> { Person(id + "-1"), Person(id + "-2", 1), Person(id + "-3", 2) }
                    : people.ToList()
            };
        }

        private static void AddSquad(GameKernel kernel, string id, params Defender[] people)
        {
            var result = kernel.Confirm(Payload("squad.set", Squad(id, people)));
            Require(result.Ok, "분대 " + id + "를 만들지 못함 — " + result.Message);
        }

        private static void Place(GameKernel kernel, string squad, string roof, string kind = "Roof")
        {
            Done(kernel, "formation.place", ("squad", squad), ("target", roof), ("kind", kind));
        }

        private static void Begin(GameKernel kernel)
        {
            Done(kernel, "defense.alarm", ("entrance", Entrance), ("element", "Earth"));
            Done(kernel, "defense.begin");
        }

        private static void Finish(GameKernel kernel)
        {
            long end = kernel.ElapsedMs + 180000;
            for (long at = kernel.ElapsedMs + 500; at <= end; at += 500)
            {
                kernel.Tick(at);
                if (State(kernel).Result != null) return;
            }
            throw new InvalidOperationException("시험 시간 안에 방어 결과가 나오지 않음");
        }
    }
}
