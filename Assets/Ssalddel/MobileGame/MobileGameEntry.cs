using System;
using System.IO;
using UnityEngine;

namespace Ssalddel.MobileGame
{
    /// <summary>휴대폰 안의 코어·화면·전용 저장을 연결한다. 실제 업무 서버를 연결하지 않는다.</summary>
    public sealed class MobileGameEntry : MonoBehaviour
    {
        public MobileGameController View { get; private set; }
        public MobileGameSession Session { get; private set; }
        private void Start()
        {
            Application.targetFrameRate = 30;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;
            var sample = Resources.Load<TextAsset>("HongdalMobile/delivery-sample");
            var graph = Resources.Load<TextAsset>("HongdalMobile/road-graph");
            if (sample == null || graph == null)
                throw new InvalidOperationException("모바일 배달 표본과 도로 자료를 확인해 주세요.");
            var args = Environment.GetCommandLineArgs();
            var proofIndex = Array.IndexOf(args, "--mobile-proof");
            var proof = proofIndex >= 0 && proofIndex + 1 < args.Length ? args[proofIndex + 1] : null;
            var storage = string.IsNullOrWhiteSpace(proof)
                ? Path.Combine(Application.persistentDataPath, "mobile-game.v1")
                : Path.Combine(Path.GetFullPath(proof), "session");
            Session = MobileGameSession.Attach(gameObject, sample.text, graph.text, storage);
            Session.SetFileExchange(MobileGameFileExchangeFactory.Create(gameObject));
            View = MobileGameBootstrap.Build(transform);
            View.AttachSession(Session);
            var font = Resources.Load<Font>("HongdalMobile/Korean");
            if (font != null) View.ConfigureFont(font);
            if (!string.IsNullOrWhiteSpace(proof))
            {
                var check = gameObject.AddComponent<MobileGamePlayerProof>();
                check.Begin(this, Path.GetFullPath(proof));
            }
        }
    }
}
