namespace Plantmonitor.Shared.Extensions;

public static class EnumerableExtensions
{
    public static IEnumerable<T> OrderByNumericString<T>(this IEnumerable<T> source, Func<T, string> selector)
    {
        return source
            .Select(x => (Number: selector(x).ExtractNumbersFromString(out var cleanText), CleanText: cleanText, Item: x))
            .OrderBy(x => x.CleanText)
            .ThenBy(x => x.Number)
            .Select(x => x.Item);
    }

    public static float Deviation<T>(this IEnumerable<T> values, float average, Func<T, float> selector)
    {
        if (!values.Any()) return 0f;
        return (float)Math.Sqrt(values.Average(p =>
        {
            var value = selector(p);
            return (value - average) * (value - average);
        }));
    }

    public static float Median<T>(this IOrderedEnumerable<T> enumerable, Func<T, float> selector)
    {
        if (!enumerable.Any()) return 0f;
        var length = enumerable.Count();
        var middlePosition = length / 2;
        return length % 2 == 0 && length >= 2 ?
            (selector(enumerable.ElementAt(middlePosition)) + selector(enumerable.ElementAt(middlePosition - 1))) / 2f :
            selector(enumerable.ElementAt(middlePosition));
    }

    public static void DisposeItems<T>(this IEnumerable<T> list) where T : IDisposable
    {
        foreach (var item in list) item.Dispose();
    }

    public static IEnumerable<T> PushIf<T>(this IEnumerable<T> list, Func<T> item, Func<bool> condition)
    {
        if (condition()) return list.Append(item());
        return list;
    }

    public static IEnumerable<T> PushIf<T>(this IEnumerable<T> list, T item, Func<T, bool> condition)
    {
        if (condition(item)) return list.Append(item);
        return list;
    }

    public static List<T> Push<T>(this List<T> list, T item)
    {
        list.Add(item);
        return list;
    }

    public static string Concat<T>(this IEnumerable<T> objects, string separator)
    {
        return string.Join(separator, objects);
    }

    public static IEnumerable<(T Item, int Index)> WithIndex<T>(this IEnumerable<T> source)
    {
        return source.Select((item, index) => (item, index));
    }
}
