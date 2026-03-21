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

        // --- Query builders ---

        internal static string CreateTransportOverpassQuery(double latitude, double longitude, double radius, bool startLocation)
        {
            string rString = radius.ToString(CultureInfo.InvariantCulture);
            string latString = latitude.ToString(CultureInfo.InvariantCulture);
            string lonString = longitude.ToString(CultureInfo.InvariantCulture);

            if (startLocation)
            {
                return "[out:json];" +
                    "(" +
                    $"nwr(around:{rString},{latString},{lonString})[railway=tram_stop];" +
                    $"relation(around:{rString},{latString},{lonString})[route=tram];" +
                    $"nwr(around:{rString},{latString},{lonString})[highway=bus_stop];" +
                    $"relation(around:{rString},{latString},{lonString})[route=bus];" +
                    $"nwr(around:{rString},{latString},{lonString})[railway=subway_station];" +
                    $"relation(around:{rString},{latString},{lonString})[route=subway];" +
                    $"nwr(around:{rString},{latString},{lonString})[railway=station];" +
                    $"nwr(around:{rString},{latString},{lonString})[railway=halt];" +
                    $"nwr(around:{rString},{latString},{lonString})[railway=stop];" +
                    $"relation(around:{rString},{latString},{lonString})[route=train];" +
                    $"relation(around:{rString},{latString},{lonString})[route=light_rail];" +
                    $"relation(around:{rString},{latString},{lonString})[route=monorail];" +
                    ");" +
                    "out center tags qt;";
            }
            else
            {
                return "[out:json];" +
                    "(" +
                    $"nwr(around:{rString},{latString},{lonString})[railway=tram_stop];" +
                    $"nwr(around:{rString},{latString},{lonString})[highway=bus_stop];" +
                    $"nwr(around:{rString},{latString},{lonString})[railway=subway_station];" +
                    $"nwr(around:{rString},{latString},{lonString})[railway=station];" +
                    $"nwr(around:{rString},{latString},{lonString})[railway=stop];" +
                    $"nwr(around:{rString},{latString},{lonString})[railway=halt];" +
                    ");" +
                    "out center tags qt;";
            }
        }

        internal static string CreateBuildingOverpassQuery(double latitude, double longitude, double radius)
        {
            string rString = radius.ToString(CultureInfo.InvariantCulture);
            string latString = latitude.ToString(CultureInfo.InvariantCulture);
            string lonString = longitude.ToString(CultureInfo.InvariantCulture);

            if (_cachedEntries is { Count: > 0 } entries)
                return BuildQueryFromEntries(entries, rString, latString, lonString);

            return BuildHardcodedQuery(rString, latString, lonString);
        }

        private static string BuildQueryFromEntries(
            List<WhitelistEntry> entries, string rString, string latString, string lonString)
        {
            var sb = new StringBuilder("[out:json];(");
            foreach (var entry in entries)
            {
                var filter = string.Join("][", entry.Conditions);
                sb.Append($"nwr(around:{rString},{latString},{lonString})[{filter}];");
            }
            sb.Append(");out center qt;");
            return sb.ToString();
        }

        private static string BuildHardcodedQuery(string rString, string latString, string lonString)
        {
            return "[out:json];" +
                "(" +
                $"nwr(around:{rString},{latString},{lonString})[office=employment_agency];" +
                $"nwr(around:{rString},{latString},{lonString})[office=lawyer];" +
                $"nwr(around:{rString},{latString},{lonString})[office=educational_institution];" +
                $"nwr(around:{rString},{latString},{lonString})[office=government];" +
                $"nwr(around:{rString},{latString},{lonString})[office=political_party];" +
                $"nwr(around:{rString},{latString},{lonString})[office=coworking];" +
                $"nwr(around:{rString},{latString},{lonString})[government=register_office];" +
                $"nwr(around:{rString},{latString},{lonString})[shop];" +
                $"nwr(around:{rString},{latString},{lonString})[craft];" +
                $"nwr(around:{rString},{latString},{lonString})[aeroway=aerodrome];" +
                $"nwr(around:{rString},{latString},{lonString})[aeroway=terminal];" +
                $"nwr(around:{rString},{latString},{lonString})[railway=station];" +
                $"nwr(around:{rString},{latString},{lonString})[public_transport=station];" +
                $"nwr(around:{rString},{latString},{lonString})[leisure=fitness_centre];" +
                $"nwr(around:{rString},{latString},{lonString})[leisure=bowling_alley];" +
                $"nwr(around:{rString},{latString},{lonString})[leisure=sports_centre];" +
                $"nwr(around:{rString},{latString},{lonString})[leisure=sports_hall];" +
                $"nwr(around:{rString},{latString},{lonString})[sport=swimming];" +
                $"nwr(around:{rString},{latString},{lonString})[leisure=swimming_pool];" +
                $"nwr(around:{rString},{latString},{lonString})[leisure=sauna];" +
                $"nwr(around:{rString},{latString},{lonString})[leisure=hackerspace];" +
                $"nwr(around:{rString},{latString},{lonString})[leisure=escape_game];" +
                $"nwr(around:{rString},{latString},{lonString})[leisure=dance];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=studio];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=townhall];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=car_rental];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=convention_centre];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=conference_centre];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=congress_centre];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=events_centre];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=bar];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=place_of_worship];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=pub];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=restaurant];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=cafe];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=fast_food];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=food_court];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=ice_cream];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=college];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=dancing_school];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=driving_school];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=kindergarten];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=language_school];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=library];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=cinema];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=theatre];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=concert_hall];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=music_venue];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=arts_centre];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=brothel];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=love_hotel];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=nightclub];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=planetarium];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=stripclub];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=social_centre];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=community_centre];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=playground][indoor=yes];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=research_institute];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=music_school];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=school];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=courthouse];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=post_office];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=university];" +
                $"nwr(around:{rString},{latString},{lonString})[building=university];" +
                $"nwr(around:{rString},{latString},{lonString})[building=college];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=hospital];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=clinic];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=dentist];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=doctors];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=pharmacy];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=veterinary];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=social_facility];" +
                $"nwr(around:{rString},{latString},{lonString})[amenity=bank];" +
                $"nwr(around:{rString},{latString},{lonString})[healthcare];" +
                $"nwr(around:{rString},{latString},{lonString})[tourism=museum];" +
                $"nwr(around:{rString},{latString},{lonString})[tourism=attraction];" +
                $"nwr(around:{rString},{latString},{lonString})[tourism=zoo];" +
                $"nwr(around:{rString},{latString},{lonString})[tourism=gallery];" +
                $"nwr(around:{rString},{latString},{lonString})[tourism=hotel];" +
                $"nwr(around:{rString},{latString},{lonString})[tourism][building];" +
                ");" +
                "out center qt;";
        }
    }
}
