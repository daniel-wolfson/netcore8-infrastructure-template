using Custom.Framework.Configuration;
using Custom.Framework.Contracts;
using Custom.Framework.Models;
using Custom.Framework.Models.Errors;
using Serilog;
using Serilog.Events;

namespace Custom.Framework.Services
{
    /// <summary> Base class for all service's instances </summary>
    public abstract class ApiWorkerBase()
    {
        // Static property to hold the type of settings
        public static Type? AppSettingsType { get; set; } = typeof(ApiSettings);

        #region props

        protected Stack<ErrorInfo>? Errors { get; set; }

        #endregion props

        #region service results

        /// <summary> ServiceResult for Ok result </summary>
        public IServiceResult<T> Ok<T>(T? data = default, string message = "Success", int status = 200)
            => Result(LogEventLevel.Information, data, status, message);

        /// <summary> ServiceResult for NoContent result </summary>
        public IServiceResult<T> NotFound<T>(string message = "Not Found")
           => Result<T>(LogEventLevel.Warning, default, 404, message);

        /// <summary> ServiceResult for NoContent result </summary>
        public IServiceResult<T> NoContent<T>(string message = "No Content")
           => Result<T>(LogEventLevel.Warning, default, 204, message);

        public IServiceResult<T> NoData<T>(T? data = default, string message = "No Data")
           => Result(LogEventLevel.Error, data, 204, message);

        /// <summary> ServiceResult for BadRequest result </summary>
        public IServiceResult<T> BadRequest<T>(string message = "Bad Request")
            => Result<T>(LogEventLevel.Error, default, 400, message);

        /// <summary> ServiceResult for Warning result </summary>
        public IServiceResult<T> Warning<T>(string message = "Warning")
            => Result<T>(LogEventLevel.Warning, default, 199, message);

        /// <summary> ServiceResult for error result </summary>
        public IServiceResult<T> Error<T>(string message, int status = 500)
            => Result<T>(LogEventLevel.Error, default, status, message);

        public IServiceResult<T> Error<T>(T? data = default, string message = "error")
            => Result(LogEventLevel.Error, data, 400, message);

        private IServiceResult<T> Result<T>(LogEventLevel logEvent, T? data, int status, string message = "", Dictionary<string, object>? genericParameters = default)
        {
            var serviceResult = new ServiceResult<T>(message, data, status)
            {
                GenericParameters = genericParameters
            };

            return serviceResult;
        }

        private IServiceResult<T> Result<T>(LogEvent logEvent, T data, int status, string message = "")
        {
            Log.Logger.Write(logEvent);

            return new ServiceResult<T>(message, data, status);
        }

        #endregion service results

        #region public  methods

        protected void AddError(ErrorInfo error)
        {
            // Initialize the stack if it hasn't been already
            Errors ??= new Stack<ErrorInfo>();

            // Add error to the stack
            Errors.Push(error);
        }

        public ErrorInfo? GetLatestError()
        {
            // Ensure there are errors in the stack before trying to pop
            return Errors?.Count > 0 ? Errors.Pop() : null;
        }
        public List<ErrorInfo>? GetErrors()
        {
            List<ErrorInfo> errors = [];
            // Ensure there are errors in the stack before trying to pop
            while (Errors?.Count > 0)
            {
                errors.Add(Errors.Pop());
            }
            return errors;
        }

        #endregion public methods
    }
}