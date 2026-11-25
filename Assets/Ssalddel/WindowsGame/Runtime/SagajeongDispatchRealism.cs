using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ssalddel.WindowsGame
{
    // 현실감 시험 (2026-11-24 01:41 사용자: 「시안을 아까 뽑은 게 괜찮았었거든. 한번 적용을 해줘봐봐. 그리고 적용된 화면을 나한테 한번 보여줘.」)
    // 시안 네 장(해와 그늘 · 옥상과 벽 · 창 속의 방 · 거리와 렌즈)을 지금 렌더러(Built-in)에서 시험한다. URP 전환은 Codex 몫이라 뒤로 둔다.
    //  - 해: 그날 그 시각 · 디오라마 원점 위경도로 해 방위 · 높이를 셈한다(NOAA 간이식, 한국 표준시). 「낮과 밤」을 끄면 늘 오후(구운 그림자와 같은 방향).
    //    벽 · 지붕 · 나무가 해 쪽은 밝고 반대쪽은 어둡다(면의 방향은 화면 미분으로, HongdalDiorama.shader).
    //  - 해 그림자: 벽 · 나무를 해 반대쪽 땅으로 눕혀 그린다(HongdalSunShadow.shader). 미리 구운 고정 방향 그림자는 끈다.
    //  - 접지 그늘: 벽 밑 둘레에 옅은 띠(읽을 때 한 번 만든다, HongdalContactShade.shader).
    //  - 건물 꼬리표: 정점 순서(make_dong_modules.py prism(): 변마다 벽 4 · 테두리 4, 그다음 지붕)로 건물을 갈라 uv3 = (갈래 + 씨앗, 실제 높이 m).
    //    갈래는 구울 때 고른 지붕 색으로 안다(1 주택 · 2 초록 지붕 주택 · 3 아파트 · 4 상가 · 5 공공 · 6 공장 · 창고). 셰이더가 벽 재질 · 지붕 색 · 아파트 창을 고른다.
    //  - 옥상 소품: 물탱크 · 실외기 · 옥탑(갈래 · 넓이 · 높이에 따른 규칙).
    //  - 렌즈: 밤 불빛 번짐 · 위아래 흐림(틸트시프트) · 색 보정(HongdalLens.cs).
    //  모두 표현이다(실제 옥상 · 벽 재질 자료가 아니다). 자료 창 「현실감 시험」을 끄면 전과 똑같이 그린다. 지도 굽기 파일은 바꾸지 않는다.
    //  URP로 돌 때(S2 시험 갈래, 2026-11-24 02:17 사용자: 「여기서 한번 바꿔줘도 괜찮고」): 눕힌 그림자 대신 「Hongdal sun」 방향광의 실제 그림자 맵을 쓴다.
    //    건물 · 옥상 소품 · 나무가 그림자를 드리우고(정점 법선은 읽을 때 셈), 바탕 셰이더가 벽 · 지붕 · 땅에서 그림자를 받는다(HongdalDioramaCore.cginc).
    //    렌즈는 URP 볼륨 · 전면 패스가 맡는다(S2 사본에만 있는 HongdalUrpBridge가 _HongdalLensParams를 읽는다). 이 파일은 URP 형식을 직접 부르지 않는다.
    public sealed partial class SagajeongDispatchView
    {
        private const string RealismPreference = "hongdal.realism.on";
        private static readonly int SunDirId = Shader.PropertyToID("_HongdalSunDir");
        private static readonly int RealId = Shader.PropertyToID("_HongdalReal");
        private static readonly int ShadowAlphaId = Shader.PropertyToID("_Alpha");
        private static readonly int LensParamsId = Shader.PropertyToID("_HongdalLensParams");
        private static bool UrpActive => GraphicsSettings.currentRenderPipeline != null;

        private bool realismOn = true;
        private bool realismRead;
        private int realismApplied = -1;                       // 렌더러에 마지막으로 건 값(-1 = 아직)
        private Material sunShadowMaterial;
        private Material treeShadowMaterial;
        private Material contactMaterial;
        private readonly List<Renderer> realismRenderers = new();     // 접지 그늘 · 옥상 소품
        private readonly List<Renderer> flatShadowRenderers = new();  // 눕힌 해 그림자 · 나무 그림자(Built-in)
        private readonly List<Renderer> casterRenderers = new();      // URP: 그림자 맵에 그리는 것(건물 · 옥상 소품 · 나무)
        private Light realismSunLight;
        private readonly List<Renderer> bakedShadowRenderers = new(); // 미리 구운 그림자(현실감을 켜면 끈다)
        private HongdalLens realismLens;
        private int realismTagged, realismProps, realismContacts, realismRefined, realismKerbFaces;
        private HashSet<string> refineCodes;
        private readonly HashSet<int> surveyPolesPlaced = new();
        private int realismLampPoles;
        private long realismMs, realismKerbMs;
        private static readonly int RaiseId = Shader.PropertyToID("_HongdalRaise");
        private const float SidewalkRaise = 0.15f;      // 가 등급 동의 보도 높이(m, 연석 높이 표현값)

        // 변 열쇠((작은 번호 << 32) | 큰 번호)의 해시. long 기본 해시(위 · 아래 32비트 XOR)는 붙은 번호끼리 거의 같아
        //  사전이 한 줄로 늘어선다(g1a · g1b에서 읽기 300초). 섞어서 고르게 한다.
        private sealed class EdgeKeys : IEqualityComparer<long>
        {
            public static readonly EdgeKeys Instance = new();
            public bool Equals(long a, long b) => a == b;
            public int GetHashCode(long key) => (int)(((ulong)key * 0x9E3779B97F4A7C15UL) >> 32);
        }

        [Serializable]
        private sealed class RefinePack
        {
            public string rule;
            public string madeAt;
            public string[] codes;
        }

        // 정밀화 대상(품질 「가」, 사용자 2026-11-24). 굽기 품질표에서 만든 목록(hongdal-refine-ga.json). 없으면 빈 목록.
        private HashSet<string> RefineCodes()
        {
            if (refineCodes != null) return refineCodes;
            refineCodes = new HashSet<string>();
            var asset = Resources.Load<TextAsset>("hongdal-refine-ga");
            if (asset == null) return refineCodes;
            var pack = JsonUtility.FromJson<RefinePack>(asset.text);
            if (pack?.codes != null) foreach (var code in pack.codes) refineCodes.Add(code);
            Resources.UnloadAsset(asset);
            return refineCodes;
        }

        private void ReadRealismPreference()
        {
            if (realismRead) return;
            realismRead = true;
            realismOn = PlayerPrefs.GetInt(RealismPreference, 1) != 0;
        }

        private void SaveRealismPreference()
        {
            PlayerPrefs.SetInt(RealismPreference, realismOn ? 1 : 0);
            PlayerPrefs.Save();
        }

        private bool EnsureRealismMaterials()
        {
            if (sunShadowMaterial != null && contactMaterial != null) return true;
            var shadow = Resources.Load<Shader>("HongdalSunShadow");
            var contact = Resources.Load<Shader>("HongdalContactShade");
            if (shadow == null || contact == null)
            {
                Debug.LogWarning("Hongdal realism: shader resource is missing");
                return false;
            }
            sunShadowMaterial = new Material(shadow) { name = "Hongdal sun shadows" };
            treeShadowMaterial = new Material(shadow) { name = "Hongdal tree sun shadows", renderQueue = 2011 };
            treeShadowMaterial.SetFloat("_Mode", 1f);
            contactMaterial = new Material(contact) { name = "Hongdal contact shade" };
            EnsureRoofDetail();
            return true;
        }

        // G3 옥상 무늬(2026-11-24): Poly Haven CC0 bitumen · corrugated_iron 1K에서 밝기만 뽑아 512 × 512 한 장(hongdal-roof-detail.bytes, RGBA32).
        //  r = 역청 밝기(평균 0.5), g = 골판 밝기, b = 골판 기울기. 색은 굽기 지붕색 · RoofMaterial이 정하고, 이것은 얼룩 · 골만 더한다.
        private Texture2D roofDetail;
        private bool roofDetailTried;
        private static readonly int RoofTexId = Shader.PropertyToID("_HongdalRoofTex");

        private void EnsureRoofDetail()
        {
            if (roofDetailTried) return;
            roofDetailTried = true;
            var asset = Resources.Load<TextAsset>("hongdal-roof-detail");
            if (asset == null) return;
            var bytes = asset.bytes;
            Resources.UnloadAsset(asset);
            if (bytes.Length != 512 * 512 * 4) return;
            roofDetail = new Texture2D(512, 512, TextureFormat.RGBA32, true, true)
            {
                name = "Hongdal roof detail", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4,
            };
            roofDetail.SetPixelData(bytes, 0);          // 가장 큰 밉만 넣고 나머지는 Apply가 만든다(LoadRawTextureData는 밉 전부를 바란다)
            roofDetail.Apply(true, true);
            Shader.SetGlobalTexture(RoofTexId, roofDetail);
        }

        // 해의 방향(땅에서 해 쪽을 보는 단위 벡터, x 동 · y 위 · z 북)과 높이의 사인. NOAA 간이식, 한국 표준시(UTC+9).
        private static Vector3 SunToward(DateTime date, double seconds, double latitude, double longitude, out float sinElevation)
        {
            var hour = seconds / 3600.0;
            var gamma = 2 * Math.PI / 365.0 * (date.DayOfYear - 1 + (hour - 12) / 24.0);
            var eqTime = 229.18 * (0.000075 + 0.001868 * Math.Cos(gamma) - 0.032077 * Math.Sin(gamma) - 0.014615 * Math.Cos(2 * gamma) - 0.040849 * Math.Sin(2 * gamma));
            var decl = 0.006918 - 0.399912 * Math.Cos(gamma) + 0.070257 * Math.Sin(gamma) - 0.006758 * Math.Cos(2 * gamma) + 0.000907 * Math.Sin(2 * gamma) -
                       0.002697 * Math.Cos(3 * gamma) + 0.00148 * Math.Sin(3 * gamma);
            var trueSolar = hour * 60 + eqTime + 4 * longitude - 60 * 9;          // 참 태양시(분)
            var hourAngle = (trueSolar / 4 - 180) * Math.PI / 180;
            var lat = latitude * Math.PI / 180;
            var cosZenith = Math.Max(-1, Math.Min(1, Math.Sin(lat) * Math.Sin(decl) + Math.Cos(lat) * Math.Cos(decl) * Math.Cos(hourAngle)));
            var azimuth = Math.Atan2(Math.Sin(hourAngle), Math.Cos(hourAngle) * Math.Sin(lat) - Math.Tan(decl) * Math.Cos(lat)) + Math.PI;   // 북에서 시계 방향
            var elevation = Math.Asin(cosZenith);
            sinElevation = (float)cosZenith;
            return new Vector3((float)(Math.Sin(azimuth) * Math.Cos(elevation)), (float)Math.Sin(elevation), (float)(Math.Cos(azimuth) * Math.Cos(elevation)));
        }

        // LateUpdate(SagajeongDispatchDiorama.cs)가 SyncEnvironment 다음에 매 화면 부른다.
        private void SyncRealism(Camera camera, float halfHeight)
        {
            ReadRealismPreference();
            var on = realismOn && DongModulesLoaded;
            ApplyRealismShown(on);
            if (!on)
            {
                Shader.SetGlobalVector(RealId, Vector4.zero);
                StopRealismLens();
                return;
            }
            Vector3 toward;
            float sinElevation;
            if (skyOn) toward = SunToward(GameDate(), GameSeconds(), dioramaLatitude, dioramaLongitude, out sinElevation);
            else
            {
                toward = new Vector3(-0.931f * 0.72f, 0.69f, -0.364f * 0.72f);      // 늘 오후: 구운 그림자(SHADOW_DIR)와 같은 방향, 높이 약 44°
                sinElevation = 0.69f;
            }
            var env = envNow;
            var cloud = Mathf.Clamp01(env.Rain * 1.2f + env.Wet * 0.45f);         // 비 오는 날: 빛이 흩어지고 그림자가 옅다
            var light = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.02f, 0.10f, sinElevation)) * (1f - 0.6f * cloud);
            var shade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.03f, 0.16f, sinElevation)) * (1f - 0.8f * cloud);
            Shader.SetGlobalVector(SunDirId, new Vector4(toward.x, toward.y, toward.z, light));
            var night = skyOn ? env.Night : 0f;
            var tilt = Mathf.Clamp01(1.1f - halfHeight / 350f) * 0.75f;
            Shader.SetGlobalVector(LensParamsId, new Vector4(Mathf.Lerp(0.15f, 1.0f, night), Mathf.Lerp(0.95f, 0.8f, night), tilt, 1f));   // URP 볼륨이 읽는다
            Shader.SetGlobalVector(RealId, new Vector4(1f, 0.5f * shade, Mathf.Lerp(0.55f, 0.95f, light), roofDetail != null ? 1f : 0f));      // w = 옥상 무늬(G3)
            SyncTraffic();
            if (UrpActive)
            {
                SyncRealismSunLight(toward, light, shade);
                StopRealismLens();
                return;
            }
            if (treeShadowMaterial != null)
                treeShadowMaterial.SetFloat(ShadowAlphaId, seasonOn ? Mathf.Lerp(0.45f, 1f, Mathf.Max(env.Leaf, 0.15f)) : 1f);
            // 렌즈: 가까울수록 위아래가 더 흐리다(미니어처 느낌). 밤에는 불빛이 번진다.
            var lens = EnsureRealismLens(camera);
            if (lens == null) return;
            lens.enabled = true;
            lens.Tilt = tilt;
            lens.Bloom = Mathf.Lerp(0.2f, 1.4f, night);
            lens.Threshold = Mathf.Lerp(0.92f, 0.8f, night);
            lens.Grade = 1f;
        }

        private void ApplyRealismShown(bool on)
        {
            var urp = UrpActive;
            var want = (on ? 1 : 0) + (urp ? 2 : 0);
            if (realismApplied == want) return;
            realismApplied = want;
            foreach (var renderer in realismRenderers)
                if (renderer != null) renderer.enabled = on;
            foreach (var renderer in flatShadowRenderers)
                if (renderer != null) renderer.enabled = on && !urp;
            foreach (var renderer in bakedShadowRenderers)
                if (renderer != null) renderer.enabled = !on;
            foreach (var renderer in casterRenderers)
                if (renderer != null) renderer.shadowCastingMode = on && urp ? ShadowCastingMode.On : ShadowCastingMode.Off;
            if (realismSunLight != null && !on) realismSunLight.enabled = false;
        }

        // URP: 그림자 맵을 만들 방향광 하나. 빛의 세기 · 색은 바탕 셰이더가 _HongdalSunDir로 따로 셈하므로 그림자에만 쓴다.
        private void SyncRealismSunLight(Vector3 toward, float light, float shade)
        {
            if (realismSunLight == null)
            {
                var sun = new GameObject("Hongdal sun");
                realismSunLight = sun.AddComponent<Light>();
                realismSunLight.type = LightType.Directional;
                realismSunLight.shadows = LightShadows.Soft;
                realismSunLight.intensity = 1f;
                RenderSettings.sun = realismSunLight;
            }
            realismSunLight.enabled = light > 0.01f;
            realismSunLight.shadowStrength = Mathf.Clamp01(0.92f * Mathf.Max(shade, 0.15f));
            if (toward.y > 0.001f) realismSunLight.transform.rotation = Quaternion.LookRotation(-toward, Vector3.up);
        }

        private HongdalLens EnsureRealismLens(Camera camera)
        {
            if (camera == null) return null;
            if (realismLens != null && realismLens.gameObject == camera.gameObject) return realismLens;
            realismLens = camera.GetComponent<HongdalLens>();
            if (realismLens == null) realismLens = camera.gameObject.AddComponent<HongdalLens>();
            return realismLens;
        }

        // 촬영: 동마다 기록을 남긴다
        private void WriteRealismDongs(string directory)
        {
            var report = new RealismDongReport
            {
                madeAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                note = "현실감 시험 · 동마다 덧붙인 것. refined = 정밀화 대상(「가」). 연석 세로면은 사가정 둘레 1.8 km만. 숫자는 표현 부품의 개수(자료 아님)",
                dongs = realismDongs.ToArray(),
            };
            System.IO.File.WriteAllText(System.IO.Path.Combine(directory, "realism-dongs.json"), JsonUtility.ToJson(report, true));
        }

        private void StopRealismLens()
        {
            if (realismLens != null && realismLens.enabled) realismLens.enabled = false;
        }

        private void ReleaseRealism()
        {
            StopRealismLens();
            Shader.SetGlobalVector(RealId, Vector4.zero);
            if (sunShadowMaterial != null) Destroy(sunShadowMaterial);
            if (treeShadowMaterial != null) Destroy(treeShadowMaterial);
            if (contactMaterial != null) Destroy(contactMaterial);
            sunShadowMaterial = null;
            treeShadowMaterial = null;
            contactMaterial = null;
            realismRenderers.Clear();
            flatShadowRenderers.Clear();
            casterRenderers.Clear();
            bakedShadowRenderers.Clear();
            if (realismSunLight != null) Destroy(realismSunLight.gameObject);
            realismSunLight = null;
            realismApplied = -1;
            realismTagged = realismProps = realismContacts = realismRefined = realismKerbFaces = realismLampPoles = realismCars = 0;
            realismMs = realismKerbMs = 0;
            realismDongs.Clear();
            if (trafficMesh != null) Destroy(trafficMesh);
            trafficMesh = null;
            trafficRenderer = null;
            trafficShadow = null;
            trafficHour = -1;
            roadCells.Clear();
            buildingCells.Clear();
            shopLights.Clear();
            sheltersTried = false;
            shelterCount = 0;
            shelterRenderer = null;
            trafficCentre = Vector2.zero;
            if (shelterMesh != null) Destroy(shelterMesh);
            shelterMesh = null;
            lastStreetGrid = null;
            surveyPolesPlaced.Clear();
        }

        // 큰길의 차(R4, 2026-11-24): 서울 길 속도 자료(hongdal-data-roads.json · 10월 평일 시간대별, 방향마다 한 줄)의 그 시각 막힘에 맞춰
        //  진행 방향 오른쪽 차로(가운데 선에서 1.9 m)에 차를 늘어세운다. 잘 뚫리면 38 m에 한 대, 막힐수록 7 m까지 촘촘히, 새벽(0 ~ 5시)은 성기게.
        //  차의 자리 · 차종 · 색은 표현값이다(막힘 정도만 자료). 밤에는 앞등(흰) · 뒷등(붉은)이 켜진다. 보는 곳 1.6 km 안, 시각(시)이 바뀌거나 보는 곳이 700 m 넘게 옮겨질 때만 다시 만든다.
        private readonly HashSet<long> roadCells = new(EdgeKeys.Instance);     // 읽은 동의 차도 칸(2 m)
        private readonly HashSet<long> buildingCells = new(EdgeKeys.Instance); // 사가정역 둘레의 건물 칸(2 m)
        private const float PeopleRadius = 140f;
        private readonly List<Vector2> shopLights = new();     // 밤 가게 불빛 자리(공통 원점 기준 m, 표현)
        private int peopleCount;

        // 역 앞 사람(R4): 사가정역(7호선) 그 시각 승하차 인원(서울 OA-12252, 한 달 누적 · 시각대, hongdal-data-station-hours.json)으로 사람 수를 정한다.
        //  하루치 한 시간 = (승차 + 하차) / 30, 화면에 보이게 그 1/14만큼(6 ~ 260명, 표현 비율). 자리는 차도 칸 바로 옆의 빈 칸(보도 띠), 역에 가까울수록 많이.
        //  사람의 자리 · 옷 색은 표현이다. 사가정역 둘레 140 m만(다른 역은 아직 없다).
        private void AddStationPeople(RealismMesh mesh, int hour)
        {
            peopleCount = 0;
            var pack = EnsureStationHours();
            if (pack?.stations == null || !stationHoursByName.TryGetValue("사가정", out var entries)) return;
            var riders = 0;
            foreach (var entry in entries)
            {
                var month = entry.months != null && entry.months.Length > 0 ? entry.months[0] : null;
                if (month?.b == null || month.a == null || month.b.Length <= hour || month.a.Length <= hour) continue;
                riders += month.b[hour] + month.a[hour];
            }
            var want = Mathf.Clamp(Mathf.RoundToInt(riders / 30f / 14f), 6, 260);
            var candidates = new List<Vector2>();
            var reach = Mathf.CeilToInt(PeopleRadius / 2f);
            for (var cz = -reach; cz <= reach; cz++)
            for (var cx = -reach; cx <= reach; cx++)
            {
                var key = ((long)cx << 32) ^ (uint)cz;
                if (roadCells.Contains(key) || buildingCells.Contains(key)) continue;
                var nearRoad = false;
                for (var dz = -1; dz <= 1 && !nearRoad; dz++)
                for (var dx = -1; dx <= 1 && !nearRoad; dx++)
                    nearRoad = roadCells.Contains(((long)(cx + dx) << 32) ^ (uint)(cz + dz));
                if (!nearRoad) continue;
                var centre = new Vector2(cx * 2f + 1f, cz * 2f + 1f);
                if (centre.sqrMagnitude > PeopleRadius * PeopleRadius) continue;
                candidates.Add(centre);
            }
            if (candidates.Count == 0) return;
            var coats = new[]
            {
                new Color32(40, 44, 56, 255), new Color32(28, 30, 34, 255), new Color32(120, 124, 130, 255), new Color32(196, 184, 160, 255),
                new Color32(226, 228, 226, 255), new Color32(70, 86, 120, 255), new Color32(150, 60, 56, 255), new Color32(96, 110, 84, 255),
            };
            var hair = new Color32(36, 32, 30, 255);
            for (var tries = 0; tries < want * 6 && peopleCount < want; tries++)
            {
                var roll = RealismHash(tries * 1.37f + hour * 11.3f, tries * 0.71f - hour);
                var spot = candidates[(int)(roll % (uint)candidates.Count)];
                var keep = 1f / (1f + spot.magnitude / 45f);                   // 역에 가까울수록 많이
                if (((roll >> 12) & 0xFF) / 255f > keep) continue;
                var at = spot + new Vector2((((roll >> 4) & 0xFF) / 255f - 0.5f) * 1.6f, (((roll >> 20) & 0xFF) / 255f - 0.5f) * 1.6f);
                var face = new Vector2(Mathf.Cos((roll & 0x3FF) * 0.00614f), Mathf.Sin((roll & 0x3FF) * 0.00614f));
                var side = new Vector2(-face.y, face.x);
                var coat = coats[(roll >> 9) % (uint)coats.Length];
                mesh.Box(at, 0.2f, face, side, 0.16f, 0.24f, 1.18f, coat, coat, Vector2.zero, false);
                mesh.Box(at, 1.38f, face, side, 0.11f, 0.11f, 0.24f, hair, hair, Vector2.zero, false);
                peopleCount++;
            }
        }
        private Mesh trafficMesh;
        private Renderer trafficRenderer, trafficShadow;
        private int trafficHour = -1, trafficCars;
        private const float TrafficRadius = 1600f;
        private Vector2 trafficCentre;               // 차 · 쉘터를 만든 둘레의 가운데(공통 원점 기준 m, 처음은 사가정역)

        private void SyncTraffic()
        {
            if (dioramaRoot == null || roadCells.Count == 0) return;
            // 보는 곳(화면 가운데가 닿는 땅)을 따라간다: 다른 역으로 옮겨 700 m 넘게 멀어지면 그 둘레로 다시 만든다
            var focus = trafficCentre;
            var cam = Camera.main;
            if (cam != null)
            {
                var ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                if (ray.direction.y < -1e-3f)
                {
                    var hit = ray.origin + ray.direction * (-ray.origin.y / ray.direction.y);
                    var local = dioramaRoot.transform.InverseTransformPoint(hit);
                    focus = new Vector2(local.x, local.z);
                }
            }
            var moved = (focus - trafficCentre).sqrMagnitude > 700f * 700f;
            if (!sheltersTried || moved) BuildShelters(focus);
            var hour = skyOn ? Mathf.Clamp(GameSeconds() / 3600, 0, 23) : 15;
            if (hour == trafficHour && trafficRenderer != null && !moved) return;
            trafficHour = hour;
            trafficCentre = focus;
            if (!roadTried) { roadTried = true; roadPack = LoadPack<RoadPack>("hongdal-data-roads"); }
            if (roadPack?.links == null) return;
            var mesh = new RealismMesh();
            var root = dioramaRoot.transform;
            var glass = new Color32(46, 54, 64, 255);
            var head = new Color32(230, 230, 222, 255);
            var tail = new Color32(150, 40, 36, 255);
            trafficCars = 0;
            for (var index = 0; index < roadPack.links.Length && trafficCars < 9000; index++)
            {
                var link = roadPack.links[index];
                if (link?.p == null || link.p.Length < 4) continue;
                var jam = RoadJam(index, hour, out _);
                if (jam < 0f) jam = 0.15f;
                var spacing = Mathf.Lerp(38f, 7.2f, jam) * (hour < 6 ? 2.2f : 1f);
                var seed = RealismHash(link.p[0] * 0.37f, link.p[1] * 0.61f + index);
                var next = spacing * (0.2f + 0.8f * ((seed & 0xFF) / 255f));
                var walked = 0f;
                var previous = root.InverseTransformPoint(GuWorld(link.p[0], link.p[1], 0f));
                for (var i = 2; i + 1 < link.p.Length; i += 2)
                {
                    var point = root.InverseTransformPoint(GuWorld(link.p[i], link.p[i + 1], 0f));
                    var a = new Vector2(previous.x, previous.z);
                    var b = new Vector2(point.x, point.z);
                    previous = point;
                    var length = (b - a).magnitude;
                    if (length < 0.05f) continue;
                    var dir = (b - a) / length;
                    var right = new Vector2(dir.y, -dir.x);
                    while (next <= walked + length && trafficCars < 9000)
                    {
                        var at = a + dir * (next - walked) + right * 1.9f;
                        var roll = RealismHash(at.x, at.y);
                        next += spacing * (0.7f + 0.6f * ((roll & 0xFF) / 255f));
                        if ((at - trafficCentre).sqrMagnitude > TrafficRadius * TrafficRadius) continue;
                        // 길 속도 선과 구운 차도가 몇 m 어긋나는 곳이 있다: 차의 가운데 · 앞 · 뒤가 모두 차도 칸일 때만 세운다
                        var front = at + dir * 2f;
                        var back = at - dir * 2f;
                        if (!roadCells.Contains(StreetGrid.RoadKey(at.x, at.y)) || !roadCells.Contains(StreetGrid.RoadKey(front.x, front.y)) ||
                            !roadCells.Contains(StreetGrid.RoadKey(back.x, back.y))) continue;
                        var colour = CarColours[(roll >> 8) % CarColours.Length];
                        var half = 2.15f + 0.2f * ((roll >> 13) & 1);
                        mesh.Box(at, 0.07f, dir, right, half, 0.86f, 0.72f, colour, colour, Vector2.zero, false);
                        mesh.Box(at - dir * 0.2f, 0.79f, dir, right, half * 0.52f, 0.76f, 0.5f, glass, colour, Vector2.zero, false);
                        // 앞등 · 뒷등: 위에서 보면 1 ~ 2 px라 등 상자를 조금 키워 밤길의 흰 · 붉은 줄이 읽히게 한다(표현)
                        mesh.Glow(at + dir * (half + 0.02f) + right * 0.55f, 0.36f, dir, right, 0.1f, 0.24f, 0.2f, head, -7f);
                        mesh.Glow(at + dir * (half + 0.02f) - right * 0.55f, 0.36f, dir, right, 0.1f, 0.24f, 0.2f, head, -7f);
                        mesh.Glow(at - dir * (half + 0.02f) + right * 0.58f, 0.5f, dir, right, 0.1f, 0.22f, 0.16f, tail, -9f);
                        mesh.Glow(at - dir * (half + 0.02f) - right * 0.58f, 0.5f, dir, right, 0.1f, 0.22f, 0.16f, tail, -9f);
                        trafficCars++;
                    }
                    walked += length;
                }
            }
            if (trafficRenderer != null)
            {
                realismRenderers.Remove(trafficRenderer);
                casterRenderers.Remove(trafficRenderer);
                Destroy(trafficRenderer.gameObject);
                trafficRenderer = null;
            }
            if (trafficShadow != null)
            {
                flatShadowRenderers.Remove(trafficShadow);
                Destroy(trafficShadow.gameObject);
                trafficShadow = null;
            }
            if (trafficMesh != null) Destroy(trafficMesh);
            trafficMesh = null;
            AddStationPeople(mesh, hour);
            if (mesh.Count == 0) return;
            trafficMesh = mesh.Build("Hongdal traffic " + hour, true);
            if (UrpActive) trafficMesh.RecalculateNormals();
            trafficRenderer = AddDongPart(dioramaRoot, "Hongdal traffic", trafficMesh, dioramaMaterial);
            realismRenderers.Add(trafficRenderer);
            casterRenderers.Add(trafficRenderer);
            if (treeShadowMaterial != null)          // Built-in: 차도 눕힌 해 그림자(나무와 같은 재질, 모든 정점을 눕힌다)
            {
                trafficShadow = AddDongPart(dioramaRoot, "Hongdal traffic shadows", trafficMesh, treeShadowMaterial);
                flatShadowRenderers.Add(trafficShadow);
            }
            realismApplied = -1;
            Debug.Log($"Hongdal traffic: {trafficCars} cars at {hour}시 (길 속도 자료의 막힘 · 자리 표현), 사가정역 앞 사람 {peopleCount}(승하차 자료 · 자리 표현)");
        }

        // 버스 정류장 쉘터(R4): 생활 시설 참고점 가운데 「정류장」(전국 버스정류장 원본, 중랑 16동, hongdal-data-facilities.json)마다
        //  가장 가까운 차도 칸(10 m 안)을 찾아, 차도에서 2.8 m 물러난 보도에 차도를 향해 세운다. 지붕 · 기둥 · 뒤 유리 · 의자 · 광고판(밤에 켜짐).
        //  자리는 자료(정류장 표지 근처), 쉘터가 실제로 있는지 · 생김은 표현이다. 6 m 안에 겹치면 하나만. 보는 곳 1.6 km 안(차와 같이 따라간다).
        private bool sheltersTried;
        private int shelterCount;
        private Mesh shelterMesh;

        private Renderer shelterRenderer;

        private void BuildShelters(Vector2 around)
        {
            sheltersTried = true;
            if (shelterRenderer != null)
            {
                realismRenderers.Remove(shelterRenderer);
                casterRenderers.Remove(shelterRenderer);
                Destroy(shelterRenderer.gameObject);
                shelterRenderer = null;
            }
            if (shelterMesh != null) Destroy(shelterMesh);
            shelterMesh = null;
            shelterCount = 0;
            var pack = EnsureFacilities();
            if (pack?.k == null || pack.keys == null) return;
            var bus = Array.IndexOf(pack.keys, "bus_stop");
            if (bus < 0) return;
            var root = dioramaRoot.transform;
            var mesh = new RealismMesh();
            var placed = new List<Vector2>();
            var roof = new Color32(196, 206, 212, 255);         // 서울 표준 쉘터: 밝은 지붕판(표현)
            var post = new Color32(92, 98, 102, 255);
            var glassBack = new Color32(150, 172, 184, 255);
            var bench = new Color32(146, 112, 80, 255);
            var panel = new Color32(222, 228, 234, 255);
            for (var i = 0; i < pack.k.Length; i++)
            {
                if (pack.k[i] != bus) continue;
                var world = root.InverseTransformPoint(GuWorld(pack.x[i], pack.y[i], 0f));
                var at = new Vector2(world.x, world.z);
                if ((at - around).sqrMagnitude > TrafficRadius * TrafficRadius) continue;
                var cx = Mathf.FloorToInt(at.x * 0.5f);
                var cz = Mathf.FloorToInt(at.y * 0.5f);
                var best = float.MaxValue;
                var road = Vector2.zero;
                for (var dz = -5; dz <= 5; dz++)
                for (var dx = -5; dx <= 5; dx++)
                {
                    if (!roadCells.Contains(((long)(cx + dx) << 32) ^ (uint)(cz + dz))) continue;
                    var centre = new Vector2((cx + dx) * 2f + 1f, (cz + dz) * 2f + 1f);
                    var d = (centre - at).sqrMagnitude;
                    if (d >= best) continue;
                    best = d;
                    road = centre;
                }
                if (best == float.MaxValue) continue;
                var toward = road - at;
                if (toward.sqrMagnitude < 0.01f) toward = Vector2.up;
                toward.Normalize();
                at = road - toward * 2.8f;                                   // 차도 칸 가운데에서 2.8 m 물러난 보도
                if (roadCells.Contains(StreetGrid.RoadKey(at.x, at.y))) continue;
                var near = false;
                foreach (var other in placed) if ((other - at).sqrMagnitude < 36f) { near = true; break; }
                if (near) continue;
                placed.Add(at);
                var along = new Vector2(-toward.y, toward.x);
                mesh.Box(at - toward * 0.6f, 0.2f, along, toward, 2.0f, 0.04f, 2.05f, glassBack, glassBack, Vector2.zero, false);
                mesh.Box(at, 2.45f, along, toward, 2.2f, 0.85f, 0.12f, roof, roof, Vector2.zero, false);
                mesh.Cylinder(at - toward * 0.6f + along * 1.95f, 0.18f, 0.06f, 2.27f, post, Vector2.zero);
                mesh.Cylinder(at - toward * 0.6f - along * 1.95f, 0.18f, 0.06f, 2.27f, post, Vector2.zero);
                mesh.Box(at - toward * 0.32f + along * 0.3f, 0.18f, along, toward, 1.1f, 0.2f, 0.42f, bench, bench, Vector2.zero, false);
                mesh.Glow(at - toward * 0.1f - along * 2.05f, 0.25f, toward, along, 0.55f, 0.07f, 1.75f, panel, -7f);
                shelterCount++;
            }
            if (mesh.Count == 0) return;
            shelterMesh = mesh.Build("Hongdal bus shelters", true);
            if (UrpActive) shelterMesh.RecalculateNormals();
            var renderer = AddDongPart(dioramaRoot, "Hongdal bus shelters", shelterMesh, dioramaMaterial);
            shelterRenderer = renderer;
            realismRenderers.Add(renderer);
            casterRenderers.Add(renderer);
            realismApplied = -1;
            Debug.Log($"Hongdal bus shelters: {shelterCount} (정류장 자리 · 쉘터 생김은 표현)");
        }

        // 골목 주차(표현, 시안 R4의 첫 걸음 · 「가」 동만). 차의 자리 자료는 없다.
        //  골목(굽기에서 0.02 m 겹, 골목 선분마다 직사각형 하나, 폭 3.2 m까지)이 4.8 m 넘게 길면 한쪽 가장자리를 따라 5.8 m 간격으로 열에 넷쯤 세운다.
        //  차도 · 건물 · 횡단보도 칸이면 건너뛴다. 색은 국내 차 색 비율을 흉내(흰 · 회색 · 검정이 대부분, 표현값).
        private int realismCars;
        private const int CarsPerDong = 2500;

        private static readonly Color32[] CarColours =
        {
            new(232, 233, 230, 255), new(236, 236, 232, 255), new(226, 228, 228, 255), new(240, 240, 236, 255),   // 흰
            new(150, 154, 158, 255), new(120, 124, 128, 255), new(176, 178, 180, 255),                             // 은 · 회색
            new(34, 36, 40, 255), new(44, 46, 50, 255),                                                             // 검정
            new(40, 62, 98, 255), new(150, 40, 38, 255), new(196, 184, 160, 255),                                   // 파랑 · 빨강 · 베이지
        };

        private void AddAlleyCars(RealismMesh props, Vector3[] pos, Vector2[] uv, Vector2[] uv2, StreetGrid grid)
        {
            var placed = 0;
            var glass = new Color32(46, 54, 64, 255);
            var k = 0;
            var n = pos.Length;
            bool Alley(int at) => uv2[at].x < 0.5f && Mathf.Abs(pos[at].y - 0.02f) < 0.002f && Mathf.Abs(uv[at].x) < 0.3f;
            while (k + 3 < n && placed < CarsPerDong)
            {
                if (!Alley(k) || !Alley(k + 1) || !Alley(k + 2) || !Alley(k + 3)) { k++; continue; }
                var a = new Vector2(pos[k].x, pos[k].z);
                var b = new Vector2(pos[k + 1].x, pos[k + 1].z);
                var c = new Vector2(pos[k + 2].x, pos[k + 2].z);
                var d = new Vector2(pos[k + 3].x, pos[k + 3].z);
                k += 4;
                Vector2 basePoint, dir, across;
                float length, width;
                if ((b - a).sqrMagnitude >= (c - b).sqrMagnitude)
                {
                    basePoint = a; length = (b - a).magnitude; width = (d - a).magnitude;
                    if (length < 0.01f || width < 0.01f) continue;
                    dir = (b - a) / length; across = (d - a) / width;
                }
                else
                {
                    basePoint = b; length = (c - b).magnitude; width = (a - b).magnitude;
                    if (length < 0.01f || width < 0.01f) continue;
                    dir = (c - b) / length; across = (a - b) / width;
                }
                if (width < 2.9f || length < 4.8f) continue;      // 굽기의 골목 폭은 3.2 m에서 멈춘다(표현). 한쪽에 붙여 세우면 1.4 m 남짓 남는다
                var hash = RealismHash(basePoint.x + 0.37f, basePoint.y - 1.3f);
                var line = basePoint + across * ((hash & 1) == 0 ? 0.95f : width - 0.95f);
                var slot = 0;
                var first = Mathf.Min(2.4f, length * 0.5f);
                for (var t = first; t <= length - 2.35f; t += 5.8f, slot++)
                {
                    var roll = ((hash >> (2 + slot % 24)) & 0xFF) / 255f;
                    if (roll > 0.38f) continue;
                    var at = line + dir * t;
                    if (!grid.Open(at.x, at.y) || !grid.Open(at.x + dir.x * 2f, at.y + dir.y * 2f) || !grid.Open(at.x - dir.x * 2f, at.y - dir.y * 2f)) continue;
                    var colour = CarColours[(hash >> (slot % 20)) % CarColours.Length];
                    var side = new Vector2(-dir.y, dir.x);
                    var length2 = 2.15f + 0.2f * ((hash >> (5 + slot % 16)) & 1);     // 경차 · 중형 대충
                    props.Box(at, 0.06f, dir, side, length2, 0.86f, 0.72f, colour, colour, Vector2.zero, false);
                    props.Box(at - dir * 0.2f, 0.78f, dir, side, length2 * 0.52f, 0.76f, 0.5f, glass, colour, Vector2.zero, false);
                    placed++;
                }
            }
            realismCars += placed;
        }

        // 조사된 보안등(OA-21696 2022-08)마다 기둥 · 팔 · 등머리 하나. 이 동 메시 범위 안의 점만, 먼저 읽은 동이 맡는다.
        //  자리만 자료다. 기둥 높이 · 팔 길이 · 등의 모양은 표현값(전신주에 단 등일 수도 있다).
        private void AddSurveyLampPoles(RealismMesh props, Vector3[] pos, StreetGrid grid)
        {
            var lamps = SurveyLamps();
            if (lamps.Count == 0 || pos.Length == 0 || grid == null) return;
            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
            foreach (var p in pos)
            {
                if (p.x < minX) minX = p.x;
                if (p.x > maxX) maxX = p.x;
                if (p.z < minZ) minZ = p.z;
                if (p.z > maxZ) maxZ = p.z;
            }
            var pole = new Color32(118, 122, 126, 255);
            var headColour = new Color32(226, 228, 220, 255);
            for (var k = 0; k < lamps.Count; k++)
            {
                if (surveyPolesPlaced.Contains(k)) continue;
                var at = lamps[k].At;
                if (at.x < minX || at.x > maxX || at.y < minZ || at.y > maxZ) continue;
                if (!grid.PoleSpot(ref at, out var arm)) continue;
                surveyPolesPlaced.Add(k);
                realismLampPoles++;
                var across = new Vector2(-arm.y, arm.x);
                const float Base = 0.18f, Tall = 5.4f;
                props.Cylinder(at, Base, 0.09f, Tall, pole, Vector2.zero);
                props.Box(at + arm * 0.5f, Base + Tall - 0.16f, arm, across, 0.5f, 0.035f, 0.06f, pole, pole, Vector2.zero, false);
                props.LampHead(at + arm * 1.02f, Base + Tall - 0.24f, arm, across, 0.21f, 0.1f, 0.1f, headColour);
            }
        }

        // ── 읽을 때 한 번: 건물 꼬리표 · 해 그림자 · 접지 그늘 · 옥상 소품 ─────────────────────

        // 동마다 현실감 덧붙이기 기록(자동 검사의 바탕, 제안 3절 「자동 검사(동마다)」). 촬영이 realism-dongs.json으로 남긴다.
        [Serializable]
        private sealed class RealismDongRecord
        {
            public string code;
            public string name;
            public bool refined;
            public int buildings, kerbFaces, alleyCars, lampPoles, propVertices, streetTreesMoved, streetTreesDropped;
            public long ms;
        }

        [Serializable]
        private sealed class RealismDongReport
        {
            public string madeAt;
            public string note;
            public RealismDongRecord[] dongs;
        }

        private readonly List<RealismDongRecord> realismDongs = new();

        private void AddRealismParts(DongModuleState module, GameObject part, Mesh mesh, Mesh trees)
        {
            var started = System.Diagnostics.Stopwatch.StartNew();
            var before = (realismTagged, realismKerbFaces, realismCars, realismLampPoles, realismProps, realismRefined);
            var grid = lastStreetGrid;
            try { AddRealismPartsTimed(module, part, mesh, trees); }
            catch (Exception exception)
            {
                // 시험 기능이 동 읽기 전체를 멈추지 않게 한다. 이 동은 현실감 덧붙이기 없이 보인다.
                Debug.LogWarning($"Hongdal realism {module.Entry.code}: {exception.GetType().Name} {exception.Message}");
                lastStreetGrid = null;
            }
            finally
            {
                realismMs += started.ElapsedMilliseconds;
                realismDongs.Add(new RealismDongRecord
                {
                    code = module.Entry.code, name = module.Entry.name, refined = realismRefined > before.realismRefined,
                    buildings = realismTagged - before.realismTagged, kerbFaces = realismKerbFaces - before.realismKerbFaces,
                    alleyCars = realismCars - before.realismCars, lampPoles = realismLampPoles - before.realismLampPoles,
                    propVertices = realismProps - before.realismProps, streetTreesMoved = grid?.Moved ?? 0, streetTreesDropped = grid?.Dropped ?? 0,
                    ms = started.ElapsedMilliseconds,
                });
            }
        }

        private void AddRealismPartsTimed(DongModuleState module, GameObject part, Mesh mesh, Mesh trees)
        {
            if (mesh == null || !EnsureRealismMaterials()) return;
            var pos = mesh.vertices;
            var uv = mesh.uv;
            var uv2 = mesh.uv2;
            var colours = mesh.colors32;
            var n = pos.Length;
            if (uv == null || uv.Length != n || uv2 == null || uv2.Length != n || colours == null || colours.Length != n) return;

            var tags = new Vector2[n];
            var shopCandidates = new List<Vector2>();
            var contact = new RealismMesh();
            var props = new RealismMesh();
            var ring = new List<Vector2>(64);
            var placed = new List<Vector3>(8);
            var i = 0;
            while (i < n)
            {
                if (uv2[i].x < 0.5f || i + 3 >= n) { i++; continue; }
                var start = i;
                var top = pos[i + 2].y;          // 벽 사각형: (a, 0) (b, 0) (b, h) (a, h)
                var height = uv[i + 2].y;        // 실제 높이 m (geometry는 14 m 넘으면 눌려 있다)
                var j = i;
                while (j + 3 < n && (uv2[j].x > 0.5f || uv2[j].y < -0.5f && uv2[j].y > -1.5f)) j += 4;
                var roofStart = j;
                while (j < n && uv2[j].x < 0.5f && uv2[j].y > -0.5f && Mathf.Abs(pos[j].y - top) < 0.01f) j++;
                var roofEnd = j;
                i = Math.Max(j, start + 1);
                if (roofEnd - roofStart < 3) continue;

                var kind = RoofKind(colours[roofStart]);
                // G3: 지붕 uv = 건물 긴 벽을 따라 잰 m(옥상 무늬의 결). 굽기에서 지붕 uv는 (0, 0)이라 다른 곳은 쓰지 않는다.
                var longest = 0f;
                var axis = Vector2.right;
                for (var g = start; g + 3 < roofStart; g += 4)
                {
                    if (uv2[g].x < 0.5f) continue;
                    var edge = new Vector2(pos[g + 1].x - pos[g].x, pos[g + 1].z - pos[g].z);
                    var length = edge.sqrMagnitude;
                    if (length <= longest) continue;
                    longest = length;
                    axis = edge / Mathf.Sqrt(length);
                }
                for (var k = roofStart; k < roofEnd; k++)
                    uv[k] = new Vector2(pos[k].x * axis.x + pos[k].z * axis.y, -pos[k].x * axis.y + pos[k].z * axis.x);
                var hash = RealismHash(pos[start].x, pos[start].z);
                var seed = (hash & 0xFFFF) / 65536f;
                var tag = new Vector2(kind + seed * 0.998f, height);
                for (var k = start; k < roofEnd; k++) tags[k] = tag;
                realismTagged++;

                // 접지 그늘: 벽의 밑변마다 양쪽으로 옅은 띠(안쪽은 건물에 가린다)
                for (var g = start; g + 3 < roofStart; g += 4)
                {
                    if (uv2[g].x < 0.5f) continue;
                    var a = new Vector2(pos[g].x, pos[g].z);
                    var b = new Vector2(pos[g + 1].x, pos[g + 1].z);
                    var along = b - a;
                    var length = along.magnitude;
                    if (length < 0.3f) continue;
                    var side = new Vector2(-along.y, along.x) / length * 1.9f;
                    contact.Strip(a, b, side);
                    contact.Strip(a, b, -side);
                    realismContacts++;
                }

                // 밤 가게 불빛(표현): 근린생활 · 상업(갈래 4) 건물의 3 m 넘는 벽마다, 벽 바깥 2.2 m 보도에 불빛 한 점(길가 불빛 그림에 찍는다)
                if (kind == 4 && shopCandidates.Count < 4000 && roofEnd > roofStart)
                {
                    var middle = Vector2.zero;
                    for (var k = roofStart; k < roofEnd; k++) middle += new Vector2(pos[k].x, pos[k].z);
                    middle /= roofEnd - roofStart;
                    for (var g = start; g + 3 < roofStart; g += 4)
                    {
                        if (uv2[g].x < 0.5f) continue;
                        var a = new Vector2(pos[g].x, pos[g].z);
                        var b = new Vector2(pos[g + 1].x, pos[g + 1].z);
                        var along = b - a;
                        var length = along.magnitude;
                        if (length < 3f) continue;
                        var mid = (a + b) * 0.5f;
                        var outward = new Vector2(-along.y, along.x) / length;
                        if (Vector2.Dot(outward, mid - middle) < 0f) outward = -outward;
                        shopCandidates.Add(mid + outward * 2.2f);
                    }
                }

                ring.Clear();
                for (var k = roofStart; k < roofEnd; k++) ring.Add(new Vector2(pos[k].x, pos[k].z));
                AddRoofProps(props, ring, placed, top, height, kind, hash, tag, colours[roofStart]);
            }
            // 정밀화(가 등급만): 보도 · 점자블록 · 연석선을 0.15 m 올리고(셰이더가 uv3.y만큼), 횡단보도 줄 끝 2 m는 차도 높이로 내린다(경사석).
            //  연석선(갈래 8)의 차도 쪽 변에 연석 세로면을 세운다. 높이 · 경사 길이는 표현값, 보도 면 · 횡단보도 자리는 서울시 자료(굽기 입력).
            var grid = lastStreetGrid;
            lastStreetGrid = null;
            grid?.CollectRoads(roadCells);
            if (grid != null)
                foreach (var spot in shopCandidates)
                    if (shopLights.Count < 60000 && grid.Open(spot.x, spot.y)) shopLights.Add(spot);
            grid?.CollectBuildings(buildingCells, PeopleRadius + 20f);
            if (grid != null && RefineCodes().Contains(module.Entry.code))
            {
                realismRefined++;
                var raise = new float[n];
                for (var k = 0; k < n; k++)
                {
                    if (uv2[k].x > 0.5f || pos[k].y > 0.05f || tags[k].x > 0.5f) continue;
                    var tag = uv[k].x;
                    // 시설물대(갈래 9)는 올리지 않는다: 굽기 자료에서 86%가 차도 면 위에 겹쳐 있어, 올리면 차도 · 횡단보도를 덮는다(g1d 촬영).
                    if (Mathf.Abs(tag - 5f) > 0.3f && Mathf.Abs(tag - 7f) > 0.3f && Mathf.Abs(tag - 8f) > 0.3f) continue;
                    var ramp = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 2.4f, grid.CrossingDistance(pos[k], 3)));
                    raise[k] = SidewalkRaise * ramp;
                    tags[k] = new Vector2(0f, raise[k]);
                }
                var watch = System.Diagnostics.Stopwatch.StartNew();
                // 연석 세로면 · 측구 띠는 사가정역 둘레(동 메시 가운데가 1.8 km 안)만 만든다. 가장 가까운 줌에서도 1 px 안팎이라
                //  25동 전부에 두면 정점만 530만 개 늘었다(g2h). 보도 올림(셰이더)은 「가」 동 전부 그대로.
                var middle = mesh.bounds.center;                      // 읽는 때는 동 넓이 중심(CentreEast/North)이 아직 비어 있어 메시 범위의 가운데를 쓴다
                var kerbNear = middle.x * middle.x + middle.z * middle.z < 1800f * 1800f;
                var triangles = kerbNear ? mesh.triangles : Array.Empty<int>();
                var edges = new Dictionary<long, int>(EdgeKeys.Instance);       // 변 → 맞은편 꼭짓점(두 번 나오면 -1, 안쪽 변)
                void Edge(int p, int q, int r)
                {
                    var key = ((long)Math.Min(p, q) << 32) | (uint)Math.Max(p, q);
                    edges[key] = edges.ContainsKey(key) ? -1 : r;
                }
                for (var t = 0; t + 2 < triangles.Length; t += 3)
                {
                    int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                    if (Mathf.Abs(uv[a].x - 8f) > 0.3f || uv2[a].x > 0.5f || pos[a].y > 0.05f) continue;
                    Edge(a, b, c);
                    Edge(b, c, a);
                    Edge(c, a, b);
                }
                var kerbColour = new Color32(176, 178, 174, 255);
                var gutterShade = new Color32(64, 66, 64, 255);        // 연석 밑 그늘 줄(g1f: 92는 차도와 잘 갈리지 않았다)
                var gutterColour = new Color32(158, 158, 152, 255);
                foreach (var edge in edges)
                {
                    if (edge.Value < 0) continue;
                    var ia = (int)(edge.Key >> 32);
                    var ib = (int)(edge.Key & 0xFFFFFFFF);
                    var pa = pos[ia];
                    var pb = pos[ib];
                    var along = new Vector2(pb.x - pa.x, pb.z - pa.z);
                    if (along.sqrMagnitude < 0.0025f || raise[ia] + raise[ib] < 0.02f) continue;
                    var mid = new Vector2((pa.x + pb.x) * 0.5f, (pa.z + pb.z) * 0.5f);
                    var side = new Vector2(-along.y, along.x).normalized * 0.7f;
                    var third = pos[edge.Value];
                    if (Vector2.Dot(side, new Vector2(third.x, third.z) - mid) > 0f) side = -side;      // 연석선 조각의 바깥쪽
                    if (!grid.RoadAt(mid.x + side.x, mid.y + side.y) || grid.RoadAt(mid.x - side.x, mid.y - side.y)) continue;   // 바깥은 차도, 안은 보도인 변만
                    props.Wall(new Vector3(pa.x, 0.06f, pa.z), new Vector3(pb.x, 0.06f, pb.z),
                        new Vector3(pb.x, pb.y + raise[ib], pb.z), new Vector3(pa.x, pa.y + raise[ia], pa.z), kerbColour);
                    // 측구(L형, 표현): 연석 밑 차도 쪽 0.45 m 콘크리트 띠. 연석 쪽은 그늘로 어둡다. 가장 가까이(가로 200 m, 1 m ≈ 6 px)에서
                    //  0.15 m 연석 높이는 1 px도 안 되어, 연석이 있다는 것은 이 띠와 그늘 줄로 읽힌다.
                    var gutter = side.normalized * 0.4f;
                    props.Gutter(new Vector3(pa.x, 0.0625f, pa.z), new Vector3(pb.x, 0.0625f, pb.z),
                        new Vector3(pb.x + gutter.x, 0.0625f, pb.z + gutter.y), new Vector3(pa.x + gutter.x, 0.0625f, pa.z + gutter.y),
                        gutterShade, gutterColour);
                    realismKerbFaces++;
                }
                realismKerbMs += watch.ElapsedMilliseconds;
                AddAlleyCars(props, pos, uv, uv2, grid);
                // 길가 나무도 올린 보도 위에 선다(나무 셰이더 _HongdalRaise, 렌더러마다)
                foreach (var renderer in module.Renderers)
                {
                    var filter = renderer != null ? renderer.GetComponent<MeshFilter>() : null;
                    if (filter == null || trees == null || filter.sharedMesh != trees) continue;
                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block);
                    block.SetFloat(RaiseId, SidewalkRaise);
                    renderer.SetPropertyBlock(block);
                }
            }
            AddSurveyLampPoles(props, pos, grid);
            mesh.uv = uv;                              // 지붕 uv(G3)
            mesh.SetUVs(2, tags);
            if (UrpActive) mesh.RecalculateNormals();      // URP 그림자 맵의 법선 치우침(normal bias)에 쓴다. 바탕 셰이더는 법선을 읽지 않는다

            // URP: 이 동의 건물 · 나무 렌더러가 그림자를 드리운다(켜고 끄기는 ApplyRealismShown)
            foreach (var renderer in module.Renderers)
            {
                var filter = renderer != null ? renderer.GetComponent<MeshFilter>() : null;
                if (filter != null && (filter.sharedMesh == mesh || trees != null && filter.sharedMesh == trees)) casterRenderers.Add(renderer);
            }

            // 해 그림자(Built-in): 같은 메시를 그림자 재질로 한 번 더(벽만 눕힌다)
            var sun = AddDongPart(part, "Sun shadows", mesh, sunShadowMaterial);
            flatShadowRenderers.Add(sun);
            module.Renderers.Add(sun);
            if (trees != null && treeShadowMaterial != null)
            {
                var treeShade = AddDongPart(part, "Tree sun shadows", trees, treeShadowMaterial);
                flatShadowRenderers.Add(treeShade);
                module.Renderers.Add(treeShade);
            }
            if (contact.Count > 0)
            {
                var shadeMesh = contact.Build("Dong " + module.Entry.code + " contact shade", false);
                dioramaMeshes.Add(shadeMesh);
                var shadeRenderer = AddDongPart(part, "Contact shade", shadeMesh, contactMaterial);
                realismRenderers.Add(shadeRenderer);
                module.Renderers.Add(shadeRenderer);
            }
            if (props.Count > 0)
            {
                realismProps += props.Count;
                var propMesh = props.Build("Dong " + module.Entry.code + " roof props", true);
                if (UrpActive) propMesh.RecalculateNormals();
                dioramaMeshes.Add(propMesh);
                var propRenderer = AddDongPart(part, "Roof props", propMesh, dioramaMaterial);
                realismRenderers.Add(propRenderer);
                casterRenderers.Add(propRenderer);
                module.Renderers.Add(propRenderer);
            }
            realismApplied = -1;
        }

        // 구울 때 고른 지붕 색(make_dong_modules.py roof_and_wall)으로 건물 갈래를 안다.
        private static int RoofKind(Color32 c)
        {
            switch (c.r << 16 | c.g << 8 | c.b)
            {
                case 0x4F7F6C: case 0x487564: case 0x568872: return 2;      // 초록 지붕(주택)
                case 0x8795A5: case 0x7D8C9D: return 3;                     // 공동주택
                case 0x8C8FA1: case 0x7F8496: case 0x9396A7: return 4;      // 근린생활 · 상업
                case 0x7D8F86: case 0x86978E: return 5;                     // 공공
                case 0x8A8478: case 0x7F7A70: return 6;                     // 공장 · 창고
                default: return 1;                                          // 주택
            }
        }

        private static uint RealismHash(float x, float z)
        {
            var h = (uint)Mathf.RoundToInt(x * 10f) * 73856093u ^ (uint)Mathf.RoundToInt(z * 10f) * 19349663u;
            h ^= h >> 13;
            h *= 0x5bd1e995u;
            h ^= h >> 15;
            return h;
        }

        // 옥상 소품(표현): 주택은 물탱크 · 실외기 · 3층 넘으면 옥탑, 아파트는 기계실, 상가는 실외기 무리.
        private void AddRoofProps(RealismMesh props, List<Vector2> ring, List<Vector3> placed, float top, float height, int kind, uint hash, Vector2 tag, Color32 roofColour)
        {
            var area = 0f;
            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
            var longest = 0f;
            var axis = new Vector2(1f, 0f);
            for (var k = 0; k < ring.Count; k++)
            {
                var a = ring[k];
                var b = ring[(k + 1) % ring.Count];
                area += a.x * b.y - b.x * a.y;
                minX = Mathf.Min(minX, a.x); maxX = Mathf.Max(maxX, a.x);
                minZ = Mathf.Min(minZ, a.y); maxZ = Mathf.Max(maxZ, a.y);
                var edge = (b - a).magnitude;
                if (edge > longest) { longest = edge; axis = (b - a) / edge; }
            }
            area = Mathf.Abs(area) * 0.5f;
            if (area < 30f) return;
            var rng = new System.Random((int)(hash & 0x7FFFFFFF));
            placed.Clear();
            var across = new Vector2(-axis.y, axis.x);
            var wallGrey = new Color32(206, 204, 197, 255);

            if (kind == 3)
            {
                var rooms = area > 420f ? 2 : 1;
                for (var r = 0; r < rooms; r++)
                    if (PlaceOnRoof(ring, minX, minZ, maxX, maxZ, 3.4f, placed, rng, out var at))
                        props.Box(at, top, axis, across, 2.6f + (float)rng.NextDouble(), 1.8f + 0.6f * (float)rng.NextDouble(), 3.0f, wallGrey, roofColour, tag, true);
                return;
            }
            if (kind == 1 || kind == 2)
            {
                if (height >= 8.5f && area >= 65f && rng.NextDouble() < 0.6)
                    if (PlaceOnRoof(ring, minX, minZ, maxX, maxZ, 2.1f, placed, rng, out var at))
                        props.Box(at, top, axis, across, 1.5f + 0.3f * (float)rng.NextDouble(), 1.2f + 0.3f * (float)rng.NextDouble(), 2.4f, wallGrey, roofColour, tag, true);   // 옥탑
                var tanks = rng.NextDouble() < 0.68 ? (area > 160f && rng.NextDouble() < 0.3 ? 2 : 1) : 0;
                var pick = rng.NextDouble();
                var tankColour = pick < 0.45 ? new Color32(224, 216, 192, 255) : pick < 0.8 ? new Color32(122, 166, 196, 255) : new Color32(192, 197, 202, 255);
                for (var t = 0; t < tanks; t++)
                    if (PlaceOnRoof(ring, minX, minZ, maxX, maxZ, 1.1f, placed, rng, out var at))
                    {
                        var radius = 0.62f + 0.22f * (float)rng.NextDouble();
                        props.Box(at, top, axis, across, radius + 0.15f, radius + 0.15f, 0.35f, new Color32(112, 116, 122, 255), new Color32(112, 116, 122, 255), tag, false);   // 받침
                        props.Cylinder(at, top + 0.35f, radius, 1.3f + 0.5f * (float)rng.NextDouble(), tankColour, tag);
                    }
                var units = rng.Next(0, area > 120f ? 4 : 3);
                for (var u = 0; u < units; u++)
                    if (PlaceOnRoof(ring, minX, minZ, maxX, maxZ, 0.7f, placed, rng, out var at))
                        props.Box(at, top, axis, across, 0.45f, 0.18f, 0.66f, new Color32(210, 212, 208, 255), new Color32(222, 224, 220, 255), tag, false);       // 실외기
                return;
            }
            if (kind == 4 || kind == 5)
            {
                var units = kind == 4 ? 2 + rng.Next(0, Mathf.Clamp((int)(area / 90f), 1, 6)) : 1 + rng.Next(0, 3);
                for (var u = 0; u < units; u++)
                    if (PlaceOnRoof(ring, minX, minZ, maxX, maxZ, 0.8f, placed, rng, out var at))
                        props.Box(at, top, axis, across, 0.5f, 0.2f, 0.7f, new Color32(210, 212, 208, 255), new Color32(222, 224, 220, 255), tag, false);
                if (kind == 4 && rng.NextDouble() < 0.35 && PlaceOnRoof(ring, minX, minZ, maxX, maxZ, 1.1f, placed, rng, out var tankAt))
                    props.Cylinder(tankAt, top, 0.75f, 1.6f, new Color32(192, 197, 202, 255), tag);
            }
        }

        // 지붕 안쪽 자리: 가장자리(테두리 0.45 m)에서 clearance만큼 떨어지고, 놓인 소품과 겹치지 않는 곳.
        private static bool PlaceOnRoof(List<Vector2> ring, float minX, float minZ, float maxX, float maxZ, float clearance, List<Vector3> placed, System.Random rng, out Vector2 at)
        {
            for (var attempt = 0; attempt < 14; attempt++)
            {
                var x = Mathf.Lerp(minX, maxX, (float)rng.NextDouble());
                var z = Mathf.Lerp(minZ, maxZ, (float)rng.NextDouble());
                if (!InsideRing(ring, x, z) || EdgeDistance(ring, x, z) < clearance + 0.5f) continue;
                var free = true;
                foreach (var p in placed)
                    if ((p.x - x) * (p.x - x) + (p.y - z) * (p.y - z) < (p.z + clearance + 0.3f) * (p.z + clearance + 0.3f)) { free = false; break; }
                if (!free) continue;
                placed.Add(new Vector3(x, z, clearance));
                at = new Vector2(x, z);
                return true;
            }
            at = default;
            return false;
        }

        private static bool InsideRing(List<Vector2> ring, float x, float z)
        {
            var inside = false;
            for (int a = 0, b = ring.Count - 1; a < ring.Count; b = a++)
            {
                var pa = ring[a];
                var pb = ring[b];
                if (pa.y > z != pb.y > z && x < (pb.x - pa.x) * (z - pa.y) / (pb.y - pa.y) + pa.x) inside = !inside;
            }
            return inside;
        }

        private static float EdgeDistance(List<Vector2> ring, float x, float z)
        {
            var best = float.MaxValue;
            var p = new Vector2(x, z);
            for (var k = 0; k < ring.Count; k++)
            {
                var a = ring[k];
                var b = ring[(k + 1) % ring.Count];
                var ab = b - a;
                var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                best = Mathf.Min(best, (a + ab * t - p).magnitude);
            }
            return best;
        }

        // 작은 메시 짓기: 접지 그늘 띠 · 옥상 소품. uv2.y: -3 소품(정점 색이 그대로 바탕색), -5 옥탑 벽(셰이더가 그 건물의 벽 재질을 고른다), 0 옥탑 지붕.
        private sealed class RealismMesh
        {
            private readonly List<Vector3> v = new();
            private readonly List<Color32> c = new();
            private readonly List<Vector2> u2 = new();
            private readonly List<Vector2> u3 = new();
            private readonly List<int> t = new();
            public int Count => v.Count;

            public void Strip(Vector2 a, Vector2 b, Vector2 side)
            {
                const float y = 0.2f;              // 올린 보도(가 등급) 위
                var start = v.Count;
                v.Add(new Vector3(a.x, y, a.y));
                v.Add(new Vector3(b.x, y, b.y));
                v.Add(new Vector3(b.x + side.x, y, b.y + side.y));
                v.Add(new Vector3(a.x + side.x, y, a.y + side.y));
                c.Add(new Color32(255, 255, 255, 255));
                c.Add(new Color32(255, 255, 255, 255));
                c.Add(new Color32(255, 255, 255, 0));
                c.Add(new Color32(255, 255, 255, 0));
                for (var k = 0; k < 4; k++) { u2.Add(Vector2.zero); u3.Add(Vector2.zero); }
                t.Add(start); t.Add(start + 1); t.Add(start + 2);
                t.Add(start); t.Add(start + 2); t.Add(start + 3);
            }

            // 측구 띠: 안쪽(연석 쪽) 두 점과 바깥 두 점의 색이 다르다(그늘 → 콘크리트)
            public void Gutter(Vector3 a, Vector3 b, Vector3 cc, Vector3 d, Color32 inner, Color32 outer)
            {
                var start = v.Count;
                v.Add(a); v.Add(b); v.Add(cc); v.Add(d);
                c.Add(inner); c.Add(inner); c.Add(outer); c.Add(outer);
                for (var k = 0; k < 4; k++) { u2.Add(new Vector2(0f, -3f)); u3.Add(Vector2.zero); }
                t.Add(start); t.Add(start + 1); t.Add(start + 2);
                t.Add(start); t.Add(start + 2); t.Add(start + 3);
            }

            // 연석 세로면 같은 벽 조각(소품 표시, 건물 꼬리표 없음)
            public void Wall(Vector3 a, Vector3 b, Vector3 cc, Vector3 d, Color32 colour) => Quad(a, b, cc, d, colour, -3f, Vector2.zero);

            private void Quad(Vector3 a, Vector3 b, Vector3 cc, Vector3 d, Color32 colour, float flag, Vector2 tag)
            {
                var start = v.Count;
                v.Add(a); v.Add(b); v.Add(cc); v.Add(d);
                for (var k = 0; k < 4; k++) { c.Add(colour); u2.Add(new Vector2(0f, flag)); u3.Add(tag); }
                t.Add(start); t.Add(start + 1); t.Add(start + 2);
                t.Add(start); t.Add(start + 2); t.Add(start + 3);
            }

            // 바닥 가운데 at, 바닥 높이 y, 축 방향으로 반 길이 half · 가로로 반 너비 halfAcross, 높이 h. room이면 옆면은 옥탑 벽, 윗면은 지붕.
            public void Box(Vector2 at, float y, Vector2 axis, Vector2 across, float half, float halfAcross, float h, Color32 side, Color32 topColour, Vector2 tag, bool room)
            {
                var ax = axis * half;
                var ac = across * halfAcross;
                Vector3 P(Vector2 q, float yy) => new Vector3(q.x, yy, q.y);
                var c0 = at - ax - ac;
                var c1 = at + ax - ac;
                var c2 = at + ax + ac;
                var c3 = at - ax + ac;
                var wallFlag = room ? -5f : -3f;
                Quad(P(c0, y), P(c1, y), P(c1, y + h), P(c0, y + h), side, wallFlag, tag);
                Quad(P(c1, y), P(c2, y), P(c2, y + h), P(c1, y + h), side, wallFlag, tag);
                Quad(P(c2, y), P(c3, y), P(c3, y + h), P(c2, y + h), side, wallFlag, tag);
                Quad(P(c3, y), P(c0, y), P(c0, y + h), P(c3, y + h), side, wallFlag, tag);
                Quad(P(c0, y + h), P(c1, y + h), P(c2, y + h), P(c3, y + h), topColour, room ? 0f : -3f, tag);
            }

            // 보안등 등머리: 상자 하나, 표시 -7(밤에 셰이더가 빛나게 한다)
            public void LampHead(Vector2 at, float y, Vector2 axis, Vector2 across, float half, float halfAcross, float h, Color32 colour) =>
                Glow(at, y, axis, across, half, halfAcross, h, colour, -7f);

            // 밤에 빛나는 작은 상자: 표시 -7 흰빛(등머리 · 차 앞등), -9 붉은빛(차 뒷등)
            public void Glow(Vector2 at, float y, Vector2 axis, Vector2 across, float half, float halfAcross, float h, Color32 colour, float flag)
            {
                var ax = axis * half;
                var ac = across * halfAcross;
                Vector3 P(Vector2 q, float yy) => new Vector3(q.x, yy, q.y);
                var c0 = at - ax - ac;
                var c1 = at + ax - ac;
                var c2 = at + ax + ac;
                var c3 = at - ax + ac;
                Quad(P(c0, y), P(c1, y), P(c1, y + h), P(c0, y + h), colour, flag, Vector2.zero);
                Quad(P(c1, y), P(c2, y), P(c2, y + h), P(c1, y + h), colour, flag, Vector2.zero);
                Quad(P(c2, y), P(c3, y), P(c3, y + h), P(c2, y + h), colour, flag, Vector2.zero);
                Quad(P(c3, y), P(c0, y), P(c0, y + h), P(c3, y + h), colour, flag, Vector2.zero);
                Quad(P(c0, y + h), P(c1, y + h), P(c2, y + h), P(c3, y + h), colour, flag, Vector2.zero);
                Quad(P(c3, y), P(c2, y), P(c1, y), P(c0, y), colour, flag, Vector2.zero);
            }

            public void Cylinder(Vector2 at, float y, float radius, float h, Color32 colour, Vector2 tag)
            {
                const int sides = 8;
                for (var k = 0; k < sides; k++)
                {
                    var a0 = k * Mathf.PI * 2f / sides;
                    var a1 = (k + 1) * Mathf.PI * 2f / sides;
                    var p0 = at + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * radius;
                    var p1 = at + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * radius;
                    Quad(new Vector3(p0.x, y, p0.y), new Vector3(p1.x, y, p1.y), new Vector3(p1.x, y + h, p1.y), new Vector3(p0.x, y + h, p0.y), colour, -3f, tag);
                }
                var centre = v.Count;
                v.Add(new Vector3(at.x, y + h + 0.08f, at.y));
                c.Add(colour); u2.Add(new Vector2(0f, -3f)); u3.Add(tag);
                for (var k = 0; k < sides; k++)
                {
                    var a0 = k * Mathf.PI * 2f / sides;
                    v.Add(new Vector3(at.x + Mathf.Cos(a0) * radius, y + h, at.y + Mathf.Sin(a0) * radius));
                    c.Add(colour); u2.Add(new Vector2(0f, -3f)); u3.Add(tag);
                }
                for (var k = 0; k < sides; k++)
                {
                    t.Add(centre); t.Add(centre + 1 + k); t.Add(centre + 1 + (k + 1) % sides);
                }
            }

            public Mesh Build(string name, bool full)
            {
                var mesh = new Mesh { name = name, indexFormat = v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                mesh.SetVertices(v);
                mesh.SetColors(c);
                if (full)
                {
                    var zeros = new List<Vector2>(v.Count);
                    for (var k = 0; k < v.Count; k++) zeros.Add(Vector2.zero);
                    mesh.SetUVs(0, zeros);
                    mesh.SetUVs(1, u2);
                    mesh.SetUVs(2, u3);
                }
                mesh.SetTriangles(t, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
