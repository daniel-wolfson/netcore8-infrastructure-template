namespace Custom.Domain.Optima.Models.Enums
{
    public enum EErrorCode
    {
        NoErrors,
        Failed2GetUmbracoSettings,
        Failed2GetNopSettings,
        Failed2GetDiscounts,
        Failed2GetPriceListCodes,
        InvalidSearchDates,
        InvalidDateRangeForSite,
        GeneralError,
        OperaFailure,
        ArkiaFailure,
        NoValidHotelCodes,
        Failed2GetDynamicFlightSettings,
        FailedToGetFlightPackage,
        UnavailableIncludedFlights,
        NoFlightResults,
        InvalidOccupancy,
        /// <summary>
        /// For uweb only
        /// </summary>
        AgentIsBlocked,
        MaxAllowedNightsExceeded
    }
}
