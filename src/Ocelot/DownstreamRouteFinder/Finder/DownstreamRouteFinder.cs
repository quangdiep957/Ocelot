using Microsoft.AspNetCore.Http;
using Ocelot.Configuration;
using Ocelot.DownstreamRouteFinder.UrlMatcher;
using Ocelot.Responses;

namespace Ocelot.DownstreamRouteFinder.Finder
{
    public class DownstreamRouteFinder : IDownstreamRouteProvider
    {
        private readonly IUrlPathToUrlTemplateMatcher _urlMatcher;
        private readonly IPlaceholderNameAndValueFinder _placeholderNameAndValueFinder;

        public DownstreamRouteFinder(IUrlPathToUrlTemplateMatcher urlMatcher, IPlaceholderNameAndValueFinder urlPathPlaceholderNameAndValueFinder)
        {
            _urlMatcher = urlMatcher;
            _placeholderNameAndValueFinder = urlPathPlaceholderNameAndValueFinder;
        }

        public Response<DownstreamRouteHolder> Get(
            string upstreamUrlPath,
            string upstreamQueryString,
            string httpMethod,
            IInternalConfiguration configuration,
            string upstreamHost,
            IHeaderDictionary requestHeaders)
        {
            var downstreamRoutes = new List<DownstreamRouteHolder>();

            var applicableRoutes = configuration.Routes
                .Where(r => RouteIsApplicableToThisRequest(r, httpMethod, upstreamHost, requestHeaders))
                .OrderByDescending(x => x.UpstreamTemplatePattern.Priority);

            foreach (var route in applicableRoutes)
            {
                var urlMatch = _urlMatcher.Match(upstreamUrlPath, upstreamQueryString, route.UpstreamTemplatePattern);

                if (urlMatch.Data.Match)
                {
                    downstreamRoutes.Add(GetPlaceholderNamesAndValues(upstreamUrlPath, upstreamQueryString, route));
                }
            }

            if (downstreamRoutes.Any())
            {
                var notNullOption = downstreamRoutes.FirstOrDefault(x => !string.IsNullOrEmpty(x.Route.UpstreamHost));
                var nullOption = downstreamRoutes.FirstOrDefault(x => string.IsNullOrEmpty(x.Route.UpstreamHost));

                return notNullOption != null ? new OkResponse<DownstreamRouteHolder>(notNullOption) : new OkResponse<DownstreamRouteHolder>(nullOption);
            }

            return new ErrorResponse<DownstreamRouteHolder>(new UnableToFindDownstreamRouteError(upstreamUrlPath, httpMethod));
        }

        private static bool RouteIsApplicableToThisRequest(Route route, string httpMethod, string upstreamHost, IHeaderDictionary requestHeaders)
            => (route.UpstreamHttpMethod.Count == 0 || RouteHasHttpMethod(route, httpMethod)) &&
                (string.IsNullOrEmpty(route.UpstreamHost) || route.UpstreamHost == upstreamHost) &&
                (route.UpstreamHeaderRoutingOptions?.Enabled() != true || RequiredUpstreamHeadersArePresent(route.UpstreamHeaderRoutingOptions, requestHeaders));

        private static bool RouteHasHttpMethod(Route route, string httpMethod) =>
            route.UpstreamHttpMethod.Contains(new HttpMethod(httpMethod));
        
        private static bool RequiredUpstreamHeadersArePresent(UpstreamHeaderRoutingOptions options, IHeaderDictionary requestHeaders) =>
            options.Mode == UpstreamHeaderRoutingTriggerMode.Any
                ? options.Headers.HasAnyOf(requestHeaders)
                : options.Headers.HasAllOf(requestHeaders);

        private DownstreamRouteHolder GetPlaceholderNamesAndValues(string path, string query, Route route)
        {
            var templatePlaceholderNameAndValues = _placeholderNameAndValueFinder.Find(path, query, route.UpstreamTemplatePattern.OriginalValue);

            return new DownstreamRouteHolder(templatePlaceholderNameAndValues.Data, route);
        }
    }
}
