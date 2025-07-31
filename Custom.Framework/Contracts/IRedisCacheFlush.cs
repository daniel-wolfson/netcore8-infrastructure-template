namespace Custom.Framework.Contracts
{
    public interface IRedisCacheFlush
    {
        void FlushDb();
        bool Reconnect(TimeSpan? timeout = null);
        Task<bool> ReconnectAsync(TimeSpan? timeout = null);
        string ReconnectTimeStamp { get; }
    }
}