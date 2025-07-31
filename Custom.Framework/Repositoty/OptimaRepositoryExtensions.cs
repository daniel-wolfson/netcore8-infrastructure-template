using Custom.Domain.Optima.Models.Base;
using Custom.Framework.Models.Base;

namespace Custom.Framework.Repositoty
{
    /// <summary>
    /// POC
    /// </summary>
    public static class OptimaRepositoryExtensions
    {
        public static OptimaResult<List<TTarget>> CastResult<TSource, TTarget>(
            this OptimaResult<List<TSource>> sourceResult)
            where TSource : OptimaData
            where TTarget : class
        {
            var targetList = sourceResult.Data?
                .Select(item => item as TTarget).ToList() ?? new List<TTarget>();

            return new OptimaResult<List<TTarget>>(targetList, sourceResult.Message.Text, sourceResult.IsSuccess);
        }
    }
}