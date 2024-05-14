using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Plantmonitor.Shared.Extensions;

public static class StringExtensions
{
    [MemberNotNullWhen(false)]
    public static bool IsEmpty(this string? value)
    {
        return string.IsNullOrEmpty(value);
    }
}
