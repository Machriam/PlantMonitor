using System;
using System.Collections.Generic;

namespace Plantmonitor.DataModel.DataModel;

public partial class AutomaticPhotoTour
{
    public long Id { get; set; }

    public Guid DeviceId { get; set; }

    public string Name { get; set; } = null!;

    public string Comment { get; set; } = null!;

    public virtual ICollection<PhotoTourJourney> PhotoTourJourneys { get; set; } = new List<PhotoTourJourney>();

    public virtual ICollection<TemperatureMeasurement> TemperatureMeasurements { get; set; } = new List<TemperatureMeasurement>();
}
