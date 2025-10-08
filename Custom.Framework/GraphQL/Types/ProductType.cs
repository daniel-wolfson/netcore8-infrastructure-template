using Custom.Framework.GraphQL.Models;
using HotChocolate.Types;

namespace Custom.Framework.GraphQL.Types
{
    /// <summary>
    /// GraphQL type definition for Product
    /// </summary>
    public class ProductType : ObjectType<Product>
    {
        protected override void Configure(IObjectTypeDescriptor<Product> descriptor)
        {
            descriptor.Description("Represents a product in the catalog");
            
            descriptor.Field(p => p.Id)
                .Description("The unique identifier of the product");
                
            descriptor.Field(p => p.Name)
                .Description("The name of the product");
                
            descriptor.Field(p => p.Description)
                .Description("The description of the product");
                
            descriptor.Field(p => p.Price)
                .Description("The price of the product");
                
            descriptor.Field(p => p.Category)
                .Description("The category this product belongs to");

            descriptor.Field(p => p.CreatedAt)
                .Description("The date and time when the product was created");

            descriptor.Field(p => p.UpdatedAt)
                .Description("The date and time when the product was last updated");
        }
    }
}
