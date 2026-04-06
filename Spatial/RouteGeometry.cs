namespace IndoorCO2MapAppV2.Spatial
{
    public record RouteGeometry(List<(double Lon, double Lat)> Points, string? Color);
}
