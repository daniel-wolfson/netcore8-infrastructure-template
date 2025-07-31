using Custom.Framework.Configuration.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Custom.Framework.StaticData.Services
{
    public class ReloadTaskQueueItem
    {
        public SettingKeys SettingKey { get; set; }
        public Func<IServiceScopeFactory, CancellationToken, Task> GetReloadTask { get; set; } = default!;
    }
}
