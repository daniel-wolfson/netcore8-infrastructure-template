using Custom.Framework.Models;
using Custom.Framework.Exceptions;
using Serilog;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Custom.Framework.Helpers
{
    public class AsyncTimedLock2([CallerMemberName] string namedMember = "")
    {
        private readonly SemaphoreSlim _semaphoreSlim = new(1, 1);
        private readonly string _namedMember = namedMember;
        private object _lockObj = new();

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
                    throw new ApiException($"{_namedMember} timeout.");
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

        public async Task<Locker> LockAsync(TimeSpan timeout)
        {
            Exception? lockException = null;
            try
            {
                if (await _semaphoreSlim.WaitAsync(timeout))
                {
                    return new Locker(_semaphoreSlim);
                }
                throw new ApiException(ServiceStatus.Conflict, $"{_namedMember} error.");
            }
            catch (Exception ex)
            {
                lockException = ex;
                return new Locker(_semaphoreSlim);
            }
            finally
            {
                // Release the _semaphoreSlim when done with the resource
                _semaphoreSlim.Release();
                Debug.WriteLine($"Task {_namedMember} has released the resource.");

                if (lockException != null)
                    Log.Logger.Error("{TITLE} async lock error: {EXCEPTION}",
                        ApiHelper.LogTitle(), lockException.InnerException?.Message ?? lockException.Message);
            }
        }

        public readonly struct Locker : IDisposable
        {
            private readonly SemaphoreSlim _semaphoreSlim;

            public Locker(SemaphoreSlim toRelease)
            {
                _semaphoreSlim = toRelease;
            }
            public void Dispose()
            {
                _semaphoreSlim.Release();
            }
        }
    }
}
