namespace Plantmonitor.Shared.Extensions;

public static class IntExtensions
{
    public static float KelvinToCelsius(this int kelvin) => (kelvin - 27315f) / 100f;

    public static float KelvinToCelsius(this float kelvin) => float.Round(kelvin - 273.15f, 2);
}
