namespace Isrotel.Framework.Models.Main
{
    public class TranslationPacageShowRequest
    {
        public List<int> HotelIDList { get; set; }
        public int LanguageID { get; set; }
        public bool IsLocal { get; set; } = true;
        public bool IncludeDerivedPackages { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public int CustomerID { get; set; }
    }
}
