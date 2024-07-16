using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Plantmonitor.Shared.Extensions;

public static class StringExtensions
{
    public static FileData GetBytesFromIrFilePath(this string irFilePath, out int temperatureInK)
    {
        temperatureInK = int.TryParse(Path.GetFileNameWithoutExtension(irFilePath).Split('_').Last(), out var temperature) ? temperature : default;
        var data = File.ReadAllText(irFilePath)
            .Replace("\n", " ")
            .Split(" ")
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x =>
            {
                var temperature = int.Parse(x.Trim());
                var bytes = BitConverter.GetBytes(temperature);
                return (Temperature: temperature, Bytes: bytes);
            })
            .ToArray();
        return new(data.Sum(d => d.Temperature), data.SelectMany(d => d.Bytes).ToArray());
    }

    [MemberNotNullWhen(false)]
    public static bool IsEmpty(this string? value)
    {
        return string.IsNullOrEmpty(value);
    }
}

public record struct FileData(double SumOfTemperature, byte[] Bytes);
