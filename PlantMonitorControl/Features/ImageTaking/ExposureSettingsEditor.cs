using System.Globalization;
using System.Text.RegularExpressions;

namespace PlantMonitorControl.Features.ImageTaking;

public interface IExposureSettingsEditor
{
    ExposureSettings GetExposure();

    void UpdateExposure(ExposureSettings settings);

    ExposureSettings GetExposureFromStdout(string stdout);
}

public partial class ExposureSettingsEditor : IExposureSettingsEditor
{
    private const string ExposureSettingsFile = "exposuresettings.json";
    private static readonly string s_filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ExposureSettingsFile);

    public ExposureSettings GetExposure()
    {
        if (!File.Exists(s_filePath))
        {
            File.WriteAllText(s_filePath, new ExposureSettings().AsJson());
        }
        return File.ReadAllText(s_filePath).FromJson<ExposureSettings>() ?? throw new Exception("Could not read devicehealth.json");
    }

    public ExposureSettings GetExposureFromStdout(string stdout)
    {
        var exposureLine = stdout.Split("\n").Select(l => ExposureStdoutTemplate().Match(l))
            .FirstOrDefault(m => m.Success);
        if (exposureLine == null || exposureLine.Groups.Count < 4) return new();
        if (!double.TryParse(exposureLine.Groups[1].Value, CultureInfo.InvariantCulture, out var exposureTime)) return new();
        if (!double.TryParse(exposureLine.Groups[2].Value, CultureInfo.InvariantCulture, out var analogueGain)) return new();
        if (!double.TryParse(exposureLine.Groups[3].Value, CultureInfo.InvariantCulture, out var digitalGain)) return new();
        return new()
        {
            ExposureTimeInMicroSeconds = (int)exposureTime,
            Gain = (float)double.Round(digitalGain * analogueGain, 2)
        };
    }

    public void UpdateExposure(ExposureSettings settings)
    {
        File.WriteAllText(s_filePath, settings.AsJson());
    }

    [GeneratedRegex(".*\\(.*fps\\).*exp (\\d+\\.\\d+) ag (\\d+.\\d+) dg (\\d+.\\d+).*")]
    private static partial Regex ExposureStdoutTemplate();
}
