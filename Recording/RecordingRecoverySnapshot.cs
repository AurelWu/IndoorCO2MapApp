namespace IndoorCO2MapAppV2.Recording
{
    /// <summary>
    /// a snapshot that allows restoring
    /// a recording after the app is killed
    /// or suspended by the OS.
    /// </summary>
    public class RecordingRecoverySnapshot
    {

        public long RecordingStart { get; set; }

        public long NwrID { get; set; }
        public string NwrType { get; set; } = "";
        public string LocationName { get; set; } = "";
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        
        public string MonitorType { get; set; } = ""; //maybe not needed
        public string? MonitorDeviceId { get; set; }  

        // public string? DoorState { get; set; }
        // public string? WindowState { get; set; }
        // public string? VentilationState { get; set; }
        // public string? CustomNote { get; set; }
        // public double? TrimSliderValue { get; set; }
    }
}