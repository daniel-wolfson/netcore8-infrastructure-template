using Custom.Framework.Models.Base;
using Microsoft.Extensions.Logging;

namespace Custom.Framework.StaticData;

public class ReloadCacheTask(ILogger<ReloadCacheTask> logger, 
                             StaticDataCollection<EntityData> staticDataCollection) : IReloadCacheTask
{
    private readonly ILogger<ReloadCacheTask> _logger = logger;
    private readonly StaticDataCollection<EntityData> _staticDataCollection = staticDataCollection;

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                await _staticDataCollection.ReloadAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ReloadCacheTask error");
        }
    }
}