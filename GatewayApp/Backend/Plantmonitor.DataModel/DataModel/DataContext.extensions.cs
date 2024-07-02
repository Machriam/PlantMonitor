using Microsoft.EntityFrameworkCore;

namespace Plantmonitor.DataModel.DataModel;

public partial class DataContext : IDataContext
{
    public delegate void EventLogger(string message, PhotoTourEventType type);

    public EventLogger CreatePhotoTourEventLogger(long photourId) => (message, type) => LogEvent(this, message, photourId, type);

    private static void LogEvent(DataContext context, string message, long phototourId, PhotoTourEventType type = PhotoTourEventType.Information)
    {
        context.PhotoTourEvents.Add(new PhotoTourEvent()
        {
            Message = message,
            Type = type,
            PhotoTourFk = phototourId,
            Timestamp = DateTime.UtcNow,
        });
        context.SaveChanges();
    }

    private partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DeviceMovement>().Property(p => p.MovementPlan).HasColumnType("jsonb").HasColumnName("movement_plan_json");
        modelBuilder.Entity<DeviceMovement>().Ignore(p => p.MovementPlanJson);
        modelBuilder.Entity<PhotoTourEvent>().Property(p => p.Type).HasColumnName("type");
    }
}
