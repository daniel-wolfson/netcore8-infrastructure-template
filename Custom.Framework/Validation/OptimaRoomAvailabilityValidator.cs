using FluentValidation;
using Custom.Domain.Optima.Models.Availability;

namespace Custom.Framework.Validation;

public class OptimaRoomAvailabilityValidator : AbstractValidator<RoomPriceList>
{
    public OptimaRoomAvailabilityValidator()
    {
        RuleFor(x => x.PlanCode)
                 .SetValidator(new OptimaPlanCodeValidator<RoomPriceList>());

        RuleFor(x => x.RoomCategory)
                 .SetValidator(new OptimaRoomCodeValidator<RoomPriceList>());
    }

    public bool IsPlanCodeValid(RoomPriceList roomPriceList)
    {
        var validationResult = this.Validate(roomPriceList, options => options.IncludeProperties("PlanCode"));
        return validationResult.IsValid;
    }

    public bool IsRoomCodeValid(RoomPriceList roomPriceList)
    {
        var validationResult = this.Validate(roomPriceList, options => options.IncludeProperties("RoomCategory"));
        return validationResult.IsValid;
    }

}

