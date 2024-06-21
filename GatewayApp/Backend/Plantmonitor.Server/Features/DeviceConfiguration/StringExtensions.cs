namespace Plantmonitor.Server.Features.DeviceConfiguration;

public static class StringExtensions
{
    public static string? GetApplicationRootGitPath(this string folderToSearchFrom)
    {
        while (!Directory.EnumerateDirectories(folderToSearchFrom, ".git").Any())
        {
            var parent = Directory.GetParent(folderToSearchFrom)?.FullName;
            if (parent == null) return null;
            folderToSearchFrom = Path.GetFullPath(parent);
        }
        return Path.GetFullPath(folderToSearchFrom);
    }

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
