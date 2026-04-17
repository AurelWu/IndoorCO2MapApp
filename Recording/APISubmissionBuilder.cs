using IndoorCO2MapAppV2.CO2Monitors;
using IndoorCO2MapAppV2.Recording;
using System;
using System.Collections.Generic;
using IndoorCO2MapAppV2.Enumerations;

namespace IndoorCO2MapAppV2.Recording
{
    public class APISubmissionBuilder
    {
        private readonly BuildingRecording _rec;
        private readonly int _trimMin;
        private readonly int _trimMax;

        // Additional metadata for submission
        private string _occupancy = "undefined";
        private string _notes = "";
        private TriState _openWindowsDoors = TriState.Unknown;
        private TriState _ventilationSystem = TriState.Unknown;
        private string _submissionId = "";

        public APISubmissionBuilder(BuildingRecording recording, int trimMin, int trimMax)
        {
            _rec = recording;
            _trimMin = trimMin;
            _trimMax = trimMax;
        }
        

        public BuildingSubmissionData Build()
        {
            var submission = new BuildingSubmissionData(
                sensorType: _rec.CO2MonitorType,
                sensorID: _submissionId,
                nwrType: _rec.NwrType,
                nwrID: _rec.NwrId,
                nwrName: _rec.LocationName,
                nwrLat: _rec.Latitude,                               
                nwrLon: _rec.Longitude,
                startTime: _rec.RecordingStart,
                isRecovery: false
            );

            submission.OccupancyLevel = _occupancy;
            submission.AdditionalNotes = _notes;
            submission.OpenWindowsDoors = TriStateToString(_openWindowsDoors);
            submission.VentilationSystem = TriStateToString(_ventilationSystem);
            submission.AdditionalNotes = _notes;
            ApplyTrimmedMeasurements(submission);

            return submission;
        }

        private void ApplyTrimmedMeasurements(BuildingSubmissionData submission)
        {
            var source = _rec.MeasurementData;

            int end = Math.Min(_trimMax, source.Count - 1);

            for (int i = _trimMin; i <= end; i++)
            {
                var m = source[i];
                submission.MeasurementData.Add(m);
            }
        }

        public APISubmissionBuilder WithOpenWindowsDoors(TriState value)
        {
            _openWindowsDoors = value;
            return this;
        }

        public APISubmissionBuilder WithVentilationSystem(TriState value)
        {
            _ventilationSystem = value;
            return this;
        }

        public APISubmissionBuilder WithOccupancy(string occupancy)
        {
            _occupancy = occupancy;
            return this;
        }

        public APISubmissionBuilder WithNotes(string notes)
        {
            _notes = notes;
            return this;
        }

        public APISubmissionBuilder WithSubmissionId(string id)
        {
            _submissionId = id;
            return this;
        }

        private static string TriStateToString(TriState value) => value switch
        {
            TriState.Yes => "true",
            TriState.No  => "false",
            _            => "noData"
        };
    }
}
