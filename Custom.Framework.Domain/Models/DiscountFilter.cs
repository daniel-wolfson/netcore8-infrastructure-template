namespace Custom.Domain.Optima.Models
{
    public class DiscountFilter
    {
        public int FilterCategoryId { get; set; }
        public int FilterCategoryPriority { get; set; }
        public string FilterCategoryName { get; set; }
        public bool FilterCategoryIsShow { get; set; }
        public bool FilterCategoryIsDisabled { get; set; }
    }
}