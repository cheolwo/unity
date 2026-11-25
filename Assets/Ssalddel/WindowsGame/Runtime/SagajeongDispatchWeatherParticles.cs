using UnityEngine;
using UnityEngine.Rendering;

namespace Ssalddel.WindowsGame
{
    // 계절 · 변화 그림 구조 E3 (2026-11-23): 동 디오라마의 비 · 눈을 화면 위 그림(IMGUI)에서 땅 위 알갱이로 옮긴다.
    //  - 빗줄기 · 눈송이가 건물 뒤로 지나가면 가려지고, 보는 방향 · 배율이 바뀌어도 땅 위에서 내린다.
    //  - 파티클 모듈 없이 네모 메시 하나를 셰이더(HongdalParticle)가 시간으로 움직인다(빌드 프로젝트에 모듈이 없다).
    //  - 언제 내리는지는 실제 달력(그날 젖은 시간 · 새 눈)에서 온다. 굵기 · 속도 · 양은 화면 크기에 맞춘 표현값이다.
    //  - 자료 창 「비 · 눈」이 꺼져 있거나 동 디오라마가 아니면 멈춘다. 구 지도는 전처럼 화면 위 그림이다.
    public sealed partial class SagajeongDispatchView
    {
        private const int RainDrops = 6000;
        private const int SnowFlakes = 3600;
        private GameObject rainObject, snowObject;
        private Material rainMaterial, snowMaterial;
        private bool rainPlaying, snowPlaying;
        private static readonly int WeatherAreaId = Shader.PropertyToID("_Area");
        private static readonly int WeatherShapeId = Shader.PropertyToID("_Shape");
        private static readonly int WeatherTintId = Shader.PropertyToID("_Tint");
        private static readonly int WeatherTimeId = Shader.PropertyToID("_WeatherTime");
        private static readonly int WeatherShape2Id = Shader.PropertyToID("_Shape2");

        private bool WeatherParticlesActive => rainPlaying || snowPlaying;

        private static Mesh MakeWeatherMesh(string name, int count, int seed)
        {
            var positions = new Vector3[count * 4];
            var corners = new Vector2[count * 4];
            var seeds = new Vector2[count * 4];
            var colours = new Color32[count * 4];
            var indices = new int[count * 6];
            for (var i = 0; i < count; i++)
            {
                var a = SeasonHash(i, 1, seed) / 65536f;
                var b = SeasonHash(i, 2, seed) / 65536f;
                var c = (byte)(SeasonHash(i, 3, seed) >> 8);
                var d = (byte)(SeasonHash(i, 4, seed) >> 8);
                for (var k = 0; k < 4; k++)
                {
                    corners[i * 4 + k] = new Vector2(k == 1 || k == 2 ? 1f : 0f, k >= 2 ? 1f : 0f);
                    seeds[i * 4 + k] = new Vector2(a, b);
                    colours[i * 4 + k] = new Color32(c, d, 0, 255);
                }
                indices[i * 6] = i * 4; indices[i * 6 + 1] = i * 4 + 1; indices[i * 6 + 2] = i * 4 + 2;
                indices[i * 6 + 3] = i * 4; indices[i * 6 + 4] = i * 4 + 2; indices[i * 6 + 5] = i * 4 + 3;
            }
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.vertices = positions;
            mesh.uv = corners;
            mesh.uv2 = seeds;
            mesh.colors32 = colours;
            mesh.triangles = indices;
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);      // 정점은 셰이더가 옮긴다. 잘라내지 않게 크게
            return mesh;
        }

        private GameObject MakeWeatherObject(string name, int count, int seed, out Material material)
        {
            material = null;
            var shader = Resources.Load<Shader>("HongdalParticle");
            if (shader == null) return null;
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = MakeWeatherMesh(name, count, seed);
            var renderer = go.AddComponent<MeshRenderer>();
            material = new Material(shader) { name = name };
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            go.SetActive(false);
            return go;
        }

        private void StopWeatherParticles()
        {
            if (rainObject != null && rainObject.activeSelf) rainObject.SetActive(false);
            if (snowObject != null && snowObject.activeSelf) snowObject.SetActive(false);
            rainPlaying = snowPlaying = false;
        }

