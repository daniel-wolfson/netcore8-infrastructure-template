using Custom.Domain.Optima.Models.Enums;

namespace Custom.Framework.Helpers
{
    public static class OptimaSalesHelper
    {
        public const int OptimaSalesPrefix = 1_000_000;
        public const int OptimaSalesEnPrefix = 2_000_000;

        /// <summary>
        /// Add 1M for Optima Hebrew sales and 2M for Optima English sales
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="saleId"></param>
        /// <returns></returns>
        public static int GetPrefixedSaleId(EChannel channel, int saleId)
        {
            if (saleId == default)
                return saleId;

            return channel == EChannel.WHENIS
                            ? saleId + OptimaSalesPrefix
                            : saleId + OptimaSalesEnPrefix;
        }
        /// <summary>
        /// Get Optima original sale Id without prefix manipulation
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="prefixedSaleId"></param>
        /// <returns></returns>
        public static int GetOriginalSaleId(EChannel channel, int prefixedSaleId)
        {
            if (prefixedSaleId == default)
                return prefixedSaleId;

            return channel == EChannel.WHENIS
                         ? prefixedSaleId - OptimaSalesPrefix
                         : prefixedSaleId - OptimaSalesEnPrefix;
        }
    }
}
