using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace Plantmonitor.DataModel.DataModel;

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
        modelBuilder.Entity<VirtualImageSummary>().Property(p => p.ImageDescriptors).HasColumnType("jsonb").HasColumnName("image_descriptors_json");
        modelBuilder.Entity<VirtualImageSummary>().Ignore(p => p.ImageDescriptorsJson);
        modelBuilder.Entity<PhotoTourEvent>().Property(p => p.Type).HasColumnName("type");
    }
}

public class QueryableList<T> : IList<T>, IQueryable<T> where T : class
{
    private readonly List<T> _list = [];

    public T this[int index] { get => _list[index]; set => _list[index] = value; }

    public Type ElementType => _list.AsQueryable().ElementType;

    public Expression Expression => _list.AsQueryable().Expression;

    public IQueryProvider Provider => _list.AsQueryable().Provider;

    public int Count => _list.Count;

    public bool IsReadOnly => false;

    public void Add(T item) => _list.Add(item);

    public void Clear() => _list.Clear();

    public bool Contains(T item) => _list.Contains(item);

    public void CopyTo(T[] array, int arrayIndex) => _list.CopyTo(array, arrayIndex);

    public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();

    public int IndexOf(T item) => _list.IndexOf(item);

    public void Insert(int index, T item) => _list.Insert(index, item);

    public bool Remove(T item) => _list.Remove(item);

    public void RemoveAt(int index) => _list.RemoveAt(index);

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _list.GetEnumerator();
}

public static class IQueryableExtensions
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
