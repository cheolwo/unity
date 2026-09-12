using System;
using UnityEditor;
using UnityEngine;

namespace Ssalddel.Unity.Editor
{
    /// <summary>다음 Play의 프로필만 선택한다. Scene 저장·자동 Play 진입은 하지 않는다.</summary>
    public static class 음식점관찰ProfileMenu
    {
        [MenuItem("Ssalddel/동네 관찰/업무 서버 · 5분 검증", false, 1)]
        private static void BusinessServer()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Environment.SetEnvironmentVariable("SSALDDEL_RESTAURANT_OBSERVER", null);
            Environment.SetEnvironmentVariable("SSALDDEL_SYNTHETIC_DELIVERY", "6");
            Debug.Log("업무 서버 관찰 선택 · Local Runtime 없음 · 휴대폰의 명시적 시작만 실행");
        }
        [MenuItem("Ssalddel/동네 관찰/기본 동네 · NPC 생활과 확장", false, 0)]
        private static void NeighborhoodLife()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Environment.SetEnvironmentVariable("SSALDDEL_RESTAURANT_OBSERVER", null);
            Environment.SetEnvironmentVariable("SSALDDEL_SYNTHETIC_DELIVERY", "5");
            Debug.Log("30분 직업 생활 선택 · 별도 저장 · 기존 표본 보존 · 운영 효과 없음");
        }
        [MenuItem("Ssalddel/동네 관찰/이전 실험/NPC 주문·음식점 수락·배차")]
        private static void OrderFlow()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Environment.SetEnvironmentVariable("SSALDDEL_RESTAURANT_OBSERVER", null);
            Environment.SetEnvironmentVariable("SSALDDEL_SYNTHETIC_DELIVERY", "4");
            Debug.Log("r4 주문 원장 프로필 선택 · 기존 저장 보존 · 서버 통신 없음");
        }
        [MenuItem("Ssalddel/동네 관찰/이전 실험/살뜰마트 피킹·포장·배송")]
        private static void MartCenter()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Environment.SetEnvironmentVariable("SSALDDEL_RESTAURANT_OBSERVER", null);
            Environment.SetEnvironmentVariable("SSALDDEL_SYNTHETIC_DELIVERY", "3");
            Debug.Log("살뜰마트 배송센터 선택 · r3 전용 저장 · 작업자1명/기사3명 · 운영 효과 없음");
        }
        [MenuItem("Ssalddel/동네 관찰/이전 실험/마트·도로변 기사 대기")]
        private static void WaitingFleet()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Environment.SetEnvironmentVariable("SSALDDEL_RESTAURANT_OBSERVER", null);
            Environment.SetEnvironmentVariable("SSALDDEL_SYNTHETIC_DELIVERY", "2");
            Debug.Log("기사3명 대기 선택 · r2 전용 저장 · 기존 단일 기사 저장 보존");
        }
        [MenuItem("Ssalddel/동네 관찰/이전 실험/가상 동네 배달 관찰")]
        private static void Delivery()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Environment.SetEnvironmentVariable("SSALDDEL_RESTAURANT_OBSERVER", null);
            Environment.SetEnvironmentVariable("SSALDDEL_SYNTHETIC_DELIVERY", "1");
            Debug.Log("가상 배달 선택 · SimulationWorldShell에서 Play. 재생을 누르면 진행하며 매 Tick 저장합니다.");
        }
        [MenuItem("Ssalddel/동네 관찰/이전 실험/음식점 관찰 (수동 Tick)")]
        private static void Select() => Set("1");
        [MenuItem("Ssalddel/동네 관찰/이전 실험/기존 기본 프로필")]
        private static void Default() => Set(null);
        private static void Set(string value)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { Debug.LogWarning("Play를 중지한 뒤 실행 프로필을 선택하세요."); return; }
            Environment.SetEnvironmentVariable("SSALDDEL_RESTAURANT_OBSERVER", value);
            Environment.SetEnvironmentVariable("SSALDDEL_SYNTHETIC_DELIVERY", null);
            Debug.Log(value == "1" ? "음식점 관찰 선택 · SimulationWorldShell에서 직접 Play하세요. 이 Editor 프로세스에만 적용됩니다."
                : "기존 프로필 복귀 · 음식점 저장은 보존됩니다.");
        }
    }
}
