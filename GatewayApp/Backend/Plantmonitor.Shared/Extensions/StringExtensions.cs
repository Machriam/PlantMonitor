using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Xml.XPath;

namespace Plantmonitor.Shared.Extensions;

public static class StringExtensions
{
    public static decimal? ExtractNumbersFromString(this string text, out string notNumericText)
    {
        var numberText = new StringBuilder();
        var stringText = new StringBuilder();
        foreach (var c in text)
        {
            if (char.IsNumber(c)) numberText.Append(c);
            else stringText.Append(c);
        }
        notNumericText = stringText.ToString();
        return decimal.TryParse(numberText.ToString(), out var result) ? result : null;
    }

    public static int TemperatureInKFromIrPath(this string irFilePath)
    {
        return int.TryParse(Path.GetFileNameWithoutExtension(irFilePath).Split('_').Last(), out var temperature) ? temperature : default;
    }

    public static FileData GetBytesFromIrFilePath(this string irFilePath, out int temperatureInK)
    {
        temperatureInK = irFilePath.TemperatureInKFromIrPath();
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
