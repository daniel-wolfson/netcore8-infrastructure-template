namespace Custom.Framework.GraphQL.Inputs
{
    /// <summary>
    /// Input type for adding a new category
    /// </summary>
    public class AddCategoryInput
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// Input type for updating an existing category
    /// </summary>
    public class UpdateCategoryInput
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
    }
}
