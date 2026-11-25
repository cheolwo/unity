using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Ssalddel.Simulation.Contracts;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>가상 센터·선반의 명시 ID를 표현한다. 기존 Hub 자산이나 업무 상태를 변경하지 않는다.</summary>
    public sealed class 다중센터출고선반Binding : MonoBehaviour
    {
        private readonly Dictionary<string, Transform> centers = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Transform> locations = new(StringComparer.Ordinal);
        private readonly Dictionary<string, TextMesh> stockLabels = new(StringComparer.Ordinal);
        private readonly Dictionary<string, TextMesh> workerLabels = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Renderer> workerMarkers = new(StringComparer.Ordinal);
        private readonly Dictionary<Renderer, Color> colors = new();
        private readonly Dictionary<string, List<Renderer>> centerMarks = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<Renderer>> locationMarks = new(StringComparer.Ordinal);
        private readonly List<Material> materials = new();
        private readonly List<TextMesh> labels = new();
        private Transform ownedRoot;
        private Font font;
        private Camera labelCamera;
        private string scenarioId, configurationRevision;
        private MaterialPropertyBlock properties;

        public Bounds 관찰Bounds { get; private set; }
        public bool 구성됨 => ownedRoot != null;
        public string 선택센터코드 { get; private set; } = "";
        public string 선택위치코드 { get; private set; } = "";
        public string[] 센터코드목록 => centers.Keys.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        public string[] 위치코드목록 => locations.Keys.OrderBy(value => value, StringComparer.Ordinal).ToArray();

        public void 구성(SimulationFulfillmentStateSnapshot state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (구성됨) throw new InvalidOperationException("출고 모형은 한 번만 구성할 수 있습니다.");
            if (state.Centers == null || state.Locations == null || state.Workers == null || state.Stocks == null)
                throw new InvalidOperationException("출고 공간의 상태 자료가 없습니다.");
            scenarioId = state.ScenarioStableId; configurationRevision = state.ConfigurationRevision;
            ownedRoot = new GameObject("출고 연습 · 별도 가상 센터 모형").transform;
            ownedRoot.SetParent(transform, false); ownedRoot.gameObject.layer = gameObject.layer;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 48);
            var ordered = state.Centers.OrderBy(value => value.CenterStableId, StringComparer.Ordinal).ToArray();
            for (var index = 0; index < ordered.Length; index++)
            {
                var center = ordered[index];
                var root = new GameObject("모의 센터 · " + center.CenterStableId).transform;
                root.SetParent(ownedRoot, false); root.gameObject.layer = ownedRoot.gameObject.layer;
                root.localPosition = new Vector3((index - (ordered.Length - 1) / 2f) * 15f, 0, 0);
                centers.Add(center.CenterStableId, root);
                centerMarks.Add(center.CenterStableId, new List<Renderer>());
                var accent = index % 2 == 0 ? new Color(.96f, .63f, .25f) : new Color(.23f, .78f, .68f);
                var floor = Box(root, "가상 바닥", new Vector3(0, -.14f, 0), new Vector3(13f, .25f, 12f), new Color(.13f, .19f, .25f));
                centerMarks[center.CenterStableId].Add(floor);
                centerMarks[center.CenterStableId].Add(Box(root, "센터 색상 안내", new Vector3(0, .05f, 5.5f), new Vector3(12f, .12f, .24f), accent));
                Label(root, center.DisplayName + " · 모의 공간", new Vector3(0, 3.1f, 4.5f), .16f, accent);
                var shelves = state.Locations.Where(value => value.CenterStableId == center.CenterStableId)
                    .OrderBy(value => value.LocationStableId, StringComparer.Ordinal).ToArray();
                for (var shelf = 0; shelf < shelves.Length; shelf++)
                {
                    var location = shelves[shelf];
                    var shelfRoot = new GameObject("가상 선반 · " + location.LocationStableId).transform;
                    shelfRoot.SetParent(root, false); shelfRoot.gameObject.layer = root.gameObject.layer;
                    shelfRoot.localPosition = new Vector3((shelf % 3 - Math.Min(shelves.Length - 1, 2) / 2f) * 3.8f, 0, 1.3f - shelf / 3 * 3.7f);
                    locations.Add(location.LocationStableId, shelfRoot);
                    var renderers = new List<Renderer>(); locationMarks.Add(location.LocationStableId, renderers);
                    for (var level = 0; level < 3; level++)
                    {
                        renderers.Add(Box(shelfRoot, "선반 단 " + level, new Vector3(0, .4f + level * .85f, 0), new Vector3(3.1f, .13f, 1.6f), new Color(.29f, .39f, .48f)));
                        Box(shelfRoot, "가상 상품 상자 " + level, new Vector3(-.6f + level * .45f, .75f + level * .85f, 0), new Vector3(.65f, .55f, .65f), accent * .85f);
                    }
                    foreach (var x in new[] { -1.45f, 1.45f })
                        renderers.Add(Box(shelfRoot, "선반 기둥", new Vector3(x, 1.1f, 0), new Vector3(.12f, 2.2f, 1.5f), new Color(.35f, .45f, .53f)));
                    Label(shelfRoot, location.LocationBarcode, new Vector3(0, 2.65f, -.2f), .095f, Color.white);
                    stockLabels.Add(location.LocationStableId,
                        Label(shelfRoot, "재고 조회 중", new Vector3(0, .15f, -1.8f), .09f, new Color(.83f, .9f, .95f)));
                }
                var staff = state.Workers.Where(value => value.CenterStableId == center.CenterStableId)
                    .OrderBy(value => value.WorkerStableId, StringComparer.Ordinal).ToArray();
                for (var worker = 0; worker < staff.Length; worker++)
                {
                    var person = staff[worker];
                    var marker = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    marker.GetComponent<Collider>().enabled = false;
                    marker.name = "가상 직원 · " + person.WorkerStableId;
                    marker.transform.SetParent(root, false);
                    marker.layer = root.gameObject.layer;
                    marker.transform.localPosition = new Vector3((worker - (staff.Length - 1) / 2f) * 2f, 1f, -3.6f);
                    marker.transform.localScale = new Vector3(.7f, 1f, .7f);
                    OwnColor(marker.GetComponent<Renderer>(), person.Available ? accent : new Color(.38f, .4f, .43f));
                    workerMarkers.Add(person.WorkerStableId, marker.GetComponent<Renderer>());
                    workerLabels.Add(person.WorkerStableId,
                        Label(root, "", new Vector3(marker.transform.localPosition.x, .1f, -5.2f), .09f, Color.white));
                }
            }
            갱신(state);
            var all = ownedRoot.GetComponentsInChildren<Renderer>();
            관찰Bounds = all.Length == 0 ? new Bounds(transform.position, Vector3.one) : all[0].bounds;
            foreach (var renderer in all.Skip(1)) { var bounds = 관찰Bounds; bounds.Encapsulate(renderer.bounds); 관찰Bounds = bounds; }
        }

        public void 갱신(SimulationFulfillmentStateSnapshot state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (!구성됨) { 구성(state); return; }
            if (state.ScenarioStableId != scenarioId || state.ConfigurationRevision != configurationRevision
                || !state.Centers.Select(value => value.CenterStableId).OrderBy(value => value, StringComparer.Ordinal).SequenceEqual(센터코드목록)
                || !state.Locations.Select(value => value.LocationStableId).OrderBy(value => value, StringComparer.Ordinal).SequenceEqual(위치코드목록))
                throw new InvalidOperationException("현재 가상 공간과 출고 구성 판본이 다릅니다.");
            foreach (var location in state.Locations)
            {
                var lines = state.Stocks.Where(value => value.LocationStableId == location.LocationStableId)
                    .GroupBy(value => value.UnitCode).OrderBy(group => group.Key, StringComparer.Ordinal)
                    .Select(group => "보관 " + Number(group.Sum(value => value.OnHandQuantity))
                        + " · 예약 " + Number(group.Sum(value => value.ReservedQuantity))
                        + " · 가용 " + Number(group.Sum(value => value.OnHandQuantity - value.ReservedQuantity)) + " " + group.Key);
                stockLabels[location.LocationStableId].text = string.Join("\n", lines);
            }
            foreach (var worker in state.Workers)
            {
                if (!workerLabels.TryGetValue(worker.WorkerStableId, out var label))
                    throw new InvalidOperationException("가상 직원과 출고 구성의 연결이 없습니다.");
                label.text = worker.DisplayName + "\n" + (worker.Available && worker.CanPick ? "피킹 가능" : "현재 담당 불가");
                var marker = workerMarkers[worker.WorkerStableId];
                colors[marker] = worker.Available && worker.CanPick ? new Color(.24f, .76f, .64f) : new Color(.38f, .4f, .43f);
            }
            선택강조(선택센터코드, 선택위치코드);
        }

        public void 선택강조(string centerId, string locationId)
        {
            centerId ??= ""; locationId ??= "";
            if (centerId.Length > 0 && !centers.ContainsKey(centerId))
                throw new InvalidOperationException("선택 센터에 대응하는 모형이 없습니다.");
            if (locationId.Length > 0 && (!locations.TryGetValue(locationId, out var shelf)
                || centerId.Length == 0 || !shelf.IsChildOf(centers[centerId])))
                throw new InvalidOperationException("선택 위치가 해당 센터의 선반과 일치하지 않습니다.");
            선택센터코드 = centerId; 선택위치코드 = locationId;
            foreach (var pair in colors) Colorize(pair.Key, pair.Value);
            if (centerMarks.TryGetValue(centerId, out var center))
                foreach (var renderer in center) Colorize(renderer, new Color(.17f, .36f, .42f));
            if (locationMarks.TryGetValue(locationId, out var selected))
                foreach (var renderer in selected) Colorize(renderer, new Color(1f, .84f, .32f));
        }

        public void 카메라연결(Camera camera) => labelCamera = camera;
        private void LateUpdate()
        {
            if (labelCamera == null) return;
            foreach (var label in labels)
                if (label != null) label.transform.rotation = labelCamera.transform.rotation;
        }
        private Renderer Box(Transform parent, string name, Vector3 position, Vector3 size, Color color)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube); item.name = name;
            item.GetComponent<Collider>().enabled = false;
            item.transform.SetParent(parent, false); item.layer = parent.gameObject.layer;
            item.transform.localPosition = position; item.transform.localScale = size;
            var renderer = item.GetComponent<Renderer>(); OwnColor(renderer, color); return renderer;
        }
        private void OwnColor(Renderer renderer, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("가상 센터 표시 Shader가 없습니다.");
            var material = new Material(shader) { name = "출고 연습 전용 표시" };
            materials.Add(material); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            colors.Add(renderer, color); Colorize(renderer, color);
        }
        private void Colorize(Renderer renderer, Color color)
        {
            // Unity native 자원은 MonoBehaviour 필드 생성자에서 만들 수 없다.
            properties ??= new MaterialPropertyBlock();
            properties.Clear(); properties.SetColor("_BaseColor", color); properties.SetColor("_Color", color);
            renderer.SetPropertyBlock(properties);
        }
        private TextMesh Label(Transform parent, string text, Vector3 position, float size, Color color)
        {
            var root = new GameObject("모형 안내"); root.transform.SetParent(parent, false); root.layer = parent.gameObject.layer;
            root.transform.localPosition = position;
            root.transform.localRotation = Quaternion.Euler(40, 0, 0);
            var label = root.AddComponent<TextMesh>(); label.font = font; label.fontSize = 48;
            label.characterSize = size; label.text = text; label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center; label.color = color;
            root.GetComponent<Renderer>().sharedMaterial = font.material; labels.Add(label); return label;
        }
        private static string Number(decimal value) => value.ToString("0.###", CultureInfo.InvariantCulture);
        private void OnDestroy()
        {
            foreach (var material in materials)
                if (material != null) { if (UnityEngine.Application.isPlaying) Destroy(material); else DestroyImmediate(material); }
            if (font != null) { if (UnityEngine.Application.isPlaying) Destroy(font); else DestroyImmediate(font); }
        }
    }
}
