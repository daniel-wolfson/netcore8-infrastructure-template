namespace Custom.Domain.Optima.Models.Umbraco
{
    public class AlternativeHotelsOptions
    {
        /// <summary>
        /// The requested hotel
        /// </summary>
        public string OriginalHotel { get; set; } = null!;
        public List<string>? AlternativeHotels { get; set; }
    }
}