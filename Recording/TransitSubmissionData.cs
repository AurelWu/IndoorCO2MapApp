using Newtonsoft.Json.Linq;
using System.Globalization;
using IndoorCO2MapAppV2.Enumerations;
using IndoorCO2MapAppV2.CO2Monitors;
using IndoorCO2MapAppV2.Spatial;

namespace IndoorCO2MapAppV2.Recording
{
    /// <summary>
    /// Builds the transit JSON payload that matches the old SubmissionDataTransport format
    /// so the backend requires no changes.
    /// Keys: d, b, st, si, sn, dt, di, dn, lt, li, ln, c, t, la, lo, a
    /// </summary>
    internal class TransitSubmissionData
    {
        private readonly string _sensorType;
        private readonly string _sensorId;
        private readonly long _startTime;

        private readonly string _startNWRType;
        private readonly long _startID;
        private readonly string _startName;

        private readonly string _endNWRType;
        private readonly long _endID;
        private readonly string _endName;

        private readonly string _routeNWRType;
        private readonly long _routeID;
        private readonly string _routeName;

        private readonly List<CO2Reading> _measurements;
        private readonly string _notes;

        public TransitSubmissionData(
            string sensorType, string sensorId, long startTime,
            string startNWRType, long startID, string startName,
            string endNWRType, long endID, string endName,
            string routeNWRType, long routeID, string routeName,
            List<CO2Reading> measurements, string notes)
        {
            _sensorType = sensorType;
            _sensorId = sensorId;
            _startTime = startTime;
            _startNWRType = startNWRType;
            _startID = startID;
            _startName = startName;
            _endNWRType = endNWRType;
            _endID = endID;
            _endName = endName;
            _routeNWRType = routeNWRType;
            _routeID = routeID;
            _routeName = routeName;
            _measurements = measurements;
            _notes = notes;
        }

        public string ToJson()
        {
            int count = _measurements.Count;
            var ppm = new string[count];
            var ts = new string[count];
            for (int i = 0; i < count; i++)
            {
                ppm[i] = _measurements[i].Ppm.ToString(CultureInfo.InvariantCulture);
                ts[i] = _measurements[i].RelativeTimeStamp.ToString(CultureInfo.InvariantCulture);
            }

            var json = new JObject
            {
                ["d"] = $"NewApp_{_sensorType}_{_sensorId}",
                ["b"] = _startTime,
                ["st"] = _startNWRType,
                ["si"] = _startID,
                ["sn"] = _startName,
                ["dt"] = _endNWRType,
                ["di"] = _endID,
                ["dn"] = _endName,
                ["lt"] = _routeNWRType,
                ["li"] = _routeID,
                ["ln"] = _routeName,
                ["c"] = string.Join(";", ppm),
                ["t"] = string.Join(";", ts),
                ["la"] = "",
                ["lo"] = "",
                ["a"] = _notes
            };

            return json.ToString();
        }

        /// <summary>
        /// Convenience factory: builds from a BuildingRecording that has transit metadata
        /// stored in AdditionalDataByParameter, applying trim range.
        /// endpoint is optional — if null, dt/di/dn fields are empty/zero.
        /// </summary>
        public static TransitSubmissionData FromRecording(
            BuildingRecording rec, int trimMin, int trimMax, string notes,
            LocationData? endpoint = null)
        {
            var d = rec.AdditionalDataByParameter;

            d.TryGetValue("startNWRType", out var startNWRType);
            d.TryGetValue("startName",    out var startName);
            d.TryGetValue("routeNWRType", out var routeNWRType);
            d.TryGetValue("routeName",    out var routeName);

            long.TryParse(d.GetValueOrDefault("startID", "0"), out long startID);
            long.TryParse(d.GetValueOrDefault("routeID", "0"), out long routeID);

            int end = Math.Min(trimMax, rec.MeasurementData.Count - 1);
            var measurements = rec.MeasurementData
                .Skip(trimMin)
                .Take(end - trimMin + 1)
                .ToList();

            return new TransitSubmissionData(
                sensorType:   rec.CO2MonitorType,
                sensorId:     Random.Shared.Next(0, 999999).ToString(),
                startTime:    rec.RecordingStart,
                startNWRType: startNWRType ?? "node",
                startID:      startID,
                startName:    startName ?? "",
                endNWRType:   endpoint?.Type ?? "",
                endID:        endpoint?.ID ?? 0,
                endName:      endpoint?.Name ?? "",
                routeNWRType: routeNWRType ?? "relation",
                routeID:      routeID,
                routeName:    routeName ?? "",
                measurements: measurements,
                notes:        notes);
        }
    }
}
