namespace IndoorCO2MapAppV2.Spatial
{
    /// <summary>
    /// How the app identifies itself to third-party OSM services. Both the OSMF
    /// tile usage policy and Overpass require a User-Agent that names the app and
    /// offers a way to reach us; a library's generic default is explicitly not
    /// acceptable and may be blocked without notice.
    /// </summary>
    public static class AppIdentity
    {
        public const string UserAgent =
            "IndoorCO2DataRecorder/1.0 (https://indoorco2Map.com; contact: aurelwuensch@proton.me)";

        /// <summary>
        /// Must stay visible on every screen that renders OSM tiles — an ODbL
        /// obligation, not just tile-server etiquette.
        /// </summary>
        public const string OsmAttribution = "© OpenStreetMap contributors";
    }
}
