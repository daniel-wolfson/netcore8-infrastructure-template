using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

public static class ApiTemplateHelper
{
    // Create replacement dictionary from target
    public static Dictionary<string, string> replacements = new()
    {
        { "Name", "\"target.Name\"" },
        { "Environment", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development" },
        { "ServiceShortName", System.Reflection.Assembly.GetExecutingAssembly().GetName().Name ?? "Unknown" },
        { "InstanceIndex", "1" } // You might want to get this from configuration or environment
    };

    /// <summary>
    /// Replaces template placeholders in a field value with corresponding values from the provided object
    /// </summary>
    /// <param name="templateValue">The template string containing placeholders like "{Name}-group"</param>
    /// <param name="sourceObject">JsonElement containing the replacement values</param>
    /// <returns>The string with placeholders replaced</returns>
    public static string ReplaceTemplate(string templateValue, JsonElement sourceObject)
    {
        if (string.IsNullOrEmpty(templateValue))
            return templateValue;

        // Pattern to match placeholders like {Name}, {Environment}, etc.
        var pattern = @"\{([^}]+)\}";

        return Regex.Replace(templateValue, pattern, match =>
        {
            var propertyName = match.Groups[1].Value;

            // Try to get the property value from the source object
            if (sourceObject.TryGetProperty(propertyName, out var property))
            {
                return property.GetString() ?? string.Empty;
            }

            // If property not found, return the original placeholder
            return match.Value;
        }, RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Replaces template placeholders using a dictionary of key-value pairs
    /// </summary>
    /// <param name="templateValue">The template string containing placeholders</param>
    /// <param name="replacements">Dictionary containing replacement values</param>
    /// <returns>The string with placeholders replaced</returns>
    public static string ReplaceTemplate(string templateValue)
    {
        if (string.IsNullOrEmpty(templateValue) || replacements == null)
            return templateValue;

        var pattern = @"\{([^}]+)\}";

        return Regex.Replace(templateValue, pattern, match =>
        {
            var propertyName = match.Groups[1].Value;

            // Try to get the replacement value (case-insensitive)
            var replacement = replacements.FirstOrDefault(kvp =>
                string.Equals(kvp.Key, propertyName, StringComparison.OrdinalIgnoreCase));

            return replacement.Key != null ? replacement.Value : match.Value;
        }, RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Replaces template placeholders using an object's properties via reflection
    /// </summary>
    /// <param name="templateValue">The template string containing placeholders</param>
    /// <param name="sourceObject">Object containing the replacement values</param>
    /// <returns>The string with placeholders replaced</returns>
    public static string ReplaceTemplate<T>(string templateValue, T sourceObject) where T : class
    {
        if (string.IsNullOrEmpty(templateValue) || sourceObject == null)
            return templateValue;

        var pattern = @"\{([^}]+)\}";
        var objectType = typeof(T);

        return Regex.Replace(templateValue, pattern, match =>
        {
            var propertyName = match.Groups[1].Value;

            // Try to get the property value via reflection
            var property = objectType.GetProperty(propertyName,
                BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);

            if (property != null)
            {
                var value = property.GetValue(sourceObject);
                return value?.ToString() ?? string.Empty;
            }

            // If property not found, return the original placeholder
            return match.Value;
        }, RegexOptions.IgnoreCase);
    }

    public static string ReplaceTemplate<T>(this T target, string fieldName)
    {
        var dicSettings = target?.GetType()
            .GetProperties()
            .Where(p => p.CanRead && p.CanWrite)
            .ToDictionary(p => p.Name, p => p.GetValue(target)?.ToString() ?? string.Empty);

        var field = string.Empty;
        if (dicSettings != null)
        {
            if (!dicSettings.TryGetValue(fieldName, out field) || string.IsNullOrEmpty(field))
                return string.Empty;

            var template = dicSettings?[fieldName];
            if (!string.IsNullOrEmpty(template) && field.Contains(template, StringComparison.Ordinal))
            {
                return field.Replace(template, dicSettings?[fieldName], StringComparison.Ordinal);
            }
        }
        return field;
    }
}