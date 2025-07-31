using System.ComponentModel.DataAnnotations;

namespace Custom.Framework.Models
{
    public class ServiceStatus
    {
        public static implicit operator int(ServiceStatus status) => status.Code;
        public static explicit operator ServiceStatus(int code) => new(code, "");

        /// <summary>
        /// Unknown status - NoContent, main flow success
        /// </summary>
        [Display(Description = "Unknown status - main flow successful")]
        public static readonly ServiceStatus Ok = new(200, "OK");

        /// <summary>
        /// Unknown status - NoContent, main flow success
        /// </summary>
        [Display(Description = "Unknown status - NoContent, main flow successful")]
        public static readonly ServiceStatus NoContent = new(204, "OK");

        /// <summary>
        /// Unknown status - NoContent, main flow success
        /// </summary>
        [Display(Description = "Unknown status - Canceled (Needing to Reset Content), main flow success")]
        public static readonly ServiceStatus Canceled = new(205, "Canceled");

        /// <summary>
        /// Unknown status - NoContent, main flow success
        /// </summary>
        [Display(Description = "Unknown status - PartialContent, main flow successful")]
        public static readonly ServiceStatus PartialContent = new(206, "Partial");

        /// <summary>
        /// Unknown fatal error with suggestion to stop or reset the main flow
        /// </summary>
        [Display(Description = "Unknown status - badRequest error, with suggestion to stop or reset the main flow")]
        public static readonly ServiceStatus BadRequest = new(400, "BadRequest");

        /// <summary>
        /// Unknown status - NotFound, with enabled to contionue the main flow
        /// </summary>
        [Display(Description = "Unknown status - NotFound, with enabled to contionue the main flow")]
        public static readonly ServiceStatus NotFound = new(404, "NodFound");

        /// <summary>
        /// Unknown status - Conflict, with enabled to contionue the main flow
        /// </summary>
        [Display(Description = "Unknown status - Conflict, with enabled to contionue the main flow")]
        public static readonly ServiceStatus Conflict = new(409, "Conflict");

        [Display(Description = "Unknown status - Conflict, with enabled to contionue the main flow")]
        public static ServiceStatus ErrorResult(int status, string? message = null) => new(status, message ?? "ErrorInfo");

        /// <summary>
        /// Unknown status - Fatal error, with suggestion to stop or reset the main flow
        /// </summary>
        [Display(Description = "Unknown status - Fatal error, with suggestion to stop or reset the main flow")]
        public static readonly ServiceStatus FatalError = new(500, "FatalError");

        public int Code { get; protected set; }
        public string Description { get; protected set; }

        protected ServiceStatus(int internalValue, string description)
        {
            Code = internalValue;
            Description = description;
        }
    }
}
