using System;
using System.Collections.Generic;
using System.Text;

namespace IndoorCO2MapAppV2.Spatial
{
    internal sealed class LocationStore
    {
        private static readonly Lazy<LocationStore> _instance =
            new(() => new LocationStore());

        public static LocationStore Instance => _instance.Value;

        // ---- Private internal lists ----
        private List<LocationData> _buildingLocationData = [];
        private List<LocationData> _transportStartLocationData = [];
        private List<LocationData> _transportDestinationLocationData = [];
        private List<TransitLineData> _transitLines = [];

        // ---- Public read-only views ----
        internal IReadOnlyList<LocationData> BuildingLocationData => _buildingLocationData;
        internal IReadOnlyList<LocationData> TransportStartLocationData => _transportStartLocationData;
        internal IReadOnlyList<LocationData> TransportDestinationLocationData => _transportDestinationLocationData;
        internal IReadOnlyList<TransitLineData> TransitLines => _transitLines;

        private LocationStore() { }

        // ---- Replace full lists safely ----
        internal void SetBuildingLocations(IEnumerable<LocationData> newData)
            => _buildingLocationData = newData?.ToList() ?? [];

        internal void SetTransportStartLocations(IEnumerable<LocationData> newData)
            => _transportStartLocationData = newData?.ToList() ?? [];

        internal void SetTransportDestinationLocations(IEnumerable<LocationData> newData)
            => _transportDestinationLocationData = newData?.ToList() ?? [];

        internal void SetTransitLines(IEnumerable<TransitLineData> newData)
            => _transitLines = newData?.ToList() ?? [];

        // ---- Sorted View Helpers ----

        internal IReadOnlyList<LocationData> GetBuildingsSortedByDistance()
        {
            return [.. _buildingLocationData.OrderBy(loc => loc.Distance)];
        }

        internal IReadOnlyList<LocationData> GetBuildingsSortedByName()
        {
            return [.. _buildingLocationData.OrderBy(loc => loc.Name ?? string.Empty)];
        }


        // ---- Clear everything ----
        internal void ClearAll()
        {
            _buildingLocationData.Clear();
            _transportStartLocationData.Clear();
            _transportDestinationLocationData.Clear();
            _transitLines.Clear();
        }
    }
}
