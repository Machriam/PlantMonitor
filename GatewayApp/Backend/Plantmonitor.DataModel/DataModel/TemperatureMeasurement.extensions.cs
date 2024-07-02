using System;
using System.Collections.Generic;

namespace Plantmonitor.DataModel.DataModel;

public partial class TemperatureMeasurement
{
    public const string FlirLeptonSensorId = "Flir Lepton";

    public bool IsThermalCamera() => SensorId == FlirLeptonSensorId;
}
