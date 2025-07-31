using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace Custom.Framework.Cache
{
    public class ServiceProviderCache
    {
        private static readonly ConcurrentDictionary<Type, ServiceDescriptor> _serviceDescriptors = new();

        private static IServiceProvider _serviceProvider;

        public static ServiceProviderCache Instance { get; } = new ServiceProviderCache();

        public static void SetServiceProvider(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public object? GetService(Type serviceType)
        {
            var serviceDescriptor = _serviceDescriptors.FirstOrDefault(x => x.Key == serviceType).Value;

            if (!serviceType.IsAbstract && serviceType.IsClass && !serviceType.IsGenericTypeDefinition)
                return ActivatorUtilities.GetServiceOrCreateInstance(_serviceProvider, serviceDescriptor.ServiceType);
            else
                return _serviceProvider.GetService(serviceType);
        }
    }
}
