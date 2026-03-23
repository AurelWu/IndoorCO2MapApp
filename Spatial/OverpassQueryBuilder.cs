using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace IndoorCO2MapAppV2.Spatial
{
    internal static class OverpassQueryBuilder
    {
        // --- Remote whitelist ---

        private record WhitelistEntry(
            [property: JsonPropertyName("conditions")] List<string> Conditions);

        private record BuildingWhitelist(
            [property: JsonPropertyName("version")] int Version,
            [property: JsonPropertyName("entries")] List<WhitelistEntry> Entries);

        private static readonly HttpClient _whitelistHttpClient = new();
        private static List<WhitelistEntry>? _cachedEntries;

        internal static async Task FetchWhitelistAsync()
        {
            try
            {
                var json = await _whitelistHttpClient
                    .GetStringAsync("https://indoorco2map.com/buildingWhitelist.json")
                    .ConfigureAwait(false);
                var whitelist = JsonSerializer.Deserialize<BuildingWhitelist>(json);
                if (whitelist?.Entries is { Count: > 0 } entries)
                    _cachedEntries = entries;
            }
            catch
            {
                // Leave _cachedEntries null → fallback to hardcoded query
            }
        }

        // --- Bbox helper ---

        // Converts a center point + radius (metres) to an Overpass bbox string "south,west,north,east".
        // Using bbox in the query header is more efficient than repeating (around:R,Lat,Lon) on every filter.
        private static string ComputeBbox(double latitude, double longitude, double radius)
        {
            double latDelta = radius / 111320.0;
            double lonDelta = radius / (111320.0 * Math.Cos(latitude * Math.PI / 180.0));
            string s = (latitude  - latDelta).ToString("F6", CultureInfo.InvariantCulture);
            string n = (latitude  + latDelta).ToString("F6", CultureInfo.InvariantCulture);
            string w = (longitude - lonDelta).ToString("F6", CultureInfo.InvariantCulture);
            string e = (longitude + lonDelta).ToString("F6", CultureInfo.InvariantCulture);
            return $"{s},{w},{n},{e}"; // Overpass order: south,west,north,east
        }

        // --- Query builders ---

        internal static string CreateTransportOverpassQuery(double latitude, double longitude, double radius, bool startLocation)
        {
            string bbox = ComputeBbox(latitude, longitude, radius);
            string header = $"[out:json][timeout:30][bbox:{bbox}];";

            if (startLocation)
            {
                return header +
                    "(" +
                    "nwr[railway=tram_stop];" +
                    "relation[route=tram];" +
                    "nwr[highway=bus_stop];" +
                    "relation[route=bus];" +
                    "nwr[railway=subway_station];" +
                    "relation[route=subway];" +
                    "nwr[railway=station];" +
                    "nwr[railway=halt];" +
                    "nwr[railway=stop];" +
                    "relation[route=train];" +
                    "relation[route=light_rail];" +
                    "relation[route=monorail];" +
                    ");" +
                    "out center tags qt;";
            }
            else
            {
                return header +
                    "(" +
                    "nwr[railway=tram_stop];" +
                    "nwr[highway=bus_stop];" +
                    "nwr[railway=subway_station];" +
                    "nwr[railway=station];" +
                    "nwr[railway=stop];" +
                    "nwr[railway=halt];" +
                    ");" +
                    "out center tags qt;";
            }
        }

        internal static string CreateBuildingOverpassQuery(double latitude, double longitude, double radius)
        {
            string bbox = ComputeBbox(latitude, longitude, radius);

            if (_cachedEntries is { Count: > 0 } entries)
                return BuildQueryFromEntries(entries, bbox);

            return BuildHardcodedQuery(bbox);
        }

        private static string BuildQueryFromEntries(List<WhitelistEntry> entries, string bbox)
        {
            var sb = new StringBuilder($"[out:json][timeout:30][bbox:{bbox}];(");
            foreach (var entry in entries)
            {
                var filter = string.Join("][", entry.Conditions);
                sb.Append($"nwr[{filter}];");
            }
            sb.Append(");out center qt;");
            return sb.ToString();
        }

        private static string BuildHardcodedQuery(string bbox)
        {
            return $"[out:json][timeout:30][bbox:{bbox}];" +
                "(" +
                "nwr[office=employment_agency];" +
                "nwr[office=lawyer];" +
                "nwr[office=educational_institution];" +
                "nwr[office=government];" +
                "nwr[office=political_party];" +
                "nwr[office=coworking];" +
                "nwr[government=register_office];" +
                "nwr[shop];" +
                "nwr[craft];" +
                "nwr[aeroway=aerodrome];" +
                "nwr[aeroway=terminal];" +
                "nwr[railway=station];" +
                "nwr[public_transport=station];" +
                "nwr[leisure=fitness_centre];" +
                "nwr[leisure=bowling_alley];" +
                "nwr[leisure=sports_centre];" +
                "nwr[leisure=sports_hall];" +
                "nwr[sport=swimming];" +
                "nwr[leisure=swimming_pool];" +
                "nwr[leisure=sauna];" +
                "nwr[leisure=hackerspace];" +
                "nwr[leisure=escape_game];" +
                "nwr[leisure=dance];" +
                "nwr[amenity=studio];" +
                "nwr[amenity=townhall];" +
                "nwr[amenity=car_rental];" +
                "nwr[amenity=convention_centre];" +
                "nwr[amenity=conference_centre];" +
                "nwr[amenity=congress_centre];" +
                "nwr[amenity=events_centre];" +
                "nwr[amenity=bar];" +
                "nwr[amenity=place_of_worship];" +
                "nwr[amenity=pub];" +
                "nwr[amenity=restaurant];" +
                "nwr[amenity=cafe];" +
                "nwr[amenity=fast_food];" +
                "nwr[amenity=food_court];" +
                "nwr[amenity=ice_cream];" +
                "nwr[amenity=college];" +
                "nwr[amenity=dancing_school];" +
                "nwr[amenity=driving_school];" +
                "nwr[amenity=kindergarten];" +
                "nwr[amenity=language_school];" +
                "nwr[amenity=library];" +
                "nwr[amenity=cinema];" +
                "nwr[amenity=theatre];" +
                "nwr[amenity=concert_hall];" +
                "nwr[amenity=music_venue];" +
                "nwr[amenity=arts_centre];" +
                "nwr[amenity=brothel];" +
                "nwr[amenity=love_hotel];" +
                "nwr[amenity=nightclub];" +
                "nwr[amenity=planetarium];" +
                "nwr[amenity=stripclub];" +
                "nwr[amenity=social_centre];" +
                "nwr[amenity=community_centre];" +
                "nwr[amenity=playground][indoor=yes];" +
                "nwr[amenity=research_institute];" +
                "nwr[amenity=music_school];" +
                "nwr[amenity=school];" +
                "nwr[amenity=courthouse];" +
                "nwr[amenity=post_office];" +
                "nwr[amenity=university];" +
                "nwr[building=university];" +
                "nwr[building=college];" +
                "nwr[amenity=hospital];" +
                "nwr[amenity=clinic];" +
                "nwr[amenity=dentist];" +
                "nwr[amenity=doctors];" +
                "nwr[amenity=pharmacy];" +
                "nwr[amenity=veterinary];" +
                "nwr[amenity=social_facility];" +
                "nwr[amenity=bank];" +
                "nwr[healthcare];" +
                "nwr[tourism=museum];" +
                "nwr[tourism=attraction];" +
                "nwr[tourism=zoo];" +
                "nwr[tourism=gallery];" +
                "nwr[tourism=hotel];" +
                "nwr[tourism][building];" +
                ");out center qt;";
        }
    }
}
