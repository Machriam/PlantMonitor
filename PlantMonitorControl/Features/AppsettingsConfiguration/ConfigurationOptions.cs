namespace PlantMonitorControl.Features.AppsettingsConfiguration;
public record struct MotorPinout
{
    public int Enable { get; set; }
    public int Voltage { get; set; }
    public int Direction { get; set; }
    public int Pulse { get; set; }
}
public record struct IRPrograms(string DeviceHealth, string CaptureImage, string StreamData);

public record struct ConfigurationOptions
{
    public const string Configuration = nameof(Configuration);
    public MotorPinout MotorPinout { get; set; }
    public IRPrograms IRPrograms { get; set; }
}
