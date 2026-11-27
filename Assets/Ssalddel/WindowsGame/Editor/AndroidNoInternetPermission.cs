#if UNITY_ANDROID
using System.IO;
using System.Xml;
using UnityEditor.Android;

namespace Ssalddel.WindowsGame.Editor
{
    // 폰판은 인터넷을 쓰지 않는다(2026-11-25, 공개 배포 점검). Unity가 기본으로 붙이는 INTERNET 권한을 최종 매니페스트에서 뺀다.
    // - unityLibrary 매니페스트의 INTERNET 줄을 지운다.
    // - 앱(launcher) 매니페스트에 tools:node="remove"를 넣어, 다른 라이브러리가 붙이는 INTERNET도 병합 때 빠지게 한다.
    // 음악 읽기(SagajeongDispatchMusic)는 file:// 경로만 쓰므로 권한이 필요 없다.
    public sealed class AndroidNoInternetPermission : IPostGenerateGradleAndroidProject
    {
        private const string AndroidNs = "http://schemas.android.com/apk/res/android";
        private const string ToolsNs = "http://schemas.android.com/tools";
        private const string Internet = "android.permission.INTERNET";

        public int callbackOrder => 900;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var library = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (File.Exists(library)) Edit(library, false);
            var root = Path.GetDirectoryName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var launcher = root == null ? null : Path.Combine(root, "launcher", "src", "main", "AndroidManifest.xml");
            if (launcher != null && File.Exists(launcher)) Edit(launcher, true);
        }

        private static void Edit(string file, bool addRemoveMarker)
        {
            var doc = new XmlDocument { PreserveWhitespace = true };
            doc.Load(file);
            var manifest = doc.DocumentElement;
            if (manifest == null) return;
            var found = new System.Collections.Generic.List<XmlElement>();
            foreach (XmlNode child in manifest.ChildNodes)
            {
                if (child is XmlElement element && element.LocalName == "uses-permission" && element.GetAttribute("name", AndroidNs) == Internet)
                    found.Add(element);
            }
            foreach (var element in found) manifest.RemoveChild(element);
            if (addRemoveMarker)
            {
                if (!manifest.HasAttribute("xmlns:tools"))
                {
                    var declaration = doc.CreateAttribute("xmlns", "tools", "http://www.w3.org/2000/xmlns/");
                    declaration.Value = ToolsNs;
                    manifest.Attributes.Append(declaration);
                }
                var marker = doc.CreateElement("uses-permission");
                var name = doc.CreateAttribute("android", "name", AndroidNs);
                name.Value = Internet;
                marker.Attributes.Append(name);
                var removeNode = doc.CreateAttribute("tools", "node", ToolsNs);
                removeNode.Value = "remove";
                marker.Attributes.Append(removeNode);
                manifest.PrependChild(marker);
            }
            doc.Save(file);
        }
    }
}
#endif
