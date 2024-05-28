namespace Plantmonitor.Shared.Features.ImageStreaming;

public record struct TemperatureStreamData(float TemperatureInC, string Device, DateTime Time);
