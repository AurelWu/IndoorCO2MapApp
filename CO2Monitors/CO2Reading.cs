namespace IndoorCO2MapAppV2.CO2Monitors
{
    public record CO2Reading(
        ushort Ppm,
        long RelativeTimeStamp,
        DateTime DateTime
    );
}