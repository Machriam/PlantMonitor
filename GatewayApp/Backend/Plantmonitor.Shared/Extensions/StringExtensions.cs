﻿namespace Plantmonitor.Shared.Extensions;

public static class StringExtensions
{
    public static bool IsEmpty(this string? value)
    {
        return string.IsNullOrEmpty(value);
    }
}
