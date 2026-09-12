using System.Linq;
using NUnit.Framework;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Unity.Observation;
using Ssalddel.Unity.Presentation.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 동네관찰휴대폰Tests
    {
        private GameObject root;
        private 동네관찰휴대폰View phone;
        private 동네관찰ScreenModel model;
        [SetUp] public void Setup()
        {
            root = new GameObject("전화 UI 시험"); phone = root.AddComponent<동네관찰휴대폰View>(); phone.구성();
            model = 동네관찰Presenter.생성(new 경영SimulationSessionSnapshot {
                DurationTicks = 1800, WaitingFleet = new 가상배달대기Snapshot { NeighborhoodLife = new 가상동네생활Snapshot(), Mart = new 가상마트Snapshot() } });
            phone.표시(model, true, false, false, false, "저장 완료", "1ms");
        }
        [TearDown] public void Teardown() { Object.DestroyImmediate(root); }
        [Test] public void 기본은_닫힘_열기닫기에서_업무명령을_보내지_않는다()
        {
            int commands = 0; phone.명령요청 += _ => commands++;
            Assert.That(phone.IsOpen, Is.False); phone.열기(); Assert.That(phone.IsOpen, Is.True);
            phone.이동(동네휴대폰App.주문); phone.홈(); phone.닫기(); Assert.That(phone.IsOpen, Is.False); Assert.That(commands, Is.Zero);
        }
        [Test] public void 뒤로_홈_목록상세_탐색은_되돌아간다()
        {
            phone.열기(); phone.이동(동네휴대폰App.주문); phone.이동(동네휴대폰App.NPC, "actor:1");
            phone.뒤로(); Assert.That(phone.SelectedApp, Is.EqualTo(동네휴대폰App.주문));
            phone.뒤로(); Assert.That(phone.SelectedApp, Is.EqualTo(동네휴대폰App.홈));
            phone.뒤로(); Assert.That(phone.IsOpen, Is.False);
        }
        [Test] public void 상태갱신은_행을_반복생성하지_않는다()
        {
            phone.열기(); phone.이동(동네휴대폰App.동네); var before = phone.CreatedRowCount;
            for (int i = 0; i < 60; i++) phone.표시(model, i % 2 == 0, false, false, false, "저장 완료", "2ms");
            Assert.That(phone.CreatedRowCount, Is.EqualTo(before));
        }
        [Test] public void 열린패널을_Raycast대상으로_설정한다()
        {
            phone.열기(); Canvas.ForceUpdateCanvases();
            var corners = new Vector3[4]; phone.PhoneRect.GetWorldCorners(corners);
            Assert.That(corners[2].x, Is.GreaterThan(corners[0].x));
            Assert.That(root.GetComponentInChildren<GraphicRaycaster>(), Is.Not.Null);
            Assert.That(phone.PhoneRect.GetComponent<Image>().raycastTarget, Is.True);
            Assert.That(phone.PhoneRect.gameObject.activeInHierarchy, Is.True);
            phone.닫기(); Assert.That(phone.PhoneRect.gameObject.activeInHierarchy, Is.False);
        }
        [Test] public void 여섯앱과_한국어글꼴_스크롤이_있다()
        {
            phone.열기();
            foreach (var name in new[] { "운영지도", "동네", "주문", "NPC", "창고", "정책" })
                Assert.That(root.GetComponentsInChildren<Text>().Any(x => x.text.StartsWith(name + "\n")), Is.True, name);
            Assert.That(root.GetComponentInChildren<ScrollRect>().horizontal, Is.False);
            Assert.That(root.GetComponentsInChildren<Text>().All(x => x.font != null), Is.True);
        }
        [Test] public void 운영지도는_실제AreaSet요청과_생활현장복귀만_전달한다()
        {
            string selected = null; var returned = 0;
            phone.AreaSet관찰요청 += value => selected = value;
            phone.생활현장복귀요청 += () => returned++;
            phone.운영지도표시(new[]
            {
                new 운영지도AreaViewModel { StableId = "areaset:nature", DisplayName = "Nature 생활 거점", Selectable = true, Selected = true, Reason = "관찰 가능" },
                new 운영지도AreaViewModel { StableId = "areaset:farm", DisplayName = "Farm 생산", Selectable = false, Reason = "미확보" },
            }, "시험 network");
            phone.열기(); phone.이동(동네휴대폰App.운영지도);

            root.GetComponentsInChildren<Button>().Single(x => x.name == "카드 · ● Nature 생활 거점").onClick.Invoke();
            Assert.That(selected, Is.EqualTo("areaset:nature"));
            Assert.That(root.GetComponentsInChildren<Button>().Single(x => x.name == "카드 · Farm 생산").interactable, Is.False);
            root.GetComponentsInChildren<Button>().Single(x => x.name == "카드 · 현재 생활 현장으로 복귀").onClick.Invoke();
            Assert.That(returned, Is.EqualTo(1));
        }
        [Test] public void 완료표본_재시작은_두번확인해야_명령이_나간다()
        {
            int calls = 0; phone.명령요청 += command => { if (command == "restart") calls++; };
            phone.표시(model, false, false, false, true, "완료", ""); phone.열기(); phone.이동(동네휴대폰App.동네);
            root.GetComponentsInChildren<Button>().Single(x => x.name == "카드 · 새 30분 표본").onClick.Invoke(); Assert.That(calls, Is.Zero);
            root.GetComponentsInChildren<Button>().Single(x => x.name == "카드 · 새 표본 시작 확인").onClick.Invoke(); Assert.That(calls, Is.EqualTo(1));
        }
        [Test] public void 자동저장중_정지와_정책초안_수동요청이_가능하다()
        {
            phone.표시(model, true, true, false, false, "저장 중", ""); phone.열기(); phone.이동(동네휴대폰App.동네);
            Assert.That(root.GetComponentsInChildren<Button>().Single(x => x.name == "카드 · 일시정지").interactable, Is.True);
            Assert.That(root.GetComponentsInChildren<Button>().Single(x => x.name == "카드 · 지금 저장").interactable, Is.True);
            phone.이동(동네휴대폰App.정책);
            Assert.That(root.GetComponentsInChildren<Button>().First(x => x.name.StartsWith("카드 · 조리 시간")).interactable, Is.True);
            phone.표시(model, true, true, false, false, "대기 중", "", true);
            Assert.That(root.GetComponentsInChildren<Button>().First(x => x.name.StartsWith("카드 · 조리 시간")).interactable, Is.False);
        }

        [Test] public void 음식생활카드는_상세탐색만하고_추적은_사용자가다시선택한다()
        {
            model.FoodLifeCycles = new[] { new 동네음식생활CycleModel {
                OrderId = "food:test", OrdererActorId = "participant:synthetic:a",
                RestaurantActorId = 가상동네생활기준.RestaurantActorId,
                DriverActorId = "actor:synthetic-courier:1", CurrentFacilityId = "home-a",
                Title = "음식 생활 · 주민 A · 배달 중", Body = "주문자 주민 A · 음식점 음식점 주인 · 배달 기사 1" } };
            model.Orders = new[] { new 동네관찰Row { Id = "food:test", Title = "음식 주문 1 · 주민 A",
                ActorId = "actor:synthetic-courier:1", FacilityId = "restaurant" } };
            model.Actors = new[] {
                new 동네관찰Row { Id = "participant:synthetic:a", Title = "주민 A", ActorId = "participant:synthetic:a", FacilityId = "home-a" },
                new 동네관찰Row { Id = 가상동네생활기준.RestaurantActorId, Title = "음식점 주인", ActorId = 가상동네생활기준.RestaurantActorId, FacilityId = "restaurant" },
                new 동네관찰Row { Id = "actor:synthetic-courier:1", Title = "배달 기사 1", ActorId = "actor:synthetic-courier:1" } };
            string focused = null; phone.주체관찰요청 += value => focused = value;
            phone.표시(model, true, false, false, false, "진행 중", "");
            phone.열기(); phone.이동(동네휴대폰App.동네);

            root.GetComponentsInChildren<Button>().Single(x => x.name == "카드 · 음식 생활 · 주민 A · 배달 중").onClick.Invoke();
            Assert.That(phone.SelectedApp, Is.EqualTo(동네휴대폰App.주문)); Assert.That(focused, Is.Null);
            root.GetComponentsInChildren<Button>().Single(x => x.name == "카드 · 주문자 · 주민 A").onClick.Invoke();
            Assert.That(phone.SelectedApp, Is.EqualTo(동네휴대폰App.NPC)); Assert.That(focused, Is.Null);
            root.GetComponentsInChildren<Button>().Single(x => x.name == "카드 · 이 NPC 따라가기").onClick.Invoke();
            Assert.That(focused, Is.EqualTo("participant:synthetic:a"));
        }
    }
}
