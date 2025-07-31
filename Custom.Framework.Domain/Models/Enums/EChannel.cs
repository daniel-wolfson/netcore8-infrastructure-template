using System.ComponentModel.DataAnnotations;

namespace Custom.Domain.Optima.Models.Enums
{
    public enum EChannel
    {
        [Display(Description = "error mode")]
        NONE,
        [Display(Description = "Web site for 'he' location")]
        WHENIS,
        [Display(Description = "Web site for 'Cal-hakodem' location")]
        WCHNIS,
        [Display(Description = "Web site for 'en' location")]
        WENUSD,
        [Display(Description = "for agents he")]
        TANIS,
        [Display(Description = "for agents en")]
        TAUSD,
        [Display(Description = "register to sun club from home page")]
        PSEUDO
    }
}
