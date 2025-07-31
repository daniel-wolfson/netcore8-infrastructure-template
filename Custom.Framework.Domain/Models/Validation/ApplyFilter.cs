using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Custom.Domain.Optima.Models.Validation
{
    public class ApplyFilter
    {
        public ApplyFilter(ApplyFilters filterName, string? title = null,
            [CallerFilePath] string callerFilePath = "", 
            [CallerMemberName] string callerMemberName = "")
        {
            title = string.IsNullOrEmpty(title) ? "" : " " + title;
            Title = $"{Path.GetFileNameWithoutExtension(callerFilePath)}.{callerMemberName}{title}";
            FilterName = filterName.ToString();
            var fieldInfo = typeof(ApplyFilters).GetField(filterName.ToString());
            var displayAttribute = fieldInfo?.GetCustomAttribute<DisplayAttribute>();
            Description = displayAttribute?.Description ?? "";
        }

        public ApplyFilter(ApplyFilters name, string? title, int beforeCount, int afterCount, string? query = null)
        {
            FilterName = $"{name}_{Guid.NewGuid()}";
            Title = title ?? "";
            BeforeCount = beforeCount;
            AfterCount = afterCount;
            Query = query ?? "";

            var fieldInfo = typeof(ApplyFilters).GetField(name.ToString());
            var displayAttribute = fieldInfo?.GetCustomAttribute<DisplayAttribute>();
            Description = displayAttribute?.Description ?? "";
        }

        public string FilterName { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Query { get; set; }
        public bool IsValid { get; set; } = true;

        public int BeforeCount { get; set; }
        public int AfterCount { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }

    }
}