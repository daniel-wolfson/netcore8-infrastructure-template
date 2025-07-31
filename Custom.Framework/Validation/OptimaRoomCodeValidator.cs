using FluentValidation;
using FluentValidation.Validators;

namespace Custom.Framework.Validation;

public class OptimaRoomCodeValidator<T> : PropertyValidator<T, string>, IRoomCodeValidator
{
    public override string Name => "PlanCode";

    public override bool IsValid(ValidationContext<T> context, string value)
    {
        return !string.IsNullOrEmpty(value);
    }

    protected override string GetDefaultMessageTemplate(string errorCode)
    {
        return $"ErrorInfo {errorCode}: {Name} must be not empty";
    }
}

public interface IRoomCodeValidator : IPropertyValidator
{
}

