using Newtonsoft.Json;

namespace Custom.Framework.Models.Errors
{
    /// <summary>
    /// Represents an error.
    /// </summary>
    public readonly record struct ErrorInfo

    {
        private ErrorInfo(string title, string description, ErrorType type, Dictionary<string, object>? metadata)
        {
            Code = title;
            Description = description;
            ErrorType = type;
            ErrorTypeCode = (int)type;
            Metadata = metadata;
        }

        /// <summary>
        /// Gets the unique error title.
        /// </summary>
        public string Code { get; }

        /// <summary>
        /// Gets the error description.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Gets the error type.
        /// </summary>
        [JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
        public ErrorType ErrorType { get; }

        /// <summary>
        /// Gets the numeric title of the type.
        /// </summary>
        public int ErrorTypeCode { get; }

        /// <summary>
        /// Gets the metadata.
        /// </summary>
        public Dictionary<string, object>? Metadata { get; }

        /// <summary>
        /// Creates an <see cref="ErrorInfo"/> of type <see cref="ErrorType.Failure"/> from a title and description.
        /// </summary>
        /// <param name="code">The unique error title.</param>
        /// <param name="description">The error description.</param>
        /// <param name="metadata">A dictionary which provides optional space for information.</param>
        public static ErrorInfo Failure(
            string code = "General.Failure",
            string description = "A failure has occurred.",
            Dictionary<string, object>? metadata = null) =>
                new(code, description, ErrorType.Failure, metadata);

        /// <summary>
        /// Creates an <see cref="ErrorInfo"/> of type <see cref="ErrorType.Unexpected"/> from a title and description.
        /// </summary>
        /// <param name="title">The unique error title.</param>
        /// <param name="description">The error description.</param>
        /// <param name="metadata">A dictionary which provides optional space for information.</param>
        public static ErrorInfo Unexpected(
            string title = "General.Unexpected",
            string description = "An unexpected error has occurred.",
            Dictionary<string, object>? metadata = null) =>
                new(title, description, ErrorType.Unexpected, metadata);

        /// <summary>
        /// Creates an <see cref="ErrorInfo"/> of type <see cref="ErrorType.Validation"/> from a title and description.
        /// </summary>
        /// <param name="title">The unique error title.</param>
        /// <param name="description">The error description.</param>
        /// <param name="metadata">A dictionary which provides optional space for information.</param>
        public static ErrorInfo Validation(
            string title = "General.Validation",
            string description = "A validation error has occurred.",
            string value = "",
            Dictionary<string, object>? metadata = null) =>
                new(title, description, ErrorType.Validation, metadata);

        /// <summary>
        /// Creates an <see cref="ErrorInfo"/> of type <see cref="ErrorType.Conflict"/> from a title and description.
        /// </summary>
        /// <param name="title">The unique error title.</param>
        /// <param name="description">The error description.</param>
        /// <param name="metadata">A dictionary which provides optional space for information.</param>
        public static ErrorInfo Conflict(
            string title = "General.Conflict",
            string description = "A conflict error has occurred.",
            Dictionary<string, object>? metadata = null) =>
                new(title, description, ErrorType.Conflict, metadata);

        /// <summary>
        /// Creates an <see cref="ErrorInfo"/> of type <see cref="ErrorType.NotFound"/> from a title and description.
        /// </summary>
        /// <param name="title">The unique error title.</param>
        /// <param name="description">The error description.</param>
        /// <param name="metadata">A dictionary which provides optional space for information.</param>
        public static ErrorInfo NotFound(
            string title = "General.NotFound",
            string description = "A 'Not Found' error has occurred.",
            Dictionary<string, object>? metadata = null) =>
                new(title, description, ErrorType.NotFound, metadata);

        /// <summary>
        /// Creates an <see cref="ErrorInfo"/> of type <see cref="ErrorType.Unauthorized"/> from a title and description.
        /// </summary>
        /// <param name="title">The unique error title.</param>
        /// <param name="description">The error description.</param>
        /// <param name="metadata">A dictionary which provides optional space for information.</param>
        public static ErrorInfo Unauthorized(
            string title = "General.Unauthorized",
            string description = "An 'Unauthorized' error has occurred.",
            Dictionary<string, object>? metadata = null) =>
                new(title, description, ErrorType.Unauthorized, metadata);

        /// <summary>
        /// Creates an <see cref="ErrorInfo"/> of type <see cref="ErrorType.Forbidden"/> from a title and description.
        /// </summary>
        /// <param name="title">The unique error title.</param>
        /// <param name="description">The error description.</param>
        /// <param name="metadata">A dictionary which provides optional space for information.</param>
        public static ErrorInfo Forbidden(
            string title = "General.Forbidden",
            string description = "A 'Forbidden' error has occurred.",
            Dictionary<string, object>? metadata = null) =>
            new(title, description, ErrorType.Forbidden, metadata);

        /// <summary>
        /// Creates an <see cref="ErrorInfo"/> with the given numeric <paramref name="type"/>,
        /// <paramref name="title"/>, and <paramref name="description"/>.
        /// </summary>
        /// <param name="type">An integer title which represents the type of error that occurred.</param>
        /// <param name="title">The unique error title.</param>
        /// <param name="description">The error description.</param>
        /// <param name="metadata">A dictionary which provides optional space for information.</param>
        public static ErrorInfo Custom(
            int type,
            string title,
            string description,
            Dictionary<string, object>? metadata = null) =>
                new(title, description, (ErrorType)type, metadata);

        public bool Equals(ErrorInfo other)
        {
            if (ErrorType != other.ErrorType ||
                ErrorTypeCode != other.ErrorTypeCode ||
                Code != other.Code ||
                Description != other.Description)
            {
                return false;
            }

            if (Metadata is null)
            {
                return other.Metadata is null;
            }

            return other.Metadata is not null && CompareMetadata(Metadata, other.Metadata);
        }

        public override int GetHashCode() =>
            Metadata is null ? HashCode.Combine(Code, Description, ErrorType, ErrorTypeCode) : ComposeHashCode();

        private int ComposeHashCode()
        {
#pragma warning disable SA1129 // HashCode needs to be instantiated this way
            var hashCode = new HashCode();
#pragma warning restore SA1129

            hashCode.Add(Code);
            hashCode.Add(Description);
            hashCode.Add(ErrorType);
            hashCode.Add(ErrorTypeCode);

            foreach (var keyValuePair in Metadata!)
            {
                hashCode.Add(keyValuePair.Key);
                hashCode.Add(keyValuePair.Value);
            }

            return hashCode.ToHashCode();
        }

        private static bool CompareMetadata(Dictionary<string, object> metadata, Dictionary<string, object> otherMetadata)
        {
            if (ReferenceEquals(metadata, otherMetadata))
            {
                return true;
            }

            if (metadata.Count != otherMetadata.Count)
            {
                return false;
            }

            foreach (var keyValuePair in metadata)
            {
                if (!otherMetadata.TryGetValue(keyValuePair.Key, out var otherValue) ||
                    !keyValuePair.Value.Equals(otherValue))
                {
                    return false;
                }
            }

            return true;
        }

        public object ToJson() => new { Code, Description };

        public object ToJsonValidationResult() => new { ValidationName = Code, ValidationResult = Description };
    }
}
