using CvarcLogger.Core.Lookup;

namespace CvarcLogger.App.Services;

/// <summary>Tries the user's preferred callsign lookup service first, then the other two in a fixed
/// order, so the UI has one lookup call to make regardless of Settings. Also fills gaps: if the first
/// hit is missing a field no single service reliably provides (County, notably — callook.info has none
/// at all, and QRZCQ doesn't expose it either), it keeps trying the remaining services and merges in
/// whatever they find, stopping as soon as County is filled or every service has been tried.</summary>
public class LookupCoordinator
{
    private readonly CallookLookupService _callook;
    private readonly QrzLookupService _qrz;
    private readonly QrzCqLookupService _qrzCq;
    private readonly SettingsService _settings;

    public LookupCoordinator(CallookLookupService callook, QrzLookupService qrz, QrzCqLookupService qrzCq, SettingsService settings)
    {
        _callook = callook;
        _qrz = qrz;
        _qrzCq = qrzCq;
        _settings = settings;
    }

    public async Task<CallsignLookupResult> LookupAsync(string callsign, CancellationToken ct = default)
    {
        CallsignLookupResult? merged = null;

        foreach (var service in GetServicesInPreferenceOrder())
        {
            var result = await service.LookupAsync(callsign, ct).ConfigureAwait(false);
            if (!result.Found) continue;

            merged = merged is null ? result : MergeMissingFields(merged, result);
            if (merged.County is not null) break;
        }

        return merged ?? CallsignLookupResult.NotFound("Callsign not found in any configured lookup service.");
    }

    private IReadOnlyList<ICallsignLookupService> GetServicesInPreferenceOrder()
    {
        var services = new List<ICallsignLookupService> { _callook, _qrz, _qrzCq };
        ICallsignLookupService preferred = _settings.PreferredLookupService switch
        {
            LookupServicePreference.Qrz => _qrz,
            LookupServicePreference.QrzCq => _qrzCq,
            _ => _callook,
        };
        services.Remove(preferred);
        services.Insert(0, preferred);
        return services;
    }

    private static CallsignLookupResult MergeMissingFields(CallsignLookupResult primary, CallsignLookupResult secondary) =>
        primary with
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
