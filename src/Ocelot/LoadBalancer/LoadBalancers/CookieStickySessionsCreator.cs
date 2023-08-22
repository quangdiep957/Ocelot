﻿using Ocelot.Configuration;

using Ocelot.Infrastructure;

using Ocelot.Responses;

using Ocelot.ServiceDiscovery.Providers;
using System.Threading.Tasks;

namespace Ocelot.LoadBalancer.LoadBalancers
{
    public class CookieStickySessionsCreator : ILoadBalancerCreator
    {
        private readonly IStickySessionStorage _sessionStorage;

        public CookieStickySessionsCreator(IStickySessionStorage sessionStorage)
        {
            _sessionStorage = sessionStorage;
        }

        public Response<ILoadBalancer> Create(DownstreamRoute route, IServiceDiscoveryProvider serviceProvider)
        {
            var loadBalancer = new RoundRobin(async () => await serviceProvider.Get());
            var bus = new InMemoryBus<StickySession>();
            return new OkResponse<ILoadBalancer>(new CookieStickySessions(loadBalancer, route.LoadBalancerOptions.Key,
                route.LoadBalancerOptions.ExpiryInMs, bus, _sessionStorage));
        }

        public string Type => nameof(CookieStickySessions);
    }
}
