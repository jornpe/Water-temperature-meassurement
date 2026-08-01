namespace WaterTemperature.Api.Models.Devices;

public static class BatteryPercentageCalculator
{
    public const float DefaultFullAdcVoltage = 4.2F;
    public const float DefaultEmptyAdcVoltage = 2.5F;

    public static int? Calculate(float? adcVoltage, float fullAdcVoltage, float emptyAdcVoltage)
    {
        if (!adcVoltage.HasValue
            || !float.IsFinite(adcVoltage.Value)
            || !float.IsFinite(fullAdcVoltage)
            || !float.IsFinite(emptyAdcVoltage)
            || fullAdcVoltage <= emptyAdcVoltage)
        {
            return null;
        }

        var percentage = ((adcVoltage.Value - emptyAdcVoltage) / (fullAdcVoltage - emptyAdcVoltage)) * 100F;
        return (int)MathF.Round(Math.Clamp(percentage, 0F, 100F), MidpointRounding.AwayFromZero);
    }
}
