using System.Runtime.Serialization;

namespace Plantmonitor.Shared.Features.MeasureTemperature;

[KnownType(typeof(TemperatureStreamData))]
public record struct TemperatureStreamData(float TemperatureInC, string Device, DateTime Time);
