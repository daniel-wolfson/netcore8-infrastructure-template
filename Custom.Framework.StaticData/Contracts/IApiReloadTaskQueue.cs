using Custom.Framework.Configuration.Models;
using Custom.Framework.StaticData.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Custom.Framework.StaticData.Contracts
{
    public interface IApiReloadTaskQueue
    {
        // Enqueues the given task.
        void EnqueueTask(SettingKeys settingKey, Func<IServiceScopeFactory, CancellationToken, Task> task);

        // Dequeues and returns one task. This method blocks until a task becomes available.
        Task<ReloadTaskQueueItem?> DequeueAsync(CancellationToken cancellationToken);

        bool Contains(SettingKeys settingKey);

        public bool IsEmpty { get; }
    }
}
