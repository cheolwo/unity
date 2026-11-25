using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 현실감 시험의 렌즈 (2026-11-24, 시안 「거리와 렌즈」 · 「창 속의 방」): 동 디오라마를 그리는 카메라에 붙는다.
    //  - 번짐(블룸): 밝은 곳(밤의 창 · 가게 · 길가 불빛)을 문턱으로 골라 1/2 … 1/32로 줄였다가 다시 키우며 더한다.
    //  - 위아래 흐림(틸트시프트): 화면 가운데 띠는 또렷하고 위아래로 갈수록 1/2 · 1/4 크기 그림 쪽으로 흐려진다.
    //  - 색 보정: 채도 조금 · 부드러운 S 대비 · 가장자리 어둡게.
    // 화면 글자 · 표식(IMGUI)은 카메라 뒤에 그려지므로 흐려지지 않는다. SagajeongDispatchRealism.cs가 켜고 끈다.
    [DisallowMultipleComponent]
    public sealed class HongdalLens : MonoBehaviour
    {
        public float Bloom = 0.2f;       // 번짐 세기
        public float Threshold = 0.75f;  // 번짐 문턱(밝기 0~1)
        public float Tilt = 0.6f;        // 위아래 흐림 세기 0~1
        public float Focus = 0.5f;       // 또렷한 띠의 가운데(화면 아래 0 ~ 위 1)
        public float Grade = 1f;         // 색 보정 세기 0~1

        private Material material;
        private readonly RenderTexture[] chain = new RenderTexture[6];
        private static readonly int ThresholdId = Shader.PropertyToID("_Threshold");
        private static readonly int LensId = Shader.PropertyToID("_Lens");
        private static readonly int BloomTexId = Shader.PropertyToID("_BloomTex");
        private static readonly int Blur1Id = Shader.PropertyToID("_Blur1");
        private static readonly int Blur2Id = Shader.PropertyToID("_Blur2");

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (material == null)
            {
                var shader = Resources.Load<Shader>("HongdalLens");
                if (shader == null || !shader.isSupported)
                {
                    Graphics.Blit(source, destination);
                    enabled = false;
                    return;
                }
                material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }
            var w = Mathf.Max(2, source.width / 2);
            var h = Mathf.Max(2, source.height / 2);
            var format = source.format;
            material.SetFloat(ThresholdId, Threshold);
            material.SetVector(LensId, new Vector4(Bloom, Tilt, Focus, Grade));

            var blur1 = RenderTexture.GetTemporary(w, h, 0, format);
            var blur2 = RenderTexture.GetTemporary(Mathf.Max(1, w / 2), Mathf.Max(1, h / 2), 0, format);
            Graphics.Blit(source, blur1, material, 1);
            Graphics.Blit(blur1, blur2, material, 1);

            var levels = 0;
            chain[0] = RenderTexture.GetTemporary(w, h, 0, format);
            Graphics.Blit(source, chain[0], material, 0);
            for (var i = 1; i < chain.Length; i++)
            {
                var cw = w >> i;
                var ch = h >> i;
                if (cw < 4 || ch < 4) break;
                chain[i] = RenderTexture.GetTemporary(cw, ch, 0, format);
                Graphics.Blit(chain[i - 1], chain[i], material, 1);
                levels = i;
            }
            for (var i = levels - 1; i >= 0; i--) Graphics.Blit(chain[i + 1], chain[i], material, 2);
            material.SetVector(LensId, new Vector4(Bloom / (levels + 1), Tilt, Focus, Grade));     // 단계마다 더해졌으니 단계 수로 나눈다

            material.SetTexture(BloomTexId, chain[0]);
            material.SetTexture(Blur1Id, blur1);
            material.SetTexture(Blur2Id, blur2);
            Graphics.Blit(source, destination, material, 3);

            for (var i = 0; i <= levels; i++)
            {
                if (chain[i] != null) RenderTexture.ReleaseTemporary(chain[i]);
                chain[i] = null;
            }
            RenderTexture.ReleaseTemporary(blur1);
            RenderTexture.ReleaseTemporary(blur2);
        }

        private void OnDestroy()
        {
            if (material != null) Destroy(material);
        }
    }
}
