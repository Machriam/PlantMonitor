namespace PlantMonitorControl.Features.AppsettingsConfiguration;

public interface IEnvironmentConfiguration
{
    MotorPinout MotorPinout { get; }
}

public class EnvironmentConfiguration(ConfigurationOptions options) : IEnvironmentConfiguration
{
    public MotorPinout MotorPinout => options.MotorPinout;
}
