using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
        "Unity 소비 계약·결정성·Save/Replay 또는 회귀 증거를 검증한다.",
        Boundary = "자동 시험과 코드 검증을 실제 Play Mode·Game View 증거와 구분한다.")]
    public sealed class Editor메뉴경계Tests
    {
        private static readonly HashSet<string> AllowedRoots = new(
            new[]
            {
                "통합 월드",
                "검토실",
                "공간·경관",
                "검증",
                "실험·참고 Scene",
            },
            StringComparer.Ordinal);

        private static readonly Regex MenuPattern = new(
            @"\[MenuItem\(""(?<path>Ssalddel/(?<root>[^/""]+)(?:/[^""]*)?)""",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        [Test]
        public void 공식Editor메뉴는_다섯개_목적Root만_사용한다()
        {
            var registrations = ReadRegistrations();
            var unexpected = registrations
                .Where(value => !AllowedRoots.Contains(value.Root))
                .Select(value => value.Path + " @ " + value.File)
                .ToArray();

            Assert.That(registrations, Is.Not.Empty);
            Assert.That(unexpected, Is.Empty,
                "Editor 메뉴가 목적별 Root 밖에 등록됐습니다.");
            Assert.That(registrations.Select(value => value.Root).Distinct(),
                Is.EquivalentTo(AllowedRoots));
        }

        [Test]
        public void 실험과Sample코드는_공식Editor메뉴를_등록하지않는다()
        {
            var forbidden = ReadRegistrations()
                .Where(value => value.File.StartsWith(
                            "Assets/Ssalddel/Experiments - 연구/",
                            StringComparison.Ordinal)
                        || value.File.StartsWith(
                            "Assets/Samples/",
                            StringComparison.Ordinal))
                .Select(value => value.Path + " @ " + value.File)
                .ToArray();

            Assert.That(forbidden, Is.Empty,
                "실험·Sample 메뉴는 공식 Ssalddel 메뉴에 노출하지 않습니다.");
        }

        private static List<MenuRegistration> ReadRegistrations()
        {
            var assetRoot = UnityEngine.Application.dataPath;
            var registrations = new List<MenuRegistration>();
            foreach (var file in Directory.EnumerateFiles(
                         assetRoot, "*.cs", SearchOption.AllDirectories))
            {
                var relative = "Assets" + file[assetRoot.Length..]
                    .Replace('\\', '/');
                foreach (Match match in MenuPattern.Matches(File.ReadAllText(file)))
                {
                    registrations.Add(new MenuRegistration(
                        relative,
                        match.Groups["root"].Value,
                        match.Groups["path"].Value));
                }
            }

            return registrations;
        }

        private sealed class MenuRegistration
        {
            public MenuRegistration(string file, string root, string path)
            {
                File = file;
                Root = root;
                Path = path;
            }

            public string File { get; }
            public string Root { get; }
            public string Path { get; }
        }
    }
}
