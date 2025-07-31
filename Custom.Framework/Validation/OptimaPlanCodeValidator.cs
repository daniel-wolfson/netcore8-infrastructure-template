using FluentValidation;
using FluentValidation.Validators;

namespace Custom.Framework.Validation;


public class OptimaPlanCodeValidator<T> : PropertyValidator<T, string>, IPlanCodeValidator
{
    public override string Name => "PlanCode";
    private readonly string[] _planeCodes = ["RO", "HB", "BB", "AI"];

    public override bool IsValid(ValidationContext<T> context, string value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        string? name = value?.Substring(0, 2);
        return _planeCodes.Contains(name);
    }

    protected override string GetDefaultMessageTemplate(string errorCode)
    {
        var planeCodes = string.Join(",", _planeCodes);
        return $"ErrorInfo {errorCode}: {Name} must be one from {planeCodes}";
    }
}

public interface IPlanCodeValidator : IPropertyValidator
{
}