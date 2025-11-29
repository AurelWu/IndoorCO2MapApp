namespace IndoorCO2MapAppV2.CO2Monitors
{
    internal record CO2Reading(
        int Ppm,
        long RelativeTimeStamp,
        DateTime DateTime
    );
}