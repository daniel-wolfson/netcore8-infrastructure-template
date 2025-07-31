namespace Custom.Framework.Contracts
{
    public interface IRetryPolicyBuilder
    {
        T? Run<T>(Func<T> onExecute, string? correlationId = null);
        Task<T?> RunAsync<T>(Func<Task<T>> onExecute, string? correlationId = null);
    }
}