using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Plantmonitor.Shared.Extensions;

#if DEBUG

public static class Diagnostics
{
    private static Stopwatch? s_sw;

    public static void StartNew()
    {
        s_sw = Stopwatch.StartNew();
    }

    public static void LogTime(string text = "", [CallerLineNumber] int line = 0, [CallerMemberName] string methodName = "")
    {
        s_sw ??= Stopwatch.StartNew();
        Console.WriteLine(new[] { "Line: " + line, methodName, s_sw.ElapsedMilliseconds.ToString(), text }.Concat("\t"));
    }
}

#endif