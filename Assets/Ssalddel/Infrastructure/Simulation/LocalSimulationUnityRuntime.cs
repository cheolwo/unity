using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;
using UnityEngine;

namespace Ssalddel.Unity.Infrastructure.Simulation
{
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E2,
        "Unity가 공통 Core 또는 원격 Host와 연결되는 실행 경계를 제공한다.",
        Boundary = "Unity GameObject와 표현은 권위 상태를 직접 소유하지 않는다.")]
    public static class LocalSimulationUnityRuntime
    {
        public static LocalSimulationRuntime Create()
        {
            var overrideRoot = Environment.GetEnvironmentVariable(
                "SSALDDEL_LOCAL_SAVE_ROOT");
            var savesRoot = string.IsNullOrWhiteSpace(overrideRoot)
                ? Path.Combine(UnityEngine.Application.persistentDataPath, "Saves")
                : Path.GetFullPath(overrideRoot!);
            return Create(savesRoot);
        }

        public static LocalSimulationRuntime Create(string savesRootPath)
        {
            return new LocalSimulationRuntime(
                new UnityInMemorySimulationSessionStore(),
                new UnityInMemorySimulationSessionSaveStore(),
                new UnitySimulationLocalSaveSlotStore(savesRootPath));
        }
    }

    internal sealed class UnityInMemorySimulationSessionStore
        : I경영SimulationSessionStore
    {
        private readonly ConcurrentDictionary<string, 경영SimulationSessionAggregate> sessions
            = new(StringComparer.Ordinal);

        public 경영SimulationSessionAggregate CreateOrGet(
            경영SimulationSession생성Request request,
            SimulationRealityContextSnapshot? frozenRealityContext = null)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var candidate = new 경영SimulationSessionAggregate(request,
                frozenRealityContext);
            var session = sessions.GetOrAdd(candidate.SessionStableId, candidate);
            session.EnsureSameCreationRequest(request, frozenRealityContext);
            return session;
        }

        public 경영SimulationSessionAggregate? Find(string sessionStableId)
            => !string.IsNullOrWhiteSpace(sessionStableId)
               && sessions.TryGetValue(sessionStableId, out var session)
                ? session : null;

        public 경영SimulationSessionAggregate Restore(
            경영SimulationSessionAggregate session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (!sessions.TryAdd(session.SessionStableId, session))
                throw new SimulationConflictException("SimulationSessionAlreadyActive");
            return session;
        }

        public 경영SimulationSessionAggregate ReplaceForCampaignRetry(
            경영SimulationSessionAggregate session, long expectedCurrentRevision)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            while (true)
            {
                if (!sessions.TryGetValue(session.SessionStableId, out var current))
                    throw new SimulationNotFoundException("SimulationSessionNotFound");
                if (current.Revision != expectedCurrentRevision)
                    throw new SimulationConflictException("SimulationExpectedRevisionMismatch");
                if (sessions.TryUpdate(session.SessionStableId, session, current))
                    return session;
            }
        }
    }

    internal sealed class UnityInMemorySimulationSessionSaveStore
        : ISimulationSessionSaveStore
    {
        private readonly ConcurrentDictionary<string, SimulationSessionSavePackage> saves
            = new(StringComparer.Ordinal);

        public SimulationSessionSavePackage SaveOrGet(
            SimulationSessionSavePackage package)
        {
            if (package == null) throw new ArgumentNullException(nameof(package));
            var candidate = SimulationSaveReplayCloner.ClonePackage(package);
            var saved = saves.GetOrAdd(candidate.SaveStableId, candidate);
            if (!string.Equals(saved.SessionStableId, candidate.SessionStableId,
                    StringComparison.Ordinal)
                || !string.Equals(saved.ReplayHash, candidate.ReplayHash,
                    StringComparison.Ordinal))
                throw new SimulationConflictException("SimulationSaveStableIdConflict");
            return SimulationSaveReplayCloner.ClonePackage(saved);
        }

        public SimulationSessionSavePackage? Find(string saveStableId)
            => !string.IsNullOrWhiteSpace(saveStableId)
               && saves.TryGetValue(saveStableId, out var package)
                ? SimulationSaveReplayCloner.ClonePackage(package) : null;
    }

    internal sealed class UnitySimulationLocalSaveSlotStore
        : ISimulationLocalSaveSlotStore
    {
        private const string Format = "ssalddel-local-save.v1";
        private readonly string rootPath;
        private readonly JsonSerializerOptions jsonOptions = new()
        {
            PropertyNameCaseInsensitive = false,
            WriteIndented = false,
        };

        public UnitySimulationLocalSaveSlotStore(string savesRootPath)
        {
            rootPath = Path.GetFullPath(savesRootPath);
            Directory.CreateDirectory(rootPath);
        }

        public void Write(string slotStableId, SimulationSessionSavePackage package)
        {
            var slot = Normalize(slotStableId);
            SimulationSessionReplay.Restore(package);
            var packageJson = JsonSerializer.Serialize(package, jsonOptions);
            var envelope = new Envelope
            {
                FileFormatVersion = Format,
                SlotStableId = slot,
                WrittenAtUtc = DateTimeOffset.UtcNow,
                PackageSha256 = Hash(packageJson),
                Package = package,
            };
            var path = Resolve(slot);
            var temporary = path + ".tmp";
            var backup = path + ".bak";
            try
            {
                File.WriteAllText(temporary,
                    JsonSerializer.Serialize(envelope, jsonOptions),
                    new UTF8Encoding(false));
                ReadEnvelope(temporary, slot);
                if (File.Exists(path)) File.Replace(temporary, path, backup, true);
                else File.Move(temporary, path);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        public SimulationLocalSaveSlotPackage Read(string slotStableId)
        {
            var slot = Normalize(slotStableId);
            var path = Resolve(slot);
            try
            {
                return Result(ReadEnvelope(path, slot), false);
            }
            catch (Exception primary) when (Recoverable(primary)
                                             && File.Exists(path + ".bak"))
            {
                try { return Result(ReadEnvelope(path + ".bak", slot), true); }
                catch (Exception backup) when (Recoverable(backup))
                { throw new SimulationContractException("SimulationLocalSaveCorrupted"); }
            }
            catch (FileNotFoundException)
            { throw new SimulationNotFoundException("SimulationLocalSaveSlotNotFound"); }
            catch (Exception error) when (Recoverable(error))
            { throw new SimulationContractException("SimulationLocalSaveCorrupted"); }
        }

        private Envelope ReadEnvelope(string path, string slot)
        {
            var storedJson = File.ReadAllText(path, Encoding.UTF8);
            using var document = JsonDocument.Parse(storedJson);
            var envelope = JsonSerializer.Deserialize<Envelope>(
                storedJson, jsonOptions)
                ?? throw new SimulationContractException("SimulationLocalSaveCorrupted");
            if (envelope.FileFormatVersion != Format || envelope.SlotStableId != slot
                || envelope.Package == null || !document.RootElement.TryGetProperty("Package", out var storedPackage))
                throw new SimulationContractException("SimulationLocalSaveCorrupted");
            // 추가된 선택 필드의 기본값으로 과거 파일을 다시 직렬화하지 않는다.
            // 저장 당시 원문을 검증한 뒤 기존 Replay 검증을 그대로 수행한다.
            var json = storedPackage.GetRawText();
            if (envelope.PackageSha256 != Hash(json))
                throw new SimulationContractException(
                    "SimulationLocalSaveChecksumMismatch");
            SimulationSessionReplay.Restore(envelope.Package);
            return envelope;
        }

        private string Resolve(string slot)
        {
            var path = Path.GetFullPath(Path.Combine(rootPath, slot + ".ssalddel"));
            var root = rootPath.TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new SimulationContractException("SimulationLocalSaveSlotInvalid");
            return path;
        }

        private static string Normalize(string slotStableId)
        {
            if (string.IsNullOrWhiteSpace(slotStableId))
                throw new SimulationContractException("SimulationLocalSaveSlotInvalid");
            var slot = slotStableId.Trim();
            if (slot.Length > 64 || slot.Any(value => !char.IsLetterOrDigit(value)
                    && value != '-' && value != '_'))
                throw new SimulationContractException("SimulationLocalSaveSlotInvalid");
            return slot;
        }

        private static string Hash(string value)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value)))
                .Replace("-", string.Empty).ToLowerInvariant();
        }

        private static bool Recoverable(Exception error)
            => error is IOException || error is UnauthorizedAccessException
                || error is JsonException || error is SimulationContractException;

        private static SimulationLocalSaveSlotPackage Result(Envelope envelope,
            bool backup) => new()
        {
            SlotStableId = envelope.SlotStableId,
            RecoveredFromBackup = backup,
            Package = envelope.Package,
        };

        private sealed class Envelope
        {
            public string FileFormatVersion { get; set; } = string.Empty;
            public string SlotStableId { get; set; } = string.Empty;
            public DateTimeOffset WrittenAtUtc { get; set; }
            public string PackageSha256 { get; set; } = string.Empty;
            public SimulationSessionSavePackage Package { get; set; } = new();
        }
    }
}
