using IndoorCO2MapAppV2.Spatial;
using System.Collections.Generic;
using System.Text.Json;
using System.Linq;

internal static class OverpassDataParser
{
    internal static List<LocationData> ParseBuildingLocationOverpassResponse(string response, double userLat, double userLon, bool updateLocationStore = true, bool keepResultsIfEmptyResults = true)
    {
        var locations = new List<LocationData>();
        using var doc = JsonDocument.Parse(response);

        if (!doc.RootElement.TryGetProperty("elements", out var elements))
            return locations;

        foreach (var element in elements.EnumerateArray())
        {
            if (!element.TryGetProperty("type", out var typeProp) ||
                !element.TryGetProperty("id", out var idProp))
                continue;

            string type = typeProp.GetString() ?? "";
            long id = idProp.GetInt64();

            // coordinates
            double lat, lon;
            if (element.TryGetProperty("center", out var center))
            {
                if (!center.TryGetProperty("lat", out var latProp) ||
                    !center.TryGetProperty("lon", out var lonProp))
                    continue;

                lat = latProp.GetDouble();
                lon = lonProp.GetDouble();
            }
            else if (element.TryGetProperty("lat", out var latProp) && element.TryGetProperty("lon", out var lonProp))
            {
                lat = latProp.GetDouble();
                lon = lonProp.GetDouble();
            }
            else
            {
                continue; // skip elements without coordinates
            }

            // tags and name
            string name = "";
            if (element.TryGetProperty("tags", out var tags) && tags.TryGetProperty("name", out var nameProp))
                name = nameProp.GetString() ?? "";

            locations.Add(new LocationData(type, id, name, lat, lon, userLat, userLon));
        }
        if(updateLocationStore)
        {
            if (keepResultsIfEmptyResults == true && locations.Count == 0) return locations;
            LocationStore.Instance.SetBuildingLocations(locations);
        }

        

        return locations;
    }

    // Both stops and lines come from the same Overpass response (startLocation=true query).
    // Stops = nwr elements; Lines = relation elements with a [route=*] tag.

    internal static List<LocationData> ParseTransitStopsFromOverpassResponse(string response, double userLat, double userLon)
    {
        var stops = new List<LocationData>();
        using var doc = JsonDocument.Parse(response);
        if (!doc.RootElement.TryGetProperty("elements", out var elements))
            return stops;

        // Deduplicate by name (same logic as PMTiles: keep station over stop)
        var candidates = new Dictionary<string, (LocationData Data, int Priority)>(StringComparer.OrdinalIgnoreCase);

        foreach (var el in elements.EnumerateArray())
        {
            if (!el.TryGetProperty("type", out var typeProp)) continue;
            string type = typeProp.GetString() ?? "";

            // Skip relations — those are routes, handled separately
            if (type == "relation") continue;

            if (!el.TryGetProperty("id", out var idProp)) continue;
            long id = idProp.GetInt64();

            double lat, lon;
            if (el.TryGetProperty("center", out var center))
            {
                if (!center.TryGetProperty("lat", out var lp) || !center.TryGetProperty("lon", out var lnp)) continue;
                lat = lp.GetDouble(); lon = lnp.GetDouble();
            }
            else if (el.TryGetProperty("lat", out var lp2) && el.TryGetProperty("lon", out var lnp2))
            {
                lat = lp2.GetDouble(); lon = lnp2.GetDouble();
            }
            else continue;

            if (!el.TryGetProperty("tags", out var tags)) continue;
            string name = tags.TryGetProperty("name", out var np) ? (np.GetString() ?? "") : "";
            if (string.IsNullOrWhiteSpace(name)) continue;

            int priority = 1;
            if (tags.TryGetProperty("public_transport", out var pt) && pt.GetString() == "station")
                priority = 0;

            string key = name.ToLowerInvariant();
            var loc = new LocationData(type, id, name, lat, lon, userLat, userLon);
            if (!candidates.TryGetValue(key, out var existing) ||
                priority < existing.Priority ||
                (priority == existing.Priority && id < existing.Data.ID))
            {
                candidates[key] = (loc, priority);
            }
        }

        foreach (var (data, _) in candidates.Values)
            stops.Add(data);

        return stops;
    }

    internal static List<TransitLineData> ParseTransitLinesFromOverpassResponse(string response, double userLat, double userLon)
    {
        var lines = new List<TransitLineData>();
        var seen = new HashSet<long>();
        using var doc = JsonDocument.Parse(response);
        if (!doc.RootElement.TryGetProperty("elements", out var elements))
            return lines;

        foreach (var el in elements.EnumerateArray())
        {
            if (!el.TryGetProperty("type", out var typeProp) || typeProp.GetString() != "relation") continue;
            if (!el.TryGetProperty("id", out var idProp)) continue;
            long id = idProp.GetInt64();
            if (!seen.Add(id)) continue;

            if (!el.TryGetProperty("tags", out var tags)) continue;
            string routeType = tags.TryGetProperty("route", out var rt) ? (rt.GetString() ?? "") : "";
            if (string.IsNullOrWhiteSpace(routeType)) continue;

            string name = tags.TryGetProperty("name", out var np) ? (np.GetString() ?? "") : "";
            if (string.IsNullOrWhiteSpace(name)) continue;

            // Relations from Overpass with `out center` have a center element
            double lat = userLat, lon = userLon;
            if (el.TryGetProperty("center", out var center))
            {
                if (center.TryGetProperty("lat", out var lp) && center.TryGetProperty("lon", out var lnp))
                {
                    lat = lp.GetDouble();
                    lon = lnp.GetDouble();
                }
            }

            lines.Add(new TransitLineData(routeType, "relation", id, name, lat, lon));
        }

        return lines;
    }
}
