namespace CvarcLogger.Core.Lookup;

/// <summary>Looks up name/grid/country details for a callsign from an online service. Lookups are
/// best-effort — implementations should never throw for network failures, only return a result
/// with <see cref="CallsignLookupResult.Found"/> false and an <see cref="CallsignLookupResult.Error"/>.</summary>
public interface ICallsignLookupService
{
    string ServiceName { get; }
    Task<CallsignLookupResult> LookupAsync(string callsign, CancellationToken ct = default);
}
