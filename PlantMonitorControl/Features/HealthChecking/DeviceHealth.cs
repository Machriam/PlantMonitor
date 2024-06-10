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
        SystemCalibrated = 16,
        CanSwitchOutlets = 32,
    }

    public class DeviceHealth
    {
        public string DeviceName { get; set; } = "";
        public string DeviceId { get; set; } = "";
        public HealthState State { get; set; }
    }
}
