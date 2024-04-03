namespace Plantmonitor.Shared.Extensions;

public static class ObjectExtensions
{
    public static T? Try<T, V>(this V obj, Func<V, T> function, out string? error)
    {
        error = null;
        try
        {
            return function(obj);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return default;
        }
    }

    public static async Task<(T? Result, string Error)> Try<T>(this Task<T> obj)
    {
        try
        {
            return (await obj, "");
        }
        catch (Exception ex)
        {
            return (default, ex.Message);
        }
    }
}