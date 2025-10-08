using HotChocolate.Execution;

namespace Custom.Framework.Tests
{
    public partial class TestProgram
    {
        private static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.Services.AddControllers();
            var app = builder.Build();
            app.Run();
        }
    }
}