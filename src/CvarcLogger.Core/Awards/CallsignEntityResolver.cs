using CvarcLogger.Core.Abstractions;
using CvarcLogger.Core.Models;

namespace CvarcLogger.Core.Awards;

/// <summary>Longest-prefix-match resolver over the bundled DXCC prefix table. For a portable/mobile
/// callsign (e.g. "W1AW/KH6"), each "/"-separated segment that isn't a known operating-mode suffix
/// (P, M, MM, AM, QRP, A, B, R) is matched independently, and whichever segment yields the longest
/// (most specific) matching prefix wins — e.g. "KH6" (3-char match) beats "W1AW" (1-char "W" match).</summary>
public class CallsignEntityResolver : ICallsignEntityResolver
{
    private static readonly HashSet<string> IgnoredSuffixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "P", "M", "MM", "AM", "QRP", "A", "B", "R"
    };

    private readonly IDxccEntityRepository _repository;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private Dictionary<string, DxccEntity>? _prefixIndex;
    private int _maxPrefixLength;

    public CallsignEntityResolver(IDxccEntityRepository repository)
    {
        _repository = repository;
    }

    public async Task<DxccEntity?> ResolveAsync(string callsign, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(callsign)) return null;
        await EnsureIndexLoadedAsync(ct).ConfigureAwait(false);

        string normalized = callsign.Trim().ToUpperInvariant();
        var candidates = normalized.Contains('/')
            ? normalized.Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Where(s => !IgnoredSuffixes.Contains(s))
                .ToList()
            : new List<string> { normalized };

        if (candidates.Count == 0)
            candidates.Add(normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? normalized);

        DxccEntity? best = null;
        int bestMatchedLength = 0;
        foreach (var candidate in candidates)
        {
            var (entity, matchedLength) = MatchLongestPrefix(candidate);
            if (entity is not null && matchedLength > bestMatchedLength)
            {
                best = entity;
                bestMatchedLength = matchedLength;
            }
        }

        return best;
    }

    private async Task EnsureIndexLoadedAsync(CancellationToken ct)
    {
        if (_prefixIndex is not null) return;
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_prefixIndex is not null) return;

            var entities = await _repository.GetAllWithPrefixesAsync(ct).ConfigureAwait(false);
            var index = new Dictionary<string, DxccEntity>(StringComparer.OrdinalIgnoreCase);
            int max = 0;
            foreach (var entity in entities)
            {
                foreach (var prefix in entity.Prefixes)
                {
                    if (string.IsNullOrWhiteSpace(prefix.Prefix)) continue;
                    index[prefix.Prefix] = entity;
                    max = Math.Max(max, prefix.Prefix.Length);
                }
            }

            _prefixIndex = index;
            _maxPrefixLength = max;
        }
        finally
        {
            _lock.Release();
        }
    }

    private (DxccEntity? Entity, int MatchedLength) MatchLongestPrefix(string segment)
    {
        if (_prefixIndex is null || segment.Length == 0) return (null, 0);

        int upper = Math.Min(_maxPrefixLength, segment.Length);
        for (int length = upper; length >= 1; length--)
        {
            string candidate = segment[..length];
            if (_prefixIndex.TryGetValue(candidate, out var entity))
                return (entity, length);
        }

        return (null, 0);
    }
}
