namespace PlantMonitorControl.Features.AppsettingsConfiguration;

public interface IEnvironmentConfiguration
{
    MotorPinout MotorPinout { get; }
    IRPrograms IRPrograms { get; }
    Temp2ClickPrograms Temp2ClickPrograms { get; }
}

public class EnvironmentConfiguration(ConfigurationOptions options) : IEnvironmentConfiguration
{
    public MotorPinout MotorPinout => options.MotorPinout;
    public IRPrograms IRPrograms => options.IRPrograms;
    public Temp2ClickPrograms Temp2ClickPrograms => options.Temp2ClickPrograms;
}
