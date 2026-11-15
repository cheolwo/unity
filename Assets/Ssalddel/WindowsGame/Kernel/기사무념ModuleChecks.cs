using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Ssalddel.WindowsGame.Kernel
{
    // 기사 무념 모듈을 실제 음식 배달 모듈 옆에 꽂아 보는 검사. 빌드의 kernel-checks.txt에 붙는다.
    // 음식 배달 응답이 모듈을 꽂기 전과 글자까지 같은지, 저장했다 되살려도 같은지를 본다.
    public static class 기사무념ModuleChecks
    {
        public static List<(string Name, bool Ok, string Detail)> Run(string sampleJson, string roadJson)
        {
            var results = new List<(string, bool, string)>();
            void Check(string name, bool ok, string detail = null) => results.Add((name, ok, detail));
            try
            {
                Body(Check, sampleJson, roadJson);
            }
            catch (Exception exception)
            {
                Check("기사 무념 모듈", false, exception.GetType().Name + ": " + exception.Message);
            }
            return results;
        }

        private static void Body(Action<string, bool, string> check, string sampleJson, string roadJson)
        {
            const string start = "{\"action\":\"start\",\"sessionId\":\"kernel-check-mind\"}";
            // 기준: 음식 배달만 있는 커널.
            var plainKernel = new GameKernel();
            var plainFood = new FoodDeliveryModule(sampleJson, roadJson);
            plainKernel.Register(plainFood);
            plainKernel.Start(1);
            plainKernel.Confirm(new GameCommand { OsId = BusinessIds.FoodDelivery, Action = "start", PayloadJson = start });

            var kernel = new GameKernel();
            var food = new FoodDeliveryModule(sampleJson, roadJson);
            var mind = new 기사무념Module();
            kernel.Register(food);
            kernel.Register(mind);
            kernel.Start(1);
            kernel.Confirm(new GameCommand { OsId = BusinessIds.FoodDelivery, Action = "start", PayloadJson = start });
            Func<기사무념Snapshot> look = () => (기사무념Snapshot)mind.Snapshot();

            var first = look();
            check("모듈: 회차를 열면 기사마다 경 시간이 가득 차 있다",
                first.Observed && first.Riders.Count >= 2 && first.Riders.All(item => item.Remaining == item.Capacity && !item.Broken),
                string.Join(", ", first.Riders.Select(item => item.GradeName + " " + 기사무념Policy.Duration(item.Remaining))));

            var riderId = first.Riders.Count == 0 ? string.Empty : first.Riders[0].Id;
            Func<string, GameCommand> checkCommand = id => new GameCommand
            {
                OsId = 기사무념Module.Id, Action = "check", CommandId = id,
            }.With("rider", riderId);
            var checkedOnce = kernel.Confirm(checkCommand("mind-check-1"));
            var replay = kernel.Confirm(checkCommand("mind-check-1"));
            var second = kernel.Confirm(checkCommand("mind-check-2"));
            var unknown = kernel.Confirm(new GameCommand { OsId = 기사무념Module.Id, Action = "check", CommandId = "mind-check-x" }.With("rider", "없는-기사"));
            check("모듈: 출발 전 점검은 한 번만 적힌다",
                checkedOnce.Ok && replay.Replayed && second.Ok && second.Code == "AlreadyChecked" && !unknown.Ok && look().Find(riderId).CheckedToday,
                checkedOnce.Message);

            var same = true;
            long elapsed = 0;
            var assigned = new HashSet<string>();
            var settled = false;
            var sawWorking = false;
            for (var step = 0; step < 3600 && !settled; step++)
            {
                elapsed += 1000;
                plainKernel.Tick(elapsed);
                if (!kernel.Tick(elapsed)) break;
                same &= plainFood.LastReplyJson == food.LastReplyJson;
                var state = JObject.Parse(food.LastReplyJson)["state"];
                if (state == null || state.Type != JTokenType.Object) break;
                var orders = state["orders"] as JArray ?? new JArray();
                var couriers = state["couriers"] as JArray ?? new JArray();
                foreach (var order in orders)
                {
                    var orderId = (string)order["id"];
                    if ((string)order["stage"] != "Waiting" || !assigned.Add(orderId)) continue;
                    var storeId = (string)order["storeId"] ?? string.Empty;
                    var letter = storeId.Length == 0 ? string.Empty : storeId.Substring(storeId.Length - 1);
                    var driver = couriers.Select(courier => (string)courier["id"])
                        .FirstOrDefault(id => !string.IsNullOrEmpty(id) && id.EndsWith(letter, StringComparison.OrdinalIgnoreCase))
                        ?? (string)order["recommendedDriverId"];
                    var assign = new JObject
                    {
                        ["action"] = "assign",
                        ["elapsedMs"] = elapsed,
                        ["orderId"] = orderId,
                        ["driverId"] = driver,
                        ["commandId"] = "kernel-check-mind-" + orderId,
                        ["revision"] = (long?)order["revision"] ?? 0,
                        ["policy"] = string.Empty,
                    }.ToString(Newtonsoft.Json.Formatting.None);
                    var a = plainKernel.Confirm(new GameCommand { OsId = BusinessIds.FoodDelivery, Action = "assign", PayloadJson = assign }).RawJson;
                    var b = kernel.Confirm(new GameCommand { OsId = BusinessIds.FoodDelivery, Action = "assign", PayloadJson = assign }).RawJson;
                    same &= a == b;
                }
                if (look().Riders.Any(item => item.Working)) sawWorking = true;
                settled = (bool?)state["settled"] == true;
            }
            check("모듈: 꽂아도 음식 배달의 응답은 글자까지 같다", same && settled, "명령 " + assigned.Count + "건 · " + elapsed / 1000 + "초");

            var done = look();
            var expected = done.Riders.Sum(item => item.Settled);
            check("모듈: 배달한 만큼 경 시간이 줄고 정산 건수가 주문 수와 같다",
                sawWorking && expected == assigned.Count && done.MaintainedMinutes > 0 && done.LinkedIncome > 0 &&
                done.Riders.All(item => Math.Abs(item.Capacity - item.Used - item.Remaining) < 0.0001 || item.Broken),
                "경 유지 " + 기사무념Policy.Duration(done.MaintainedMinutes) + " · 이어진 보수 " + done.LinkedIncome + "원 · 정산 " + expected + "건");

            var preview = kernel.Preview(new GameCommand { OsId = 기사무념Module.Id, Action = "assign" }.With("rider", riderId));
            var view = done.Find(riderId);
            check("모듈: 배차 판정이 사본과 같다",
                preview.CanConfirm == !view.Broken && (preview.BlockCode == 기사무념배차판정.Low) == view.Low, preview.BlockCode);

            // 저장 → 새 커널에서 음식 배달과 함께 되살리기(실행판의 순서와 같다).
            var foodSave = (string)JObject.Parse(food.Query("{\"action\":\"save\"}"))["saveJson"];
            var kernelSave = kernel.Save();
            var restoredKernel = new GameKernel();
            var restoredFood = new FoodDeliveryModule(sampleJson, roadJson);
            var restoredMind = new 기사무념Module();
            restoredKernel.Register(restoredFood);
            restoredKernel.Register(restoredMind);
            restoredKernel.Start(1);
            var restoredReply = restoredKernel.Confirm(new GameCommand
            {
                OsId = BusinessIds.FoodDelivery, Action = "restore",
                PayloadJson = new JObject { ["action"] = "restore", ["saveJson"] = foodSave }.ToString(Newtonsoft.Json.Formatting.None),
            });
            var restoredOk = restoredReply.Ok && restoredKernel.Restore(kernelSave);
            var back = restoredOk ? (기사무념Snapshot)restoredMind.Snapshot() : null;
            check("모듈: 저장했다가 되살려도 같다(점검·경 시간·정산)",
                restoredOk && back.Riders.Count == done.Riders.Count && back.Riders.Zip(done.Riders, (x, y) =>
                    x.Id == y.Id && x.Grade == y.Grade && x.Capacity == y.Capacity && x.CheckedToday == y.CheckedToday &&
                    Math.Abs(x.Maintained - y.Maintained) < 0.0001 && x.Settled == y.Settled && x.Linked == y.Linked).All(ok => ok),
                restoredOk ? "경 유지 " + 기사무념Policy.Duration(back.MaintainedMinutes) + " · 점검 " + back.Riders.Count(item => item.CheckedToday) + "명"
                    : "되살리지 못했습니다: " + restoredReply.Message);

            // 이 칸이 없던 예전 저장: 처음 값으로 시작하고 음식 배달의 사건 기록에서 오늘 업무를 다시 읽는다.
            var oldSave = JObject.Parse(kernelSave);
            var modules = oldSave["modules"] as JObject;
            if (modules != null) modules.Remove(기사무념Module.Id);
            var oldKernel = new GameKernel();
            var oldFood = new FoodDeliveryModule(sampleJson, roadJson);
            var oldMind = new 기사무념Module();
            oldKernel.Register(oldFood);
            oldKernel.Register(oldMind);
            oldKernel.Start(1);
            oldKernel.Confirm(new GameCommand
            {
                OsId = BusinessIds.FoodDelivery, Action = "restore",
                PayloadJson = new JObject { ["action"] = "restore", ["saveJson"] = foodSave }.ToString(Newtonsoft.Json.Formatting.None),
            });
            var oldOk = oldKernel.Restore(oldSave.ToString(Newtonsoft.Json.Formatting.None));
            var old = (기사무념Snapshot)oldMind.Snapshot();
            check("모듈: 이 칸이 없던 예전 저장도 열리고 오늘 업무는 사건 기록에서 다시 읽는다",
                oldOk && old.Riders.Count == done.Riders.Count && Math.Abs(old.MaintainedMinutes - done.MaintainedMinutes) < 0.0001 &&
                old.Riders.All(item => !item.CheckedToday),
                "경 유지 " + 기사무념Policy.Duration(old.MaintainedMinutes));
        }
    }
}
