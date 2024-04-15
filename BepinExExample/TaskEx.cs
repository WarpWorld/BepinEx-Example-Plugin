using System.Diagnostics;

namespace CrowdControl;

public static class TaskEx
{
    /// <summary>
    /// Calls ConfigureAwait(false) on a task and logs any errors.
    /// </summary>
    /// <param name="task">The task to forget.</param>
    [DebuggerStepThrough]
    public static async void Forget(this Task task)
    {
        try { await task.ConfigureAwait(false); }
        catch (Exception ex) { TestMod.LogError(ex); }
    }

    public static Task<R> Then<R, T>(this Task<T> self, Func<T, R> f) =>
        self.ContinueWith(t => f(t.Result), TaskContinuationOptions.OnlyOnRanToCompletion);

    public static Task Then(this Task self, Action<Task> f) =>
        self.ContinueWith(f, TaskContinuationOptions.OnlyOnRanToCompletion);

    public static Task Catch(this Task self, Action<Exception> f) =>
        self.ContinueWith(t => { if (t.IsFaulted) { f(t.Exception); } });

    public static void Done(this Task self) =>
        self.ContinueWith(_ => { }).Wait(); // Handles Catch creating cancelled tasks.
}