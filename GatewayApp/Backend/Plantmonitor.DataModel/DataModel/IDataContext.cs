using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Plantmonitor.DataModel.DataModel;

public interface IDataContext : IAsyncDisposable, IDisposable
{
    public DatabaseFacade Database { get; }

    public int SaveChanges();

    DbSet<AutomaticPhotoTour> AutomaticPhotoTours { get; set; }
    DbSet<ConfigurationDatum> ConfigurationData { get; set; }
    DbSet<DeviceMovement> DeviceMovements { get; set; }
    DbSet<DeviceSwitchAssociation> DeviceSwitchAssociations { get; set; }
    DbSet<PhotoTourEvent> PhotoTourEvents { get; set; }
    DbSet<PhotoTourTrip> PhotoTourTrips { get; set; }
    DbSet<SwitchableOutletCode> SwitchableOutletCodes { get; set; }
    DbSet<TemperatureMeasurement> TemperatureMeasurements { get; set; }
    DbSet<TemperatureMeasurementValue> TemperatureMeasurementValues { get; set; }

    DataContext.EventLogger CreatePhotoTourEventLogger(long photourId);
}
