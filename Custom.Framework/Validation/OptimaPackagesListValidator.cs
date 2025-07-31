using FluentValidation;
using Custom.Domain.Optima.Models.Availability;

namespace Custom.Framework.Validation;

public class OptimaPackagesListValidator : AbstractValidator<PackagesList>, IOptimaPackagesListValidator
{
    public OptimaPackagesListValidator()
    {
        RuleFor(x => x.OccupancyCode)
            .GreaterThan(0)
            .WithMessage("PlanCode must be geat then 0.");

        RuleFor(x => x.CurrencyCodeSource)
            .NotEmpty()
            .WithMessage("CurrencyCodeSource must not be empty.");

        // verify two first symbols of planCode
        RuleFor(x => x.PlanCode)
            .Must(x => IsPlanCodeValid(x))
            .WithMessage("Name (two first symbols) must be contained in enabled list: [HB, BB, RO, AI] ");

        RuleFor(x => x.RoomCode)
            .NotEmpty()
            .WithMessage("RoomCode must be contained in Umbraco RoomFilter");
    }

    public bool IsPlanCodeValid(PackagesList packagesList)
    {
        var validationResult = this.Validate(packagesList, 
            options => options.IncludeProperties(nameof(packagesList.PlanCode)));
        return validationResult.IsValid;
    }

    public bool IsRoomCodeValid(PackagesList packagesList)
    {
        var validationResult = this.Validate(packagesList, 
            options => options.IncludeProperties(nameof(packagesList.RoomCode)));
        return validationResult.IsValid;
    }

    public bool IsPlanCodeValid(string planCode)
    {
        string[] _planeCodes = ["RO", "HB", "BB", "AI"];

        if (string.IsNullOrEmpty(planCode))
            return false;

        var boardBase = planCode.Substring(0, 2);
        return _planeCodes.Contains(boardBase);
    }
}

public interface IOptimaPackagesListValidator
{
    bool IsPlanCodeValid(string planCode);
    bool IsPlanCodeValid(PackagesList pl);
    bool IsRoomCodeValid(PackagesList packagesList);
}
