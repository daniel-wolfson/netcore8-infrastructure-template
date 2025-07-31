using Custom.Framework.Models;
using Microsoft.AspNetCore.Mvc;

namespace Custom.Framework.Contracts
{
    public interface IServiceResult<T>
    {
        public bool IsSuccess { get; }

        /// <summary> Unknown status result </summary>
        public int Status { get; set; }

        /// <summary> Current request url </summary>
        public string? RequestUrl { get; set; }

        /// <summary> Current request data (such as data in post request) </summary>
        public object? RequestData { get; set; }

        public string Message { get; set; }

        /// <summary> Code is payload from optima </summary>
        public T? Value { get; set; }

        string CorrelationId { get; set; }

        /// <summary> GenericParameters are dynamic params from optima </summary>
        public Dictionary<string, object>? GenericParameters { get; set; }

        public IActionResult ToActionResult();
    }
}