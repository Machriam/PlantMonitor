using System;
using System.Collections.Generic;

namespace Plantmonitor.DataModel.DataModel;

public partial class AutomaticPhotoTour
{
    public long Id { get; set; }

    public Guid DeviceId { get; set; }

    public string Name { get; set; } = null!;

    public string Comment { get; set; } = null!;

    public float IntervallInMinutes { get; set; }

    public bool Finished { get; set; }

    public float PixelSizeInMm { get; set; }

    public virtual ICollection<PhotoTourEvent> PhotoTourEvents { get; set; } = new List<PhotoTourEvent>();

    public virtual ICollection<PhotoTourPlant> PhotoTourPlants { get; set; } = new List<PhotoTourPlant>();

    public virtual ICollection<PhotoTourTrip> PhotoTourTrips { get; set; } = new List<PhotoTourTrip>();

    public virtual ICollection<TemperatureMeasurement> TemperatureMeasurements { get; set; } = new List<TemperatureMeasurement>();
}
