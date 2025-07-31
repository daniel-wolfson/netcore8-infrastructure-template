using System.ComponentModel.DataAnnotations;

namespace Custom.Domain.Optima.Models.Enums
{
    public enum LanguageTypes
    {
        None = 0,
        
        [Display(Name = "En")]
        Eng = 1,

        [Display(Name = "He")]
        Heb = 2
    }
}
