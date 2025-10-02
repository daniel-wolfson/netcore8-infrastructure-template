using Custom.Framework.Kafka;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// For each delivery semantic (names must match DeliverySemantics.ToString())
builder.Services.AddOptions<ProducerSettings>(DeliverySemantics.AtMostOnce.ToString())
    .Bind(builder.Configuration.GetSection("Kafka:Producer:AtMostOnce"));
builder.Services.AddOptions<ProducerSettings>(DeliverySemantics.AtLeastOnce.ToString())
    .Bind(builder.Configuration.GetSection("Kafka:Producer:AtLeastOnce"));
builder.Services.AddOptions<ProducerSettings>(DeliverySemantics.ExactlyOnce.ToString())
    .Bind(builder.Configuration.GetSection("Kafka:Producer:ExactlyOnce"));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();
app.MapControllers();

app.Run();

/// <summary>
/// Marker class for WebApplicationFactory
/// </summary>
public partial class Program { }
