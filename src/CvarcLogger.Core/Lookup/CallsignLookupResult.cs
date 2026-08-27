namespace CvarcLogger.Core.Lookup;

public record CallsignLookupResult(
    bool Found,
    string? Name = null,
    string? GridSquare = null,
    string? Country = null,
    int? DxccEntityCode = null,
    string? State = null,
    string? County = null,
    string? City = null,
    double? Latitude = null,
    double? Longitude = null,
    string? Error = null)
{
    public static CallsignLookupResult NotFound(string? error = null) => new(false, Error: error);
}
