namespace Plantmonitor.Shared.Extensions;

public static class TaskExtensions
{
    public static async Task<(bool Success, T Result)> TryAsyncTask<T>(this Task<T> function)
    {
        try
        {
            var result = await function;
            return (true, result);
        }
        catch (Exception)
        {
            return (false, default!);
        }
    }
}