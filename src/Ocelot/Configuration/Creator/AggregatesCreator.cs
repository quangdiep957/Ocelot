using Ocelot.Configuration.Builder;
using Ocelot.Configuration.File;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;

namespace Ocelot.Configuration.Creator
{
    public class AggregatesCreator : IAggregatesCreator
    {
        private readonly IUpstreamTemplatePatternCreator _creator;

        public AggregatesCreator(IUpstreamTemplatePatternCreator creator)
        {
            _creator = creator;
        }

        public List<Route> Create(FileConfiguration fileConfiguration, List<Route> routes)
        {
            return fileConfiguration.Aggregates
                .Select(aggregate => SetUpAggregateRoute(routes, aggregate, fileConfiguration.GlobalConfiguration))
                .Where(aggregate => aggregate != null)
                .ToList();
        }

        private Route SetUpAggregateRoute(IEnumerable<Route> routes, FileAggregateRoute aggregateRoute, FileGlobalConfiguration globalConfiguration)
        {
            if (!aggregateRoute.UpstreamHttpMethod.Any())
            {
                // Default method to Get for standard use case
                aggregateRoute.UpstreamHttpMethod.Add(HttpMethod.Get.ToString());
            }

            var applicableRoutes = new List<DownstreamRoute>();
            var allRoutes = routes.SelectMany(x => x.DownstreamRoute);

            foreach (var routeKey in aggregateRoute.RouteKeys)
            {
                var selec = allRoutes.FirstOrDefault(q => q.Key == routeKey);
                if (selec == null)
                {
                    return null;
                }

                applicableRoutes.Add(selec);
            }

            var upstreamTemplatePattern = _creator.Create(aggregateRoute);

            var route = new RouteBuilder()
                .WithUpstreamHttpMethod(aggregateRoute.UpstreamHttpMethod)
                .WithUpstreamPathTemplate(upstreamTemplatePattern)
                .WithDownstreamRoutes(applicableRoutes)
                .WithAggregateRouteConfig(aggregateRoute.RouteKeysConfig)
                .WithUpstreamHost(aggregateRoute.UpstreamHost)
                .WithAggregator(aggregateRoute.Aggregator)
                .Build();

            return route;
        }
    }
}
