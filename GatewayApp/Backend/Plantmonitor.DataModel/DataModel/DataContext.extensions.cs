using Microsoft.EntityFrameworkCore;

namespace Plantmonitor.DataModel.DataModel;

public static class IDbQueryableExtensions
{
    private static bool IsDbSet<T>(IQueryable<T> list) => list.GetType().Name.Contains("dbset", StringComparison.InvariantCultureIgnoreCase);

    public static void Add<T>(this IQueryable<T> list, T value) where T : class
    {
        if (IsDbSet(list))
        {
            var dbSet = (DbSet<T>)list;
            dbSet.Add(value);
            return;
        }
        var listSet = (IList<T>)list;
        listSet.Add(value);
    }

    public static void AddRange<T>(this IQueryable<T> list, IEnumerable<T> values) where T : class
    {
        if (IsDbSet(list))
        {
            var dbSet = (DbSet<T>)list;
            dbSet.AddRange(values);
            return;
        }
        var listSet = (IList<T>)list;
        foreach (var value in values) listSet.Add(value);
    }

    public static void RemoveRange<T>(this IQueryable<T> list, IEnumerable<T> values) where T : class
    {
        if (IsDbSet(list))
        {
            var dbSet = (DbSet<T>)list;
            dbSet.RemoveRange(values);
            return;
        }
        var listSet = (IList<T>)list;
        foreach (var value in values) listSet.Remove(value);
    }
}

public partial class DataContext
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
