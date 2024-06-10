namespace PlantMonitorControl.Features.AppsettingsConfiguration;
public record struct MotorPinout
{
    public int Enable { get; set; }
    public int Voltage { get; set; }
    public int Direction { get; set; }
    public int Pulse { get; set; }
}
public record struct IRPrograms(string DeviceHealth, string CaptureImage, string StreamData);
public record struct Temp2ClickPrograms(string GetDevices, string WriteThermalData, string PythonExecutable);
public record struct PowerSwitchPrograms(string SwitchOutlet, string ReceiveTest);
public record struct PowerSwitchPinout(int TX, int TXIN, int WiringPiTX, int WiringPiTXIN);

public record struct ConfigurationOptions
{
    public static string LogFileLocation => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "server.logs");
    public const string Configuration = nameof(Configuration);
    public MotorPinout MotorPinout { get; set; }
    public IRPrograms IRPrograms { get; set; }
    public Temp2ClickPrograms Temp2ClickPrograms { get; set; }
    public PowerSwitchPrograms PowerSwitchPrograms { get; set; }
    public PowerSwitchPinout PowerSwitchPinout { get; set; }
}
