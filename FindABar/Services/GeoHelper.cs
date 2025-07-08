namespace FindABar.Services;

public class GeoHelper
{
    public static double GetDistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        var R = 6371; // rayon de la terre en km
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);

        var a = Math.Sin(dLat/2) * Math.Sin(dLat/2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon/2) * Math.Sin(dLon/2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1-a));

        return R * c;
    }

    public static double ToRadians(double deg)
    {
        return deg * (Math.PI/180);
    }
    public static double ToDegrees(double radians) => radians * 180 / Math.PI;
}