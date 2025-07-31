namespace Custom.Framework.Models.Errors
{
    /// <summary>
    /// ErrorInfo types.
    /// </summary>
    public enum ErrorType
    {
        Failure,
        Unexpected,
        Validation,
        Conflict,
        NotFound,
        Unauthorized,
        Forbidden,
    }
}
