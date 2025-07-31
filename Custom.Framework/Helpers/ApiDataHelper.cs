namespace Custom.Framework.Helpers
{
    public static class ApiDataHelper
    {
        public static DateTime GetValueOrDefault(this DateTime startDate, DateTime? endDate = null)
        {
            if (startDate > DateTime.Now && !endDate.HasValue)
            {
                return startDate;
            }
            else if (startDate > DateTime.Now && endDate.HasValue && endDate > DateTime.Now && endDate > startDate)
            {
                return (DateTime)endDate;
            }

            startDate = DateTime.Now.AddMonths(1);
            DateTime firstDayOfMonth = new DateTime(startDate.Year, startDate.Month, 1);
            startDate = firstDayOfMonth.AddDays(14); //.ToString("yyyy-MM-dd");

            DateTime dataResult;
            if (endDate.HasValue)
                dataResult = firstDayOfMonth.AddDays(16); //.ToString("yyyy-MM-dd");
            else
                dataResult = startDate;

            return dataResult;
        }
    }
}