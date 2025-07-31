using FluentValidation;

namespace Custom.Framework.Models.Main
{
    public class TranslatesPackageRequestValidator : AbstractValidator<TranslationsPackageRequest>
    {
        /// <summary> ctor  </summary>
        public TranslatesPackageRequestValidator()
        {
            RuleFor(req => req.LanguageID)
                .GreaterThan(0)
                .WithMessage(req => $"{nameof(req.LanguageID)} must be greater than 0");
            RuleFor(req => req.CustomerID)
                .GreaterThan(0)
                .WithMessage(req => $"{nameof(req.CustomerID)} must be greater than 0");
            RuleFor(req => req.Password)
                .NotEmpty()
                .WithMessage(req => $"{nameof(req.Password)} must be not empty");
            RuleFor(req => req.UserName)
                .NotEmpty()
                .WithMessage(req => $"{nameof(req.UserName)} must be not empty");
        }
    }
}