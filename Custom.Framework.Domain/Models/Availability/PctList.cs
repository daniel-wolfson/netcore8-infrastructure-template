namespace Custom.Domain.Optima.Models.Availability
{
    public class PctList
    {
        public int HotelID { get; set; }
        public int LanguageID { get; set; }
        public string PriceCode { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Picture1Url { get; set; }
        public string Picture2Url { get; set; }
        public GenericParameters genericParameters { get; set; }
    }
}