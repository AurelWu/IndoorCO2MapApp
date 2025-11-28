using IndoorCO2MapAppV2.Spatial;
using System.Collections.Generic;
using System.Text.Json;

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

    internal static void ParseTransitLocationOverpassResponse()
    {
        //we request both transit stops and lines with same request to reduce amount of requests but we parse it separated to keep methods cleaner
        throw new System.NotImplementedException();
    }

    internal static void ParseTransitLineOverpassResponse()
    {
        //we request both transit stops and lines with same request to reduce amount of requests but we parse it separated to keep methods cleaner
        throw new System.NotImplementedException();
    }
}
