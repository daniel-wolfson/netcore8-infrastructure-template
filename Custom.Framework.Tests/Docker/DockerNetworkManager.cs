using Docker.DotNet;
using Docker.DotNet.Models;

namespace Custom.Framework.Tests.Docker
{
    public static class DockerNetworkManager
    {
        public static async Task<NetworkResponse> GetNetworkAsync(string networkName)
        {
            using var client = new DockerClientConfiguration().CreateClient();

            // List all networks
            var networks = await client.Networks.ListNetworksAsync(new NetworksListParameters());

            // Find the network by name
            var network = networks.FirstOrDefault(n => n.Name == networkName);

            if (network == null)
            {
                Console.WriteLine($"Network '{networkName}' not found.");
                return null;
            }

            Console.WriteLine($"Found network: {network.Name}, ID: {network.ID}");
            return network;
        }
    }
}

