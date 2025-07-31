using Custom.Framework.Models.Errors;

namespace Custom.Framework.Services
{
    public interface IApiWorkerBase
    {
        List<ErrorInfo>? GetErrors();

        public ErrorInfo? GetLatestError();
    }
}