using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 빛이 몰렸을 때 (2026-11-14, 사용자: 매장이 많아 빛이 겹치면 구분하기 어렵다 → 「1과 2 적용」).
    //  1. 가운데 점은 늘 따로 보이게: 빛마다의 가운데 점은 모든 빛의 연한 부분을 그린 뒤 맨 위에 그리고, 그 한가운데에 작은 흰 점을 찍는다(GlowCore → FlushGlowCores).
    //     빛이 몰린 곳에서는 연한 부분만 옅게 한다. 지도 위의 빛 한 점(GlowCrowd)과 땅의 빛(GroundCrowd) 둘 다다.
    //     크기의 상한은 명덕으로 크기가 바뀌는 걸음(명1)에서 둔다. 지금의 빛은 크기가 정해져 있다.
    //  2. 숨의 박자를 빛마다 엇갈리게: 이웃한 빛이 번갈아 퍼진다(LightPhase). 엇갈림은 빛마다 정해져 있어 프레임마다 흔들리지 않는다.
    //     땅의 빛은 잔잔하게 두었다(셰이더의 음식점 · 전달지 숨을 줄였다).
    //  - 표현만 바뀐다. 시간 · 돈 · 주문은 그대로다.
    //  - 몰린 정도는 바로 앞 그림의 빛 자리로 잰다(한 프레임 늦어도 눈에 띄지 않는다).
    public sealed partial class SagajeongDispatchView
    {
        private bool lightSeparate = true;            // 끄면 전과 같다(방향 촬영의 전후 비교용)
        private readonly List<(Vector2 Centre, float Size, Color Colour)> glowCores = new();
        private readonly List<Vector2> glowSpots = new();          // 이번 그림의 빛 자리
        private readonly List<Vector2> glowSpotsLast = new();      // 앞 그림의 빛 자리
        private readonly List<Vector2> crowdGround = new();        // 땅의 빛: 음식점 빛의 자리 (m)
        private float glowCrowdMin = 1f, glowCrowdMinShown = 1f;
        private int glowCoresShown;

        // 빛의 연한 부분에 곱할 값. 앞 그림에서 이 빛의 둘레(reach, 논리 픽셀)에 다른 빛이 몇 개 있었나.
        private float GlowCrowd(Vector2 centre, float reach)
        {
            if (!lightSeparate) return 1f;
            var count = 0;
            var reach2 = reach * reach;
            foreach (var spot in glowSpotsLast)
            {
                var gap = (spot - centre).sqrMagnitude;
                if (gap > 1f && gap < reach2) count++;
            }
            var crowd = 기사불빛Policy.Crowd(count);
            if (crowd < glowCrowdMin) glowCrowdMin = crowd;
            return crowd;
        }

        // 빛의 가운데 점. 나누어 그리면 맨 위에 모아 그린다.
        private void GlowCore(Vector2 centre, float size, Color colour)
        {
            if (Event.current.type != EventType.Repaint) return;
            glowSpots.Add(centre);
            if (lightSeparate) glowCores.Add((centre, size, colour));
            else GlowNow(centre, size, colour);
        }

        // DrawBoard의 처음: 지난 그림에서 남은 것을 비운다.
        private void BeginGlowFrame()
        {
            if (Event.current.type != EventType.Repaint) return;
            glowCores.Clear();
            glowSpots.Clear();
            glowCrowdMin = 1f;
        }

        // DrawGlowName에서(모든 빛을 그린 뒤, 이름표 아래): 가운데 점을 그리고 이번 빛의 자리를 다음 그림에 넘긴다.
        private void FlushGlowCores()
        {
            foreach (var core in glowCores)
            {
                GlowNow(core.Centre, core.Size, core.Colour);
                GlowNow(core.Centre, core.Size * 0.45f, Fade(Paper, 0.85f));      // 가장 가운데의 작은 흰 점: 몰려도 하나하나 보인다
            }
            glowCoresShown = glowCores.Count;
            glowCores.Clear();
            glowSpotsLast.Clear();
            glowSpotsLast.AddRange(glowSpots);
            glowSpots.Clear();
            glowCrowdMinShown = glowCrowdMin;
            glowCrowdMin = 1f;
        }

        // 땅의 빛: 음식점 빛이 몰린 곳은 옅게 놓는다. crowdGround는 이번에 놓을 음식점 빛의 자리 (StepRoamLight).
        private float GroundCrowd(Vector2 spot)
        {
            if (!lightSeparate) return 1f;
            var count = 0;
            var reach = 기사불빛Policy.StoreRadius * 1.2f;
            var reach2 = reach * reach;
            foreach (var other in crowdGround)
            {
                var gap = (other - spot).sqrMagnitude;
                if (gap > 1f && gap < reach2) count++;
            }
            return 기사불빛Policy.Crowd(count);
        }

        // 빛마다의 숨의 엇갈림. 나누어 보지 않으면 0(자리에 따라서만 엇갈린다).
        private float LightPhase(uint key) => lightSeparate ? 기사불빛Policy.Phase(key) : 0f;
        private float LightPhase(string key) => lightSeparate ? 기사불빛Policy.Phase(key) : 0f;

        private Vector2 BuildingMid(int building)
        {
            var shape = buildingShapes[building];
            return (shape.Min + shape.Max) * 0.5f;
        }

        // 방향 촬영: 음식점 빛이 몰린 곳. 고른 동에서 가상 자리를 촬영에서만 40곳 더 켜고(제휴를 맺은 것이 아니다),
        // 나누어 그린 모습과 전의 모습을 같은 자리에서 찍는다. 시계와 원장은 그대로여야 한다.
        private IEnumerator ViewProofCrowd(string directory, ViewProofReceipt receipt)
        {
            holdProofClock = true;
            actorFocus = null;
            selectedBuilding = null;
            lightMenu = null;
            dongDeskOpen = false;
            var elapsedBefore = engine.Kernel.ElapsedMs;
            var ledgerBefore = engine.Kernel.Ledger.Count;
            var region = BusinessDong()?.Entry.regionId;
            var extra = new List<int>();
            var centre = Vector2.zero;
            if (!string.IsNullOrEmpty(region) && managedCandidates.TryGetValue(region, out var candidates) && candidates.Count > 0)
            {
                // 이미 켠 자리들의 가운데(없으면 첫 후보)에서 가까운 후보부터. 한 골목에 음식점이 몰린 모습이다.
                var mine = new List<int>();
                foreach (var building in managedPartnerSites)
                    if (building >= 0 && building < buildingShapes.Length && buildingShapes[building].Record.region == region) mine.Add(building);
                if (mine.Count > 0)
                {
                    foreach (var building in mine) centre += BuildingMid(building);
                    centre /= mine.Count;
                }
                else centre = BuildingMid(candidates[0]);
                var near = new List<int>();
                foreach (var building in candidates)
                    if (building >= 0 && building < buildingShapes.Length && !managedPartnerSites.Contains(building) && !roleByBuilding.ContainsKey(building))
                        near.Add(building);
                near.Sort((m, n) => (BuildingMid(m) - centre).sqrMagnitude.CompareTo((BuildingMid(n) - centre).sqrMagnitude));
                for (var i = 0; i < near.Count && extra.Count < 40; i++) extra.Add(near[i]);
            }
            managedPartnerSites.UnionWith(extra);

            // 몰린 정도와 박자의 엇갈림을 잰다(같은 동의 음식점 빛).
            var sites = new List<int>();
            foreach (var building in managedPartnerSites)
                if (building >= 0 && building < buildingShapes.Length && buildingShapes[building].Record.region == region) sites.Add(building);
            receipt.crowdLights = sites.Count;
            float before = 0f, after = 0f;
            var pairs = 0;
            for (var i = 0; i < sites.Count; i++)
            {
                var a = BuildingMid(sites[i]);
                var neighbours = 0;
                for (var j = 0; j < sites.Count; j++)
                {
                    if (i == j) continue;
                    var b = BuildingMid(sites[j]);
                    var gap = (a - b).sqrMagnitude;
                    if (gap < 80f * 80f) neighbours++;
                    if (j <= i || gap >= 60f * 60f) continue;
                    var pa = (a.x * 0.02f + a.y * 0.014f) * StoreLag;
                    var pb = (b.x * 0.02f + b.y * 0.014f) * StoreLag;
                    before += 기사불빛Policy.PhaseGap(pa, pb);
                    after += 기사불빛Policy.PhaseGap(pa + 기사불빛Policy.Phase((uint)sites[i]), pb + 기사불빛Policy.Phase((uint)sites[j]));
                    pairs++;
                }
                if (neighbours > receipt.crowdNeighbours) receipt.crowdNeighbours = neighbours;
            }
            receipt.crowdPairs = pairs;
            receipt.crowdPhaseBefore = pairs == 0 ? 0f : before / pairs;
            receipt.crowdPhaseAfter = pairs == 0 ? 0f : after / pairs;

            if (extra.Count > 0)
                ViewProofCentre(buildingIndex.origin.latitude + centre.y / buildingIndex.metresPerDegreeLatitude,
                    buildingIndex.origin.longitude + centre.x / buildingIndex.metresPerDegreeLongitude, mapZoom * 2.6f);

            // 나누어 그린 모습을 먼저 찍는다(땅의 빛은 더 밝은 쪽을 따르므로, 옅은 쪽을 먼저 놓아야 전후가 섞이지 않는다).
            lightSeparate = true;
            roamLightDue = 0.1f;
            StepRoamLight();
            ShowRoamLight();
            yield return null;
            yield return null;
            yield return null;
            receipt.crowdDimMin = glowCrowdMinShown;
            receipt.crowdCoresOnTop = glowCoresShown;
            yield return SaveFrame(directory, "47-crowd-after.png");
            // 숨이 엇갈리는 모습: 실제 시간으로 조금씩 사이를 두고 여러 장(움직이는 그림을 만들려고).
            for (var shot = 0; shot < 12; shot++)
            {
                yield return new WaitForSecondsRealtime(0.12f);
                yield return SaveFrame(directory, "48a-crowd-after-t" + shot.ToString("00") + ".png");
            }

            lightSeparate = false;
            roamLightDue = 0.1f;
            StepRoamLight();
            ShowRoamLight();
            yield return null;
            yield return null;
            yield return null;
            yield return SaveFrame(directory, "46-crowd-before.png");
            for (var shot = 0; shot < 12; shot++)
            {
                yield return new WaitForSecondsRealtime(0.12f);
                yield return SaveFrame(directory, "48b-crowd-before-t" + shot.ToString("00") + ".png");
            }

            lightSeparate = true;
            managedPartnerSites.ExceptWith(extra);
            managedStamp = int.MinValue;          // 다음 프레임에 관리 건물을 다시 맞춘다
            roamLightDue = 30f;                   // 촬영에서 켠 땅의 빛을 식힌다
            StepRoamLight();
            ShowRoamLight();
            receipt.crowdUnchanged = engine.Kernel.ElapsedMs == elapsedBefore && engine.Kernel.Ledger.Count == ledgerBefore;
            holdProofClock = false;
        }
    }
}
