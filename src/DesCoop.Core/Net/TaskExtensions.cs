namespace DesCoop.Net;

static class TaskExtensions
{
    /// <summary>
    /// For pipe copies we stop waiting on once the other direction ends: their "connection closed"
    /// errors are expected, so mark them observed instead of letting the finalizer report them.
    /// </summary>
    public static Task Observe(this Task t)
    {
        t.ContinueWith(x => _ = x.Exception, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
        return t;
    }
}
