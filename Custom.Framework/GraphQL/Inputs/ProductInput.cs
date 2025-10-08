namespace Custom.Framework.GraphQL.Inputs
{
    /// <summary>
    /// Input type for adding a new product
    /// </summary>
    public class AddProductInput
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int CategoryId { get; set; }
    }

    /// <summary>
    /// Input type for updating an existing product
    /// </summary>
    public class UpdateProductInput
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public decimal? Price { get; set; }
        public int? CategoryId { get; set; }
    }
}
