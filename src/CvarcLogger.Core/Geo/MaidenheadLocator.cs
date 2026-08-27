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

    /// <summary>Converts a latitude/longitude to a Maidenhead grid square, the opposite direction of
    /// ToLatLon above -- standard field/square/subsquare encoding (20 deg x 10 deg field, 2 deg x 1 deg
    /// square, 5' x 2.5' subsquare). Used to derive a station's own grid square from GPS.</summary>
    public static string FromLatLon(double latitude, double longitude, int precision = 6)
    {
        double lon = Math.Clamp(longitude, -180, 180) + 180.0;
        double lat = Math.Clamp(latitude, -90, 90) + 90.0;
        if (lon >= 360) lon = 359.999999;
        if (lat >= 180) lat = 179.999999;

        int lonFieldIndex = (int)(lon / 20.0);
        int latFieldIndex = (int)(lat / 10.0);
        char lonField = (char)('A' + lonFieldIndex);
        char latField = (char)('A' + latFieldIndex);

        double lonRemainder = lon - lonFieldIndex * 20.0;
        double latRemainder = lat - latFieldIndex * 10.0;

        int lonSquare = (int)(lonRemainder / 2.0);
        int latSquare = (int)latRemainder;

        var grid = $"{lonField}{latField}{lonSquare}{latSquare}";
        if (precision < 6) return grid;

        double lonSquareRemainder = lonRemainder - lonSquare * 2.0;
        double latSquareRemainder = latRemainder - latSquare;

        char lonSubsquare = (char)('a' + (int)(lonSquareRemainder / (2.0 / 24.0)));
        char latSubsquare = (char)('a' + (int)(latSquareRemainder / (1.0 / 24.0)));

        return grid + lonSubsquare + latSubsquare;
    }
}
