namespace CvarcLogger.Core.Geo;

/// <summary>Converts a Maidenhead grid square (4 or 6 characters, e.g. "DM04" or "DM04mm") to its
/// center-point latitude/longitude. Ported from grid.radio's maidenhead.js (maidenheadToLatLon) —
/// the standard Maidenhead algorithm, field-by-field.</summary>
public static class MaidenheadLocator
{
    public static (double Lat, double Lon)? ToLatLon(string? grid)
    {
        if (string.IsNullOrWhiteSpace(grid)) return null;
        grid = grid.Trim();

        if (grid.Length == 4 && IsFieldSquare(grid))
        {
            int a = char.ToUpperInvariant(grid[0]) - 'A';
            int b = char.ToUpperInvariant(grid[1]) - 'A';
            int c = grid[2] - '0';
            int d = grid[3] - '0';

            double lon = a * 20 - 180 + c * 2 + 1;
            double lat = b * 10 - 90 + d + 0.5;
            return (lat, lon);
        }

        if (grid.Length == 6 && IsFieldSquare(grid) && char.IsLetter(grid[4]) && char.IsLetter(grid[5]))
        {
            int a = char.ToUpperInvariant(grid[0]) - 'A';
            int b = char.ToUpperInvariant(grid[1]) - 'A';
            int c = grid[2] - '0';
            int d = grid[3] - '0';
            int e = char.ToLowerInvariant(grid[4]) - 'a';
            int f = char.ToLowerInvariant(grid[5]) - 'a';

            double lon = a * 20 - 180 + c * 2 + e / 12.0 + 1 / 24.0;
            double lat = b * 10 - 90 + d + f / 24.0 + 1 / 48.0;
            return (lat, lon);
        }

        return null;
    }

    private static bool IsFieldSquare(string grid) =>
        char.IsLetter(grid[0]) && char.IsLetter(grid[1]) && char.IsDigit(grid[2]) && char.IsDigit(grid[3]);
}
