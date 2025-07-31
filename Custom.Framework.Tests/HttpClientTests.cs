using FluentAssertions;
using Custom.Framework.Contracts;
using Custom.Framework.Core;
using Custom.Framework.Extensions;
using Custom.Framework.TestFactory.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Xml.Linq;
using Xunit.Abstractions;

namespace Custom.Framework.Tests;

public class HttpClientTests(ITestOutputHelper output) : TestHostBase(output)
{
    [Fact]
    public void ApiHttpClientFactoryPositiveTest()
    {
        var testHost = CreateTestHost<object>((builder, services) => {
            services.ConfigureHttpClients(builder.Configuration);
        });
        
        var apiHttpClientFactory = testHost.Services.GetService<IApiHttpClientFactory>();
        var config = testHost.Services.GetService<IConfiguration>()!;
        var endpoints = config.GetSection("Endpoints").GetChildren()
            .Where(c => c.Key != "OptimaEndpointBase")
            .ToList();
        var clientNames = ApiHttpClientNames.GetNames();

        var clients = endpoints
            .Select(item => apiHttpClientFactory!.CreateClient(item.Key))
            .Where(c => c != null
                && c.BaseAddress != null
                && !string.IsNullOrEmpty(c.BaseAddress.Host)
                && clientNames.Contains(c.ClientName)
                && c.ClientName != "OptimaEndpointBase")
            .ToList();

        var differentClients = clients
            .Select(x => x.ClientName)
            .Except(endpoints.Select(x => x.Key))
            .Concat(endpoints.Select(x => x.Key)
            .Except(clients.Select(x => x.ClientName)))
            .ToList();
        
        
        if (differentClients.Count != 0)
        {
            differentClients.Should().BeEmpty($"fount diff: {string.Join(", ", differentClients)}");
        }

        clients.Should().HaveCount(endpoints.Count);
    }
}