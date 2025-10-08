using Custom.Framework.GraphQL.Models;
using HotChocolate.Types;

namespace Custom.Framework.GraphQL.Types
{
    /// <summary>
    /// GraphQL type definition for Category
    /// </summary>
    public class CategoryType : ObjectType<Category>
    {
        protected override void Configure(IObjectTypeDescriptor<Category> descriptor)
        {
            descriptor.Description("Represents a product category");
            
            descriptor.Field(c => c.Id)
                .Description("The unique identifier of the category");
            
            descriptor.Field(c => c.Name)
                .Description("The name of the category");
            
            descriptor.Field(c => c.Description)
                .Description("The description of the category");
            
            descriptor.Field(c => c.Products)
                .Description("Products in this category");
        }
    }
}
