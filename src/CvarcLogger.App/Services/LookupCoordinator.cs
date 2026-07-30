using CvarcLogger.Core.Abstractions;
using CvarcLogger.Core.Lookup;

namespace CvarcLogger.App.Services;

/// <summary>Callsign lookup chain, in a fixed order: QRZ.com, then QRZCQ.com, then Callook.info last.
/// QRZ goes first because it's the only one of the three that can ever supply County (confirmed in
/// QrzCqLookupService's own doc comment, and Callook is US-only FCC data with no county field at all) --
/// checking it first means the common case (a callsign with a complete QRZ profile) finishes in a single
/// call. Each paid service (QRZ/QRZCQ) is skipped outright -- no network call at all -- unless it actually
/// has saved credentials, so an unconfigured service never wastes a round-trip on a login that would just
/// fail. Every service, including Callook, is also skipped once the merged result already has every field
/// that service is structurally capable of contributing, since calling it at that point could not add
/// anything. Net effect: Callook.info becomes the automatic fallback whenever neither paid service is
/// configured, and the chain stops as early as traffic allows in every other case.</summary>
public class LookupCoordinator
{
    private readonly CallookLookupService _callook;
    private readonly QrzLookupService _qrz;
    private readonly QrzCqLookupService _qrzCq;
    private readonly ICredentialStore _credentialStore;

    public LookupCoordinator(CallookLookupService callook, QrzLookupService qrz, QrzCqLookupService qrzCq, ICredentialStore credentialStore)
    {
        _callook = callook;
        _qrz = qrz;
        _qrzCq = qrzCq;
        _credentialStore = credentialStore;
    }

    public async Task<CallsignLookupResult> LookupAsync(string callsign, CancellationToken ct = default)
    {
        CallsignLookupResult? merged = null;

        bool qrzConfigured = await _credentialStore.LoadAsync(QrzLookupService.CredentialKey, ct).ConfigureAwait(false) is not null;
        bool qrzCqConfigured = await _credentialStore.LoadAsync(QrzCqLookupService.CredentialKey, ct).ConfigureAwait(false) is not null;

        if (qrzConfigured && !HasAllFieldsIncludingCounty(merged))
        {
            var result = await _qrz.LookupAsync(callsign, ct).ConfigureAwait(false);
            if (result.Found) merged = Merge(merged, result);
        }

        if (qrzCqConfigured && !HasAllCoreFields(merged))
        {
            var result = await _qrzCq.LookupAsync(callsign, ct).ConfigureAwait(false);
            if (result.Found) merged = Merge(merged, result);
        }

        if (!HasAllCoreFields(merged))
        {
            var result = await _callook.LookupAsync(callsign, ct).ConfigureAwait(false);
            if (result.Found) merged = Merge(merged, result);
        }

        return merged ?? CallsignLookupResult.NotFound("Callsign not found in any configured lookup service.");
    }

    // "Core" fields are the ones Callook.info and QRZCQ.com can both supply -- County is deliberately
    // excluded here since neither of them ever returns it, so requiring it would make this check
    // impossible to satisfy and defeat the point of skipping a redundant call.
    private static bool HasAllCoreFields(CallsignLookupResult? r) =>
        r is not null && r.Name is not null && r.GridSquare is not null && r.Country is not null &&
        r.DxccEntityCode is not null && r.State is not null && r.City is not null &&
        r.Latitude is not null && r.Longitude is not null;

    private static bool HasAllFieldsIncludingCounty(CallsignLookupResult? r) => HasAllCoreFields(r) && r!.County is not null;

    private static CallsignLookupResult Merge(CallsignLookupResult? primary, CallsignLookupResult secondary)
    {
        if (primary is null) return secondary;
        return primary with
        {
            Name = primary.Name ?? secondary.Name,
            GridSquare = primary.GridSquare ?? secondary.GridSquare,
            Country = primary.Country ?? secondary.Country,
            DxccEntityCode = primary.DxccEntityCode ?? secondary.DxccEntityCode,
            State = primary.State ?? secondary.State,
            County = primary.County ?? secondary.County,
            City = primary.City ?? secondary.City,
            Latitude = primary.Latitude ?? secondary.Latitude,
            Longitude = primary.Longitude ?? secondary.Longitude,
        };
    }
}
