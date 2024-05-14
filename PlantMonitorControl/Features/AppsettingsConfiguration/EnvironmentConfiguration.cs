namespace PlantMonitorControl.Features.AppsettingsConfiguration;

public interface IEnvironmentConfiguration
{
    MotorPinout MotorPinout { get; }
    IRPrograms IRPrograms { get; }
}

public class EnvironmentConfiguration(ConfigurationOptions options) : IEnvironmentConfiguration
{
    public MotorPinout MotorPinout => options.MotorPinout;
    public IRPrograms IRPrograms => options.IRPrograms;
}
