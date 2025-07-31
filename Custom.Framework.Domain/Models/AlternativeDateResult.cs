using Custom.Domain.Optima.Models.Enums;

namespace Custom.Domain.Optima.Models
{
    public class AlternativeDateResult
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public ESearchResultType AlternativeDateGroup { get; set; }
        /// <summary>
        /// Data between 1-3 (1 = top priority). Defines which alternative date option will be shown to the user, in every alternative dates group
        /// (groups are: OneWeekEarlier, OneWeekLater, OneDayEarlier)
        /// </summary>
        public int Priority { get; set; }
    }
}