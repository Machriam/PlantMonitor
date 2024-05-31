using System;
using System.Collections.Generic;

namespace Plantmonitor.DataModel.DataModel;

public partial class TemperatureMeasurement
{
    public long Id { get; set; }

    public string Comment { get; set; } = null!;

    public string DeviceId { get; set; } = null!;

    public DateTime StartTime { get; set; }

    public virtual ICollection<TemperatureMeasurementValue> TemperatureMeasurementValues { get; set; } = new List<TemperatureMeasurementValue>();
}
