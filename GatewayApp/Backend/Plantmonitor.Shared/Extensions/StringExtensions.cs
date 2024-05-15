using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Plantmonitor.Shared.Extensions;

public static class StringExtensions
{
    public static byte[] GetBytesFromIrFilePath(this string irFilePath)
    {
        return File.ReadAllText(irFilePath)
            .Replace("\n", " ")
            .Split(" ")
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .SelectMany(x => BitConverter.GetBytes(int.Parse(x.Trim())))
            .ToArray();
    }

    [MemberNotNullWhen(false)]
    public static bool IsEmpty(this string? value)
    {
        return string.IsNullOrEmpty(value);
    }
}
