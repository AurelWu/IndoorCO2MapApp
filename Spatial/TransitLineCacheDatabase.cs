using IndoorCO2MapAppV2.Utility;
using SQLite;
using System.Globalization;

namespace IndoorCO2MapAppV2.Spatial
{
    public class TransitLineCacheDatabase
    {
        private readonly SQLiteAsyncConnection _db;

        public TransitLineCacheDatabase(string path)
        {
            _db = new SQLiteAsyncConnection(path);
            _ = _db.CreateTableAsync<CachedTransitLine>();
        }

        public Task InsertOrReplaceAsync(TransitLineData line, double searchLat, double searchLon)
        {
            double lat = Math.Round(searchLat, 3);
            double lon = Math.Round(searchLon, 3);
            string latStr = lat.ToString(CultureInfo.InvariantCulture);
            string lonStr = lon.ToString(CultureInfo.InvariantCulture);
            return _db.InsertOrReplaceAsync(new CachedTransitLine
            {
                Key = $"{line.NWRType}_{line.ID}_{latStr}_{lonStr}",
                VehicleType = line.VehicleType,
                ID = line.ID,
                NWRType = line.NWRType,
                Name = line.Name,
                Latitude = lat,
                Longitude = lon,
            });
        }

        public async Task<List<TransitLineData>> GetAllAsync(double userLat, double userLon, double rangeMeters)
        {
            var list = await _db.Table<CachedTransitLine>().ToListAsync().ConfigureAwait(false);
            return list
                .Where(c => Haversine.GetDistanceInMeters(userLat, userLon, c.Latitude, c.Longitude) <= rangeMeters)
                .DistinctBy(c => c.ID)
                .Select(c => new TransitLineData(c.VehicleType, c.NWRType, c.ID, c.Name, c.Latitude, c.Longitude))
                .ToList();
        }

        public Task ClearAsync() => _db.DeleteAllAsync<CachedTransitLine>();
    }
}
