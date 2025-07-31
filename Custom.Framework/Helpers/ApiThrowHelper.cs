using Custom.Framework.Exceptions;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Custom.Framework.Helpers
{
    public static class ApiThrowHelper
    {
        public static void ThrowIfNull([NotNull] object? argument, [CallerArgumentExpression("argument")] string? paramName = null)
        {
            if (argument == null)
                Throw(paramName ?? "Parameter is null");
        }
        public static void ThrowIfNull([NotNull] object? argument, Exception ex)
        {
            if (argument == null)
                Throw("Parameter is null", ex);
        }

        public static void ThrowIf(Func<bool> condition, string message)
        {
            if (condition())
            {
                throw new ApiException(message);
            }
        }

        public static void ThrowIf(Func<Task<bool>> condition, string message)
        {
            if (condition().GetAwaiter().GetResult())
            {
                throw new ApiException(message);
            }
        }

        public static void Throw(string message, Exception exception)
        {
            throw new ApiException(message, exception);
        }

        [DoesNotReturn]
        public static void Throw(string paramName)
        {
            throw new ArgumentNullException(paramName);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [return: NotNull]
        public static string IfNullOrWhitespace([NotNull] string? argument,
            [CallerArgumentExpression("argument")] string paramName = "")
        {
            if (string.IsNullOrWhiteSpace(argument))
            {
                if (argument == null)
                {
                    throw new ArgumentNullException(paramName);
                }

                throw new ArgumentException(paramName, "Argument is whitespace");
            }

            return argument;
        }
    }
}