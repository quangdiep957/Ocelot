﻿using Ocelot.Logging;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

using Ocelot.ServiceDiscovery;

namespace Ocelot.Provider.Consul
{
    public static class ConsulProviderFactory
    {
        public static ServiceDiscoveryFinderDelegate Get = (provider, config, route) =>
        {
            var factory = provider.GetService<IOcelotLoggerFactory>();

            var consulFactory = provider.GetService<IConsulClientFactory>();
            var memoryCache = provider.GetRequiredService<IMemoryCache>();

            var consulRegistryConfiguration = new ConsulRegistryConfiguration(config.Scheme, config.Host, config.Port, route.ServiceName, config.Token);

            var consulServiceDiscoveryProvider = new Consul(consulRegistryConfiguration, factory, consulFactory);

            if (config.Type?.ToLower() == "pollconsul")
            {
                return new PollConsul(config.PollingInterval, route.ServiceName, factory, consulServiceDiscoveryProvider, memoryCache);
            }

            return consulServiceDiscoveryProvider;
        };
    }
}
