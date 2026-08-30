using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ssalddel.LocalNpcDialogue
{
    // 연결부는 승인된 로컬 파일만 제공한다. 이 계약은 도구·저장·네트워크 권한을 제공하지 않는다.
    public interface I로컬대화Model
    {
        bool IsLocal { get; }
        bool IsModelAvailable { get; }
        Task<string> GenerateAsync(string prompt, CancellationToken token);
    }

    public enum 대화상태 { Generated, Disabled, ModelMissing, RemoteRejected, NoEvidence, InvalidInput, Busy, Cancelled, TimedOut, Stale, Error }
    public enum 근거공개범위 { Hidden, Public, PlayerPrivate }

    public sealed class 대화근거
    {
        public string EvidenceId { get; }
        public long Revision { get; }
        public string Source { get; }
        public string SessionId { get; }
        public string PlayerId { get; }
        public long WorldRevision { get; }
        public 근거공개범위 Visibility { get; }
        public IReadOnlyList<string> KnownToNpcIds { get; }
        public string Text { get; }
        public 대화근거(string evidenceId, long revision, string source, string sessionId, string playerId,
            long worldRevision, 근거공개범위 visibility, IEnumerable<string> knownToNpcIds, string text)
        {
            EvidenceId = evidenceId; Revision = revision; Source = source; SessionId = sessionId;
            PlayerId = playerId; WorldRevision = worldRevision; Visibility = visibility; Text = text;
            KnownToNpcIds = Array.AsReadOnly((knownToNpcIds ?? Array.Empty<string>()).Take(65).ToArray());
        }
    }

    // 신뢰된 게임 읽기 사본에서만 생성한다. 외부 사용자 입력을 권위 사실로 승인하는 API가 아니다.
    public sealed class 대화Context
    {
        public string NpcId { get; }
        public string PlayerId { get; }
        public string SessionId { get; }
        public long WorldRevision { get; }
        public long EvidenceRevision { get; }
        public IReadOnlyList<대화근거> Evidence { get; }
        public 대화Context(string npcId, string playerId, string sessionId, long worldRevision,
            long evidenceRevision, IEnumerable<대화근거> evidence)
        {
            NpcId = npcId; PlayerId = playerId; SessionId = sessionId;
            WorldRevision = worldRevision; EvidenceRevision = evidenceRevision;
            // 초과 표본 한 개를 남겨 상한 초과를 명시적으로 거부한다. 무제한 열거를 하지 않는다.
            Evidence = Array.AsReadOnly((evidence ?? Array.Empty<대화근거>()).Take(129).ToArray());
        }
    }

    public sealed class 대화Request
    {
        public 대화Context Context { get; }
        public string Query { get; }
        public string Fallback { get; }
        public 대화Request(대화Context context, string query, string fallback = "잠시 뒤 다시 이야기해요.")
        { Context = context; Query = query; Fallback = fallback; }
    }

    public sealed class 대화Result
    {
        public 대화상태 Status { get; }
        public string Text { get; }
        public 대화Context Context { get; }
        public IReadOnlyList<대화근거> Evidence { get; }
        public string RetrievalMethod => "Keyword";
        public bool IsAuthoritative => false;
        internal 대화Result(대화상태 status, string text, 대화Context context, IEnumerable<대화근거> evidence = null)
        { Status = status; Text = text; Context = context; Evidence = Array.AsReadOnly((evidence ?? Array.Empty<대화근거>()).ToArray()); }
    }

    public sealed class 로컬대화Options
    {
        public bool Enabled { get; }
        public int MaxConcurrency { get; }
        public int MaxOutputCharacters { get; }
        public int MaxPromptCharacters { get; }
        public TimeSpan Timeout { get; }
        public 로컬대화Options(bool enabled = false, int maxConcurrency = 1, int maxOutputCharacters = 256,
            int maxPromptCharacters = 8192, TimeSpan? timeout = null)
        {
            var duration = timeout ?? TimeSpan.FromSeconds(15);
            if (maxConcurrency < 1 || maxConcurrency > 4 || maxOutputCharacters < 1 || maxOutputCharacters > 2048 ||
                maxPromptCharacters < 512 || maxPromptCharacters > 16384 || duration <= TimeSpan.Zero || duration > TimeSpan.FromMinutes(2))
                throw new ArgumentOutOfRangeException(nameof(maxConcurrency), "로컬 대화 상한을 확인하세요.");
            Enabled = enabled; MaxConcurrency = maxConcurrency; MaxOutputCharacters = maxOutputCharacters;
            MaxPromptCharacters = maxPromptCharacters; Timeout = duration;
        }
    }
}
