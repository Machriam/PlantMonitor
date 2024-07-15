namespace PlantMonitorControl.Features.HealthChecking
{
    [Flags]
    public enum HealthState
    {
        NA = 0,
        NoirCameraFound = 1,
        ThermalCameraFound = 2,
        NoirCameraFunctional = 4,
        ThermalCameraFunctional = 8,
        HasTemperatureSensor = 16,
        CanSwitchOutlets = 32,
    }

    public record class IrCameraOffset(int Left, int Top);

    public class DeviceHealth
    {
        public string DeviceName { get; set; } = "";
        public string DeviceId { get; set; } = "";
        public HealthState State { get; set; }
        public IrCameraOffset CameraOffset { get; set; } = new(119, 48);
    }
}
