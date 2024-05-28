using System.Runtime.Serialization;

namespace Plantmonitor.Shared.Features.ImageStreaming;

[KnownType(typeof(TemperatureStreamData))]
public record struct TemperatureStreamData(float TemperatureInC, string Device, DateTime Time);
