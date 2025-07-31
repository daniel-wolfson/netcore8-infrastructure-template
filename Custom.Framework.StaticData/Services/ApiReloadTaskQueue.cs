using Custom.Framework.Configuration.Models;
using Custom.Framework.StaticData.Contracts;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace Custom.Framework.StaticData.Services
{
    public class ApiReloadTaskQueue : IApiReloadTaskQueue
    {
        // Holds the current count of tasks in the queue.
        private readonly SemaphoreSlim _signal = new(0);

        private readonly ConcurrentQueue<ReloadTaskQueueItem> _items = new();

        public bool IsEmpty => _items.IsEmpty;

        public void EnqueueTask(SettingKeys settingKey, Func<IServiceScopeFactory, CancellationToken, Task> task)
        {
            ArgumentNullException.ThrowIfNull(task);

            var queueItem = new ReloadTaskQueueItem { SettingKey = settingKey, GetReloadTask = task };

            _items.Enqueue(queueItem);

            _signal.Release();
        }

        public async Task<ReloadTaskQueueItem?> DequeueAsync(CancellationToken cancellationToken)
        {
            // Wait for task to become available
            await _signal.WaitAsync(cancellationToken);

            _items.TryDequeue(out var task);

            return task;
        }

        public bool Contains(SettingKeys settingKey)
        {
            return _items.Select(x => x.SettingKey).Contains(settingKey);
        }
    }
}
