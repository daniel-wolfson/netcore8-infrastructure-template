namespace Custom.Framework.TestFactory.Core
{
    public static class TestHostExtentions
    {
        /// <summary> MakeRequests special TaskPrefix guid of first PadLeft(8) characters </summary>
        public static Guid CteateSpecialGuid(this Guid guid, string prefix)
        {
            var result = new Guid(prefix?.PadLeft(8, '0') + guid.ToString("n")[8..]);
            return result;
        }

        /// <summary> Wait to completion async operations </summary>
        public static async Task<TResult> WaitCompletionSource<TResult>(
            this TaskCompletionSource<TResult> taskCompletionSource,
            int timeoutFromSeconds = 10, CancellationToken cancellationToken = default)
        {
            var timeout = TimeSpan.FromSeconds(timeoutFromSeconds);
            using (var timeoutCancellation = new CancellationTokenSource())
            using (var combinedCancellation = CancellationTokenSource.CreateLinkedTokenSource(timeoutCancellation.Token, cancellationToken))
            {
                var operationTask = taskCompletionSource.Task;
                var timeoutTask = Task.Delay(timeout, combinedCancellation.Token);
                var completedTask = await Task.WhenAny(operationTask, timeoutTask);

                timeoutCancellation.Cancel();
                if (completedTask == operationTask)
                {
                    return await operationTask;
                }
                else
                {
                    taskCompletionSource.SetException(new TimeoutException("The operation has timed out."));
                }
#pragma warning disable CS8603 // Possible null reference return.
                return default;
#pragma warning restore CS8603 // Possible null reference return.
            }
        }

        /// <summary> Wait to completion async operations </summary>
        public static async Task WaitAllCompletionSources<TResult>(
                this List<TaskCompletionSource<TResult>> taskSources,
                int timeFromSeconds = 20)
        {
            if (taskSources == null) return;

            var tasks = taskSources.Select(x => x.Task).ToArray();

            using (var cancellationTokenSource = new CancellationTokenSource())
            using (var timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeFromSeconds)))
            using (var cancellationMonitorTask = Task.Delay(-1, cancellationTokenSource.Token))
            {
                Task completedTask = await Task.WhenAny(Task.WhenAll(tasks), timeoutTask, cancellationMonitorTask);
                if (completedTask == timeoutTask)
                {
                    throw new TimeoutException("The operation has timed out.");
                }
                if (completedTask == cancellationMonitorTask)
                {
                    throw new OperationCanceledException();
                }
                await completedTask;
            }
        }

    }
}