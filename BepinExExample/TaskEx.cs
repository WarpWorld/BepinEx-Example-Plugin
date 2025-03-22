using System.Diagnostics;

namespace CrowdControl;

// ReSharper disable once InconsistentNaming
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
        catch (Exception ex) { CrowdControlMod.Instance.Logger.LogError(ex); }
    }

    /// <summary>
    /// Calls ConfigureAwait(false) on a task and optionally logs any errors.
    /// </summary>
    /// <param name="task">The task to forget.</param>
    /// <param name="silent">True to silently suppress errors, otherwise false.</param>
    [DebuggerStepThrough]
    public static async void Forget(this Task task, bool silent)
    {
        try { await task.ConfigureAwait(false); }
        catch (Exception ex) { if (!silent) CrowdControlMod.Instance.Logger.LogError(ex); }
    }
}