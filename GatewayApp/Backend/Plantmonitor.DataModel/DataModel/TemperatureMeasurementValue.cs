using System;
using System.Collections.Generic;

namespace Plantmonitor.DataModel.DataModel;

public partial class TemperatureMeasurementValue
{
    public long Id { get; set; }

    public float Temperature { get; set; }

    public DateTime Timestamp { get; set; }

    public long MeasurementFk { get; set; }

    public virtual TemperatureMeasurement MeasurementFkNavigation { get; set; } = null!;
}
