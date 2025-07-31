using System.ComponentModel.DataAnnotations.Schema;
using Custom.Domain.Optima.Models.Base;

namespace Custom.Domain.Optima.Models.Main
{
    public class PlanData : OptimaData
    {
        public string PlanCode { get; set; }

        /// <summary> search board base code (mapped property) </summary>
        public string BoardBaseCode { get; set; } = string.Empty;

        public int HotelID { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int Order { get; set; }
        public string GlobalPlanCode { get; set; }
        public string OtaMealPlanCode { get; set; }

        /// <summary> 
        /// not optima prop, 
        /// it is true when FilterName was successfly mapped to BoardBaseCode
        /// </summary>
        public bool isValid { get; set; }
    }
}