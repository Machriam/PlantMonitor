namespace Plantmonitor.Shared.Extensions;

public static class ObjectExtensions
{
    public static TOut Pipe<TIn, TOut>(this TIn param, Func<TIn, TOut> pipe)
    {
        return pipe(param);
    }

    public static async void RunInBackground(this Task task, Action<Exception> exceptionHandler)
    {
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            exceptionHandler(ex);
        }
    }

    public static async ValueTask<string> Try(this ValueTask obj)
    {
        try
        {
            await obj;
            return "";
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    public static bool Try(this Action action, Action<Exception> errorHandler)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception ex)
        {
            errorHandler(ex);
            return false;
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
