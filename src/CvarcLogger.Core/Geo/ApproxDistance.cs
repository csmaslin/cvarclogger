namespace CvarcLogger.Core.Geo;

/// <summary>Fast flat-earth distance approximation for ranking "nearest" among a large candidate list
/// (tens or hundreds of thousands of rows -- e.g. the full SOTA summit list or POTA park list) where
/// computing true Haversine great-circle distance for every row would be too expensive on a phone. Not
/// a real distance -- only valid for comparing/ranking candidates against the same origin point, but
/// accurate enough over any realistic activation search radius to pick the actual nearest one.</summary>
public static class ApproxDistance
{
    public static double LongitudeScaleFor(double originLatitude) => Math.Cos(originLatitude * Math.PI / 180.0);

    public static double SquaredDegrees(double originLat, double originLon, double candidateLat, double candidateLon, double longitudeScale)
    {
        double dLat = candidateLat - originLat;
        double dLon = (candidateLon - originLon) * longitudeScale;
        return dLat * dLat + dLon * dLon;
    }
}
