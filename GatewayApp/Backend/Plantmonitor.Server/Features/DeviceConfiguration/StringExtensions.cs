namespace Plantmonitor.Server.Features.DeviceConfiguration;

public static class StringExtensions
{
    public static IEnumerable<string> ToIpRange(this string from, string to)
    {
        var fromSplit = from.Split('.');
        var toSplit = to.Split('.');
        var start = int.Parse(fromSplit.Last());
        var end = int.Parse(toSplit.Last());
        for (var i = start; i <= end; i++)
        {
            yield return fromSplit.Take(3).Append(i.ToString()).Concat(".");
        }
    }
}
