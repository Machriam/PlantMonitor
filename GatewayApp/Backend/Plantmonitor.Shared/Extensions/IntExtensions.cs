namespace Plantmonitor.Shared.Extensions;

public static class IntExtensions
{
    public static float KelvinToCelsius(this int kelvin) => (kelvin - 27315f) / 100f;
}
