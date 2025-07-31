using Custom.Framework.Exceptions;
using Serilog;
using System.Runtime.CompilerServices;

namespace Custom.Framework.Helpers
{
    public class ApiTimeoutLock(
        [CallerFilePath] string callerFilePath = "",
        [CallerMemberName] string callerMemberName = "")
    {
        private readonly SemaphoreSlim semaphore = new(1, 1);
        private object _lockObj = new();
        private readonly string _callerFilePath = callerFilePath;
        private readonly string _callerMemberName = callerMemberName;
        private readonly TimeSpan _timeSpanDefault = TimeSpan.FromSeconds(30);

        public void Lock(object lockObj, Action action, TimeSpan timeout)
        {
            _lockObj = lockObj;
            Exception? lockException = null;
            bool bLockWasTaken = false;

            try
            {
                Monitor.TryEnter(_lockObj, timeout, ref bLockWasTaken);

                if (bLockWasTaken)
                    action();
                else
                    throw new ApiException($"{_callerMemberName} timeout.");
            }
            catch (Exception ex)
            {
                lockException = ex;
            }
            finally
            {
                // release the lock
                if (bLockWasTaken)
                    Monitor.Exit(_lockObj);

                if (lockException != null)
                    Log.Logger.Error("{TITLE} async lock error: {EXCEPTION}",
                        ApiHelper.LogTitle(), lockException.InnerException?.Message ?? lockException.Message);
            }
        }

        /// <summary> LockAsync by timeout parameter or by default (TimeSpan.FromSeconds(10))  </summary>
        public async Task<IDisposable> LockAsync(TimeSpan? timeout = null)
        {
            try
            {
                timeout ??= _timeSpanDefault;
                var cancellationTokenSource = new CancellationTokenSource();
                cancellationTokenSource.CancelAfter(timeout.Value);

                await semaphore.WaitAsync(cancellationTokenSource.Token);
                return new LockReleaser(semaphore);
            }
            catch (OperationCanceledException ex) // Timeout occurred
            {
                Log.Logger.Warning("{TITLE} warning: {EXCEPTION}",
                    $"{_callerFilePath}.{_callerMemberName}", ex.InnerException?.Message ?? ex.Message);
                return default!;
            }
            catch (Exception ex) // Unexpected exception
            {
                Log.Logger.Error("{TITLE} exception: {Exception}",
                    $"{_callerFilePath}.{_callerMemberName}", ex.InnerException?.Message ?? ex.Message);
                return default!;
            }
        }

        private class LockReleaser(SemaphoreSlim semaphore) : IDisposable
        {
            private readonly SemaphoreSlim semaphore = semaphore;
            private bool isDisposed = false;

            public void Dispose()
            {
                if (!isDisposed)
                {
                    semaphore.Release();
                    isDisposed = true;
                }
            }
        }

        public void Release()
        {
            semaphore.Release();
        }
    }
}
