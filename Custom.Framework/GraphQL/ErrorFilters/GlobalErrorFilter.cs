using HotChocolate;

namespace Custom.Framework.GraphQL.ErrorFilters
{
    /// <summary>
    /// Global error filter for GraphQL errors
    /// </summary>
    public class GlobalErrorFilter : IErrorFilter
    {
        private readonly ILogger _logger;

        public GlobalErrorFilter(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public IError OnError(IError error)
        {
            // Log all errors
            _logger.Error(error.Exception, "GraphQL Error: {Message}", error.Message);

            return error.Exception switch
            {
                ArgumentException => error
                    .WithCode("INVALID_ARGUMENT")
                    .WithMessage("Invalid argument provided"),
                
                InvalidOperationException => error
                    .WithCode("INVALID_OPERATION")
                    .WithMessage(error.Exception.Message),
                
                UnauthorizedAccessException => error
                    .WithCode("UNAUTHORIZED")
                    .WithMessage("Unauthorized access"),
                
                System.Collections.Generic.KeyNotFoundException => error
                    .WithCode("NOT_FOUND")
                    .WithMessage("Resource not found"),
                
                _ => error
                    .WithCode("INTERNAL_ERROR")
                    .WithMessage("An internal error occurred")
            };
        }
    }
}
