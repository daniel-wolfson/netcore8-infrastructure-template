namespace Custom.Framework.StaticData
{
    public interface IReloadCacheTask
    {
        Task ExecuteAsync(CancellationToken stoppingToken);
    }
}