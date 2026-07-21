using CvarcLogger.Core.Lookup;

namespace CvarcCellLog.Services;

public enum LookupServicePreference
{
    Callook,
    Qrz,
    QrzCq,
}

/// <summary>Ported from the WPF app's LookupCoordinator (CvarcLogger.App/Services) for Milestone 2: same
/// try-preferred-then-fill-gaps logic, just reading the preferred service from Preferences directly
/// instead of a full ported SettingsService for one value.</summary>
public class LookupCoordinator
{
    private const string PreferredServiceKey = "PreferredLookupService";

    private readonly CallookLookupService _callook;
    private readonly QrzLookupService _qrz;
    private readonly QrzCqLookupService _qrzCq;

    public LookupCoordinator(CallookLookupService callook, QrzLookupService qrz, QrzCqLookupService qrzCq)
    {
        _callook = callook;
        _qrz = qrz;
        _qrzCq = qrzCq;
    }

    public static LookupServicePreference PreferredService
    {
        get => Enum.TryParse<LookupServicePreference>(Preferences.Default.Get(PreferredServiceKey, nameof(LookupServicePreference.Callook)), out var value)
            ? value
            : LookupServicePreference.Callook;
        set => Preferences.Default.Set(PreferredServiceKey, value.ToString());
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
        ICallsignLookupService preferred = PreferredService switch
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
