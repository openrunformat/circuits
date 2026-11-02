namespace OrfCircuits;

public static class Geo
{
    /// <summary>Mean Earth radius (IUGG), in metres.</summary>
    public const double EarthRadiusM = 6_371_008.8;

    /// <summary>Great-circle distance in metres (haversine).</summary>
    public static double DistanceM(double lat1, double lon1, double lat2, double lon2)
    {
        static double Rad(double deg) => deg * Math.PI / 180.0;
        var dLat = Rad(lat2 - lat1);
        var dLon = Rad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * EarthRadiusM * Math.Asin(Math.Min(1.0, Math.Sqrt(a)));
    }
}