        // LateUpdate(SagajeongDispatchDiorama.cs): 동 디오라마가 보일 때 매 화면. halfHeight = 화면 세로 절반에 담기는 길이(m).
        private void SyncWeatherParticles(Vector3 focus, float halfHeight, float pitchDegrees)
        {
            ReadDataControl();
            var read = TodayRead();
            var rain = weatherOn ? RainNow() : 0f;
            var snowDay = weatherOn ? read.Snow : null;
            var snowing = snowDay != null && snowDay.newSnow > 0f;
            if (rain <= 0f && !snowing)
            {
                StopWeatherParticles();
                return;
            }
            if (rainObject == null) rainObject = MakeWeatherObject("Hongdal rain", RainDrops, 71, out rainMaterial);
            if (snowObject == null) snowObject = MakeWeatherObject("Hongdal snow", SnowFlakes, 73, out snowMaterial);
            if (rainObject == null || snowObject == null) return;

            var h = Mathf.Max(20f, halfHeight);
            var pitch = Mathf.Clamp(pitchDegrees, 20f, 90f) * Mathf.Deg2Rad;
            var side = 2.2f * Mathf.Max(h * Screen.width / Mathf.Max(1f, Screen.height), h / Mathf.Sin(pitch));
            var top = 0.7f * h;
            var night = skyOn ? NightFactor() : 0f;
            var time = Time.unscaledTime;
            var at = new Vector3(focus.x, 0f, focus.z);

            rainPlaying = rain > 0f;
            if (rainObject.activeSelf != rainPlaying) rainObject.SetActive(rainPlaying);
            if (rainPlaying)
            {
                var strength = Mathf.Clamp01(rain / 30f);
                rainObject.transform.SetPositionAndRotation(at, Quaternion.identity);
                // 적게 오는 날은 앞쪽 알갱이만 쓴다: 정점은 다 있지만 굵기를 0으로 둘 수는 없으니 색의 알파로 줄인다.
                rainMaterial.SetVector(WeatherAreaId, new Vector4(side * (1.15f - 0.35f * strength), top, 0.75f * h, 0.05f * h));
                rainMaterial.SetVector(WeatherShapeId, new Vector4(0.0045f * h, -0.08f * 0.75f * h, 0f, 0f));
                rainMaterial.SetColor(WeatherTintId, Color.Lerp(new Color(0.80f, 0.88f, 0.96f, 0.32f + 0.3f * strength), new Color(0.58f, 0.66f, 0.78f, 0.30f + 0.2f * strength), night));
                rainMaterial.SetFloat(WeatherTimeId, time);
                rainMaterial.SetVector(WeatherShape2Id, new Vector4(8f, 0f, 0f, 0f));       // 빗방울이 실제로 떨어지는 빠르기(m/s): 바람에 비껴 내리는 각도
                rainMaterial.SetVector(WeatherShapeId, new Vector4(0.0045f * h, 0f, 0f, 0f));
            }
            snowPlaying = snowing;
            if (snowObject.activeSelf != snowPlaying) snowObject.SetActive(snowPlaying);
            if (snowPlaying)
            {
                var strength = Mathf.Clamp01(snowDay.newSnow / 8f);
                snowObject.transform.SetPositionAndRotation(at, Quaternion.identity);
                snowMaterial.SetVector(WeatherAreaId, new Vector4(side * (1.2f - 0.3f * strength), top, 0.07f * h, 0f));
                snowMaterial.SetVector(WeatherShapeId, new Vector4(0.010f * h, 0.01f * h, 0.012f * h, 1f));
                snowMaterial.SetColor(WeatherTintId, Color.Lerp(new Color(1f, 1f, 1f, 0.9f), new Color(0.82f, 0.87f, 0.96f, 0.85f), night));
                snowMaterial.SetFloat(WeatherTimeId, time);
                snowMaterial.SetVector(WeatherShape2Id, new Vector4(1.0f, 0f, 0f, 0f));     // 눈송이는 천천히(약 1 m/s): 바람에 크게 흩날린다
            }
        }
    }
}
