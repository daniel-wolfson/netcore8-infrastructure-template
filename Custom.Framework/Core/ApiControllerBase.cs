using Custom.Framework.Contracts;
using Custom.Framework.Exceptions;
using Custom.Framework.Models;
using Custom.Framework.Models.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;
using System.Runtime.CompilerServices;

namespace Custom.Framework.Core
{
    /// <summary> BaseAction for all actions </summary>
    public class ApiControllerBase : ControllerBase
    {
        private ILogger? _logger = default;
        private LoggingLevelSwitch? _levelSwitch = default;
        protected ILogger Logger => _logger ??= GetService<ILogger>();
        protected LoggingLevelSwitch LoggingLevelSwitch => _levelSwitch ??= GetService<LoggingLevelSwitch>();

        [NonAction]
        protected T GetService<T>(object? serviceKey = null)
        {
            object? service = default;

            var serviceProvider = HttpContext?.RequestServices;
            if (serviceProvider != null)
            {
                service = (serviceKey != null)
                    ? serviceProvider.GetKeyedService<T>(serviceKey)!
                    : serviceProvider.GetService(typeof(T));
            }

            return service != null
                ? (T)service
                : throw new ApiException(ServiceStatus.FatalError, $"{typeof(T).Name} not registered");
        }

        [NonAction]
        protected IActionResult Error<T>([ActionResultObjectValue] IServiceResult<T>? serviceResult)
        {
            serviceResult = serviceResult ?? ServiceResult<T>.Error();
            return serviceResult.Status switch
            {
                204 => new NoContentResult(),                           // 204 No Content
                >= 200 and < 300 => new OkObjectResult(serviceResult),  // 2xx Success
                400 => new BadRequestObjectResult(serviceResult),       // 400 Bad Request
                404 => new NotFoundObjectResult(serviceResult),         // 404 Not Found
                401 => new UnauthorizedObjectResult(serviceResult),     // 401 Unauthorized
                403 => new ForbidResult(),                              // 403 Forbidden
                409 => new ConflictObjectResult(serviceResult),         // 409 Conflict
                _ => new ObjectResult(serviceResult) { StatusCode = serviceResult.Status }
            };
        }

        [NonAction]
        protected IActionResult Error([ActionResultObjectValue] object? value,
            string? message = null,
            int status = 500,
            [CallerFilePath] string callerFilePath = "",
            [CallerMemberName] string callerMemberName = "")
        {
            message = $"{Path.GetFileNameWithoutExtension(callerFilePath)}.{callerMemberName}{message ?? " error"}";

            return new ObjectResult(message) { StatusCode = status };
        }

        protected ActionResult Error(List<ErrorInfo> errors,
            [CallerFilePath] string callerFilePath = "",
            [CallerMemberName] string callerMemberName = "")
        {
            if (errors.Count is 0)
            {
                return Problem();
            }

            if (errors.All(error => error.ErrorType == ErrorType.Validation))
            {
                return ValidationError(errors);
            }

            return Error(errors[0]);
        }

        protected ObjectResult Error(ErrorInfo error)
        {
            var statusCode = error.ErrorType switch
            {
                ErrorType.Conflict => StatusCodes.Status409Conflict,
                ErrorType.Validation => StatusCodes.Status400BadRequest,
                ErrorType.NotFound => StatusCodes.Status404NotFound,
                ErrorType.Unauthorized => StatusCodes.Status403Forbidden,
                _ => StatusCodes.Status500InternalServerError,
            };

            return Problem(statusCode: statusCode, title: error.Description);
        }

        protected ActionResult ValidationError(List<ErrorInfo> errors)
        {
            var modelStateDictionary = new ModelStateDictionary();

            errors.ForEach(error => modelStateDictionary.AddModelError(error.Code, error.Description));

            return ValidationProblem(modelStateDictionary);
        }
    }
}