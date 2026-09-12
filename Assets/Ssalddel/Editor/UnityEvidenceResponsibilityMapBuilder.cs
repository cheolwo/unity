using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Ssalddel.Contracts.Common.Metadata;
using UnityEditor;
using UnityEngine;

namespace Ssalddel.Unity.Editor
{
    public sealed class UnityEvidenceResponsibilityMapSnapshot
    {
        public string SchemaVersion = "ssalddel-evidence-responsibility-map.v1";
        public string RepositoryKey = "ssalddel-unity-product";
        public StageEntry[] Stages = Array.Empty<StageEntry>();
        public ComponentEntry[] Components = Array.Empty<ComponentEntry>();
        public CoverageEntry Coverage = new CoverageEntry();
        public DiagnosticEntry[] Diagnostics = Array.Empty<DiagnosticEntry>();
    }

    [Serializable]
    public sealed class StageEntry
    {
        public string EvidenceStage = string.Empty;
        public string ManagementSystem = string.Empty;
        public string KoreanName = string.Empty;
        public string TechnicalName = string.Empty;
        public int PrimaryCount;
        public int SecondaryCount;
    }

    [Serializable]
    public sealed class ComponentEntry
    {
        public string ComponentId = string.Empty;
        public string AssemblyName = string.Empty;
        public string ComponentName = string.Empty;
        public string SourcePath = string.Empty;
        public string CoverageState = string.Empty;
        public string PrimaryEvidenceStage = string.Empty;
        public string Responsibility = string.Empty;
        public string Boundary = string.Empty;
        public string[] SecondaryEvidenceStages = Array.Empty<string>();
        public string[] WorldInteractionIds = Array.Empty<string>();
        public string ExclusionCategory = string.Empty;
        public string ExclusionReason = string.Empty;
    }

    [Serializable]
    public sealed class CoverageEntry
    {
        public int CandidateTypeCount;
        public int AnnotatedTypeCount;
        public int ExcludedTypeCount;
        public int UncoveredTypeCount;
        public int AnnotatedMethodCount;
    }

    [Serializable]
    public sealed class DiagnosticEntry
    {
        public string Severity = string.Empty;
        public string Code = string.Empty;
        public string ComponentId = string.Empty;
        public string Message = string.Empty;
    }

    [SsalddelEvidenceResponsibility(
        SsalddelEvidenceStage.E3,
        "실제 Unity 제품 어셈블리의 E 책임 지도와 전수 누락을 검증한다.",
        Boundary = "코드 메타데이터 검증과 Play Mode·Game View 증거를 구분한다.")]
    public static class UnityEvidenceResponsibilityMapBuilder
    {
        private static readonly string[] ProductAssemblyNames =
        {
            "Ssalddel.Unity.Application",
            "Ssalddel.Unity.Bootstrap",
            "Ssalddel.Unity.Editor",
            "Ssalddel.Unity.Infrastructure",
            "Ssalddel.Unity.Presentation",
            "Ssalddel.Unity.Runtime",
            "Ssalddel.Unity.Tests.EditMode",
            "Ssalddel.Unity.Tests.PlayMode",
        };

        private static readonly string[] ExcludedAssemblyNames =
        {
            "Ssalddel.Unity.CityFarmWorldIntegration",
            "Ssalddel.Unity.CityFarmWorldIntegration.Editor",
            "Ssalddel.Unity.CityFarmWorldIntegration.EditorTests",
            "Ssalddel.Unity.SyntyCityPackIntegration.Editor",
            "Ssalddel.Unity.Farm.Sample",
            "Ssalddel.Unity.Farm.Sample.Editor",
            "Ssalddel.Unity.Farm.Sample.EditorTests",
            "Ssalddel.Unity.NpcMovement.Sample",
            "Ssalddel.Unity.ResidentialPickup.Sample",
            "Ssalddel.Unity.UrbanLogisticsCenter.Sample",
            "Ssalddel.Unity.UrbanLogisticsCenter.Sample.Editor",
            "Ssalddel.Unity.UrbanLogisticsCenter.Sample.EditorTests",
            "Ssalddel.Unity.UrbanMarket.Sample",
            "Ssalddel.Unity.UrbanMarket.Sample.Editor",
            "Ssalddel.Unity.UrbanMarket.Sample.EditorTests",
        };

