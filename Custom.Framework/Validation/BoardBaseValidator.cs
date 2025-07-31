using FluentValidation;
using Custom.Domain.Optima.Models;

namespace Custom.Framework.Validation;

public class BoardBaseValidator : AbstractValidator<BoardBase>
{
    public BoardBaseValidator()
    {
        RuleFor(x => x.BoardBaseCode)
            .NotEmpty().WithMessage("BoardBaseCode is required.")
            .WithMessage("BoardBaseCode must be not empty");
    }
}
