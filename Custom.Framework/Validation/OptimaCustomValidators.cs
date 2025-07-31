using FluentValidation;
using Custom.Domain.Optima.Models.Availability;

namespace Custom.Framework.Validation
{
    public static class OptimaCustomValidators
    {
        public static IRuleBuilderOptions<T, TElement> PlanCodeMustBe_ROorHBorBBorAI<T, TElement>(
            this IRuleBuilder<T, TElement> ruleBuilder, int num)
            where TElement : PackagesList
        {
            return ruleBuilder.Must(x =>
            {
                if (string.IsNullOrEmpty(x.PlanCode))
                    return false;

                string pc = x.PlanCode.Substring(0, 2);
                return pc switch
                {
                    "RO" or "HB" or "BB" or "AI" => true,
                    _ => false,
                };
            })
                .WithMessage("'{PropertyName}' must contain fewer than {MaxElements} items.");
        }
    }
}
