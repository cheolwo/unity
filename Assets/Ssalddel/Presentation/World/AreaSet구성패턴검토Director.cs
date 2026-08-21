using System;
using System.Linq;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>
    /// 공용 검토실에서 AreaSet과 기준안·변형안, 구조·Synty 표현을 전환한다.
    /// 모든 전환은 로컬 표현 상태이며 서버 상태를 변경하지 않는다.
    /// </summary>
    public sealed class AreaSet구성패턴검토Director : MonoBehaviour
    {
        private static readonly string[] AreaRoles =
            { "NatureHome", "Farm", "Town", "CityHub" };

        [SerializeField] private AreaSet구성패턴검토Root[] patternRoots =
            Array.Empty<AreaSet구성패턴검토Root>();
        [SerializeField] private int selectedAreaIndex;
        [SerializeField] private bool showVariant;
        [SerializeField] private bool showH3 = true;
        [SerializeField] private bool showH2 = true;
        [SerializeField] private bool showConnectors = true;
        [SerializeField] private bool showWiRoutes = true;
        [SerializeField] private bool showSynty = true;

        public AreaSet구성패턴검토Root[] PatternRoots => patternRoots;
        public int SelectedAreaIndex => selectedAreaIndex;
        public bool ShowVariant => showVariant;
        public bool ShowSynty => showSynty;
        public AreaSet구성패턴검토Root SelectedPattern => patternRoots
            .First(value => value.AreaRoleCode == AreaRoles[selectedAreaIndex]
                            && value.PatternKindCode == (showVariant ? "Variant" : "Baseline"));

        public void Configure(AreaSet구성패턴검토Root[] roots)
        {
            patternRoots = roots ?? Array.Empty<AreaSet구성패턴검토Root>();
            selectedAreaIndex = 0;
            showVariant = false;
            showH3 = showH2 = showConnectors = showWiRoutes = showSynty = true;
            Apply();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) SelectArea(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SelectArea(1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SelectArea(2);
            if (Input.GetKeyDown(KeyCode.Alpha4)) SelectArea(3);
            if (Input.GetKeyDown(KeyCode.B)) { showVariant = false; Apply(); }
            if (Input.GetKeyDown(KeyCode.V)) { showVariant = true; Apply(); }
            if (Input.GetKeyDown(KeyCode.T)) { showSynty = !showSynty; Apply(); }
            if (Input.GetKeyDown(KeyCode.H)) { showH3 = !showH3; Apply(); }
            if (Input.GetKeyDown(KeyCode.J)) { showH2 = !showH2; Apply(); }
            if (Input.GetKeyDown(KeyCode.K)) { showConnectors = !showConnectors; Apply(); }
            if (Input.GetKeyDown(KeyCode.L)) { showWiRoutes = !showWiRoutes; Apply(); }
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(18f, 18f, 520f, 300f), GUI.skin.box);
            GUILayout.Label("AreaSet 구성 패턴 검토실");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("1 Nature")) SelectArea(0);
            if (GUILayout.Button("2 Farm")) SelectArea(1);
            if (GUILayout.Button("3 Town")) SelectArea(2);
            if (GUILayout.Button("4 Hub")) SelectArea(3);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("B 기준안")) { showVariant = false; Apply(); }
            if (GUILayout.Button("V 변형안")) { showVariant = true; Apply(); }
            if (GUILayout.Button(showSynty ? "T 구조만" : "T Synty 포함"))
            { showSynty = !showSynty; Apply(); }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            showH3 = GUILayout.Toggle(showH3, "H3 경계");
            showH2 = GUILayout.Toggle(showH2, "H2 경계");
            showConnectors = GUILayout.Toggle(showConnectors, "Connector");
            showWiRoutes = GUILayout.Toggle(showWiRoutes, "WI 동선");
            GUILayout.EndHorizontal();
            if (GUILayout.Button("보기 적용")) Apply();
            var selected = SelectedPattern;
            GUILayout.Label($"{selected.Title}\n{selected.PatternCode}\n"
                            + $"슬롯 {selected.RoleSlotCodes.Length} · "
                            + $"{(showSynty ? "Synty 포함" : "구조만")}");
            GUILayout.EndArea();
        }

        public void SelectArea(int index)
        {
            selectedAreaIndex = Mathf.Clamp(index, 0, AreaRoles.Length - 1);
            Apply();
        }

        public void SetVariant(bool value)
        {
            showVariant = value;
            Apply();
        }

        public void Apply()
        {
            if (patternRoots == null || patternRoots.Length == 0) return;
            var role = AreaRoles[selectedAreaIndex];
            var kind = showVariant ? "Variant" : "Baseline";
            foreach (var root in patternRoots)
            {
                if (root == null) continue;
                var active = root.AreaRoleCode == role && root.PatternKindCode == kind;
                root.gameObject.SetActive(active);
                if (active)
                    root.ApplyVisibility(showH3, showH2, showConnectors,
                        showWiRoutes, showSynty);
            }
        }
    }
}