        [MenuItem("Ssalddel/검증/E 책임 코드 지도 갱신")]
        public static void WriteGeneratedMap()
        {
            var snapshot = BuildSnapshot(requireCoverage: true);
            var errors = snapshot.Diagnostics.Where(item =>
                item.Severity == SsalddelEvidenceCoverageDiagnosticSeverity.Error
                    .ToString()).ToArray();
            if (errors.Length > 0)
                throw new InvalidOperationException(string.Join("\n",
                    errors.Select(item => $"{item.Code} {item.ComponentId}: {item.Message}")));

            var projectRoot = Directory.GetParent(UnityEngine.Application.dataPath)!.FullName;
            var outputRoot = Path.Combine(projectRoot, "Documentation", "Generated");
            Directory.CreateDirectory(outputRoot);
            File.WriteAllText(Path.Combine(outputRoot,
                    "evidence-responsibility-code-map.json"),
                JsonConvert.SerializeObject(snapshot, Formatting.Indented) +
                Environment.NewLine, new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(outputRoot,
                    "evidence-responsibility-code-map.md"),
                BuildMarkdown(snapshot), new UTF8Encoding(false));
            Debug.Log($"E 책임 코드 지도 갱신 완료: 후보 {snapshot.Coverage.CandidateTypeCount}, " +
                      $"책임 {snapshot.Coverage.AnnotatedTypeCount}, " +
                      $"제외 {snapshot.Coverage.ExcludedTypeCount}, " +
                      $"미분류 {snapshot.Coverage.UncoveredTypeCount}");
        }

        public static UnityEvidenceResponsibilityMapSnapshot BuildSnapshot(
            bool requireCoverage)
        {
            var assemblies = ResolveProductAssemblies();
            var responsibilities = SsalddelEvidenceResponsibilityReader.Read(assemblies);
            var exclusions = SsalddelEvidenceResponsibilityReader
                .ReadExclusions(assemblies);
            var diagnostics = SsalddelEvidenceCoverageValidator.Validate(
                requireCoverage, assemblies);
            var candidates = assemblies.SelectMany(GetLoadableTypes)
                .Where(SsalddelEvidenceCoveragePolicy.IsCandidate)
                .OrderBy(ComponentId, StringComparer.Ordinal).ToArray();
            var sources = BuildSourceIndex(candidates);
            var components = new List<ComponentEntry>();
            foreach (var candidate in candidates)
            {
                var typeResponsibilities = responsibilities.Where(item =>
                    item.ComponentType == candidate && item.ComponentMethod is null)
                    .ToArray();
                var primary = typeResponsibilities.SingleOrDefault(item =>
                    item.Role == SsalddelEvidenceResponsibilityRole.Primary);
                var exclusion = exclusions.SingleOrDefault(item =>
                    item.ComponentType == candidate) ?? exclusions.SingleOrDefault(item =>
                    item.ComponentType is null && item.Assembly == candidate.Assembly);
                components.Add(new ComponentEntry
                {
                    ComponentId = ComponentId(candidate),
                    AssemblyName = candidate.Assembly.GetName().Name ?? string.Empty,
                    ComponentName = candidate.FullName ?? candidate.Name,
                    SourcePath = sources.TryGetValue(candidate, out var source)
                        ? source ?? string.Empty : string.Empty,
                    CoverageState = exclusion is not null ? "Excluded"
                        : primary is not null ? "Annotated" : "Uncovered",
                    PrimaryEvidenceStage = primary?.EvidenceStage.ToString()
                        ?? string.Empty,
                    Responsibility = primary?.Responsibility ?? string.Empty,
                    Boundary = primary?.Boundary ?? string.Empty,
                    SecondaryEvidenceStages = typeResponsibilities.Where(item =>
                            item.Role == SsalddelEvidenceResponsibilityRole.Secondary)
                        .Select(item => item.EvidenceStage.ToString()).ToArray(),
                    WorldInteractionIds = typeResponsibilities
                        .SelectMany(item => item.WorldInteractionIds)
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                    ExclusionCategory = exclusion?.Category.ToString()
                        ?? string.Empty,
                    ExclusionReason = exclusion?.Reason ?? string.Empty,
                });
            }

            return new UnityEvidenceResponsibilityMapSnapshot
            {
                Stages = SsalddelEvidenceStageDefinitionCatalog.All.Select(definition =>
                    new StageEntry
                    {
                        EvidenceStage = definition.EvidenceStage.ToString(),
                        ManagementSystem = definition.ManagementSystem,
                        KoreanName = definition.KoreanName,
                        TechnicalName = definition.TechnicalName,
                        PrimaryCount = components.Count(item =>
                            item.PrimaryEvidenceStage == definition.EvidenceStage.ToString()),
                        SecondaryCount = components.Count(item =>
                            item.SecondaryEvidenceStages.Contains(
                                definition.EvidenceStage.ToString(),
                                StringComparer.Ordinal)),
                    }).ToArray(),
                Components = components.ToArray(),
                Coverage = new CoverageEntry
                {
                    CandidateTypeCount = components.Count,
                    AnnotatedTypeCount = components.Count(item =>
                        item.CoverageState == "Annotated"),
                    ExcludedTypeCount = components.Count(item =>
                        item.CoverageState == "Excluded"),
                    UncoveredTypeCount = components.Count(item =>
                        item.CoverageState == "Uncovered"),
                    AnnotatedMethodCount = responsibilities.Count(item =>
                        item.ComponentMethod is not null),
                },
                Diagnostics = diagnostics.Select(item => new DiagnosticEntry
                {
                    Severity = item.Severity.ToString(),
                    Code = item.Code,
                    ComponentId = item.ComponentId,
                    Message = item.Message,
                }).ToArray(),
            };
        }

