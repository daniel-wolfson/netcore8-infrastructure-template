using FluentValidation;
using Custom.Domain.Optima.Models;

namespace Custom.Framework.Validation;

public class OptimaBoardBaseValidator : AbstractValidator<BoardBase>
{
    public OptimaBoardBaseValidator()
    {
        RuleFor(x => x.BoardBaseCode)
            .Empty()
            .WithMessage("The value must be not null or empty");
    }

    public bool IsPlanCodeValid(BoardBase boardBase)
    {
        var validationResult = this.Validate(boardBase, options => options.IncludeProperties("BoardBaseCode"));
        return validationResult.IsValid;
    }
}