        private static Assembly[] ResolveProductAssemblies()
        {
            var loaded = AppDomain.CurrentDomain.GetAssemblies().ToDictionary(
                assembly => assembly.GetName().Name ?? string.Empty,
                StringComparer.Ordinal);
            var targetNames = ProductAssemblyNames.Concat(ExcludedAssemblyNames)
                .ToArray();
            var missing = targetNames.Where(name => !loaded.ContainsKey(name))
                .ToArray();
            if (missing.Length > 0)
                throw new InvalidOperationException(
                    "E 책임 검사 대상 Unity 어셈블리를 찾을 수 없습니다: " +
                    string.Join(", ", missing));
            return targetNames.Select(name => loaded[name]).ToArray();
        }

        private static Dictionary<Type, string?> BuildSourceIndex(
            IReadOnlyList<Type> types)
        {
            var roots = new[]
            {
                Path.Combine(UnityEngine.Application.dataPath, "Ssalddel"),
                Path.Combine(UnityEngine.Application.dataPath, "Samples"),
            };
            var files = roots.Where(Directory.Exists).SelectMany(root =>
                    Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                .ToArray();
            var typesByName = types.GroupBy(type => type.Name.Split('`')[0],
                    StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToArray(),
                    StringComparer.Ordinal);
            var matches = types.ToDictionary(type => type, _ => new List<string>());
            var declaration = new Regex(
                @"\b(?:class|interface|struct|record)\s+(?<name>[\p{L}\p{N}_]+)\b",
                RegexOptions.CultureInvariant);
            foreach (var file in files)
            {
                var content = File.ReadAllText(file, Encoding.UTF8);
                foreach (Match match in declaration.Matches(content))
                {
                    if (!typesByName.TryGetValue(match.Groups["name"].Value,
                            out var namedTypes)) continue;
                    foreach (var type in namedTypes) matches[type].Add(file);
                }
            }
            return matches.ToDictionary(pair => pair.Key, pair =>
            {
                var paths = pair.Value.Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                return paths.Length == 1
                    ? "Assets/" + Path.GetRelativePath(UnityEngine.Application.dataPath, paths[0])
                        .Replace('\\', '/')
                    : null;
            });
        }

        private static IReadOnlyList<Type> GetLoadableTypes(Assembly assembly)
        {
            try { return assembly.GetTypes(); }
            catch (ReflectionTypeLoadException exception)
            { return exception.Types.OfType<Type>().ToArray(); }
        }

        private static string ComponentId(Type type)
            => (type.Assembly.GetName().Name ?? string.Empty) + ":" +
               (type.FullName ?? type.Name);

        private static string BuildMarkdown(
            UnityEvidenceResponsibilityMapSnapshot snapshot)
        {
            var builder = new StringBuilder();
            builder.AppendLine("# Unity E 책임 코드 지도");
            builder.AppendLine();
            builder.AppendLine("> C# E 책임 Attribute에서 자동 생성된다. 직접 수정하지 않는다.");
            builder.AppendLine();
            builder.AppendLine($"- 후보 타입: `{snapshot.Coverage.CandidateTypeCount}`");
            builder.AppendLine($"- 책임 지정: `{snapshot.Coverage.AnnotatedTypeCount}`");
            builder.AppendLine($"- 사유 있는 제외: `{snapshot.Coverage.ExcludedTypeCount}`");
            builder.AppendLine($"- 미분류: `{snapshot.Coverage.UncoveredTypeCount}`");
            builder.AppendLine();
            builder.AppendLine("| E | G | 모듈 | 대표 | 보조 |");
            builder.AppendLine("| --- | --- | --- | ---: | ---: |");
            foreach (var stage in snapshot.Stages)
                builder.AppendLine($"| `{stage.EvidenceStage}` | `{stage.ManagementSystem}` | `{stage.TechnicalName}` {stage.KoreanName} | {stage.PrimaryCount} | {stage.SecondaryCount} |");
            builder.AppendLine();
            builder.AppendLine("## 구성 요소");
            builder.AppendLine();
            builder.AppendLine("| 구성 요소 | 대표 E | 상태 | 소스 |");
            builder.AppendLine("| --- | --- | --- | --- |");
            foreach (var item in snapshot.Components)
                builder.AppendLine($"| `{item.ComponentName}` | `{item.PrimaryEvidenceStage}` | `{item.CoverageState}` | `{item.SourcePath}` |");
            return builder.ToString();
        }
    }
}
