using Microsoft.AspNetCore.Http;
using Ocelot.Configuration.File;

namespace Ocelot.AcceptanceTests
{
    public class RoutingWithQueryStringTests : IDisposable
    {
        private readonly Steps _steps;
        private readonly ServiceHandler _serviceHandler;

        public RoutingWithQueryStringTests()
        {
            _serviceHandler = new ServiceHandler();
            _steps = new Steps();
        }

        [Fact]
        public void Should_return_response_200_with_query_string_template()
        {
            var subscriptionId = Guid.NewGuid().ToString();
            var unitId = Guid.NewGuid().ToString();
            var port = PortFinder.GetRandomPort();

            var configuration = new FileConfiguration
            {
                Routes = new List<FileRoute>
                    {
                        new()
                        {
                            DownstreamPathTemplate = "/api/subscriptions/{subscriptionId}/updates?unitId={unitId}",
                            DownstreamScheme = "http",
                            DownstreamHostAndPorts = new List<FileHostAndPort>
                            {
                                new()
                                {
                                    Host = "localhost",
                                    Port = port,
                                },
                            },
                            UpstreamPathTemplate = "/api/units/{subscriptionId}/{unitId}/updates",
                            UpstreamHttpMethod = new List<string> { "Get" },
                        },
                    },
            };

            this.Given(x => x.GivenThereIsAServiceRunningOn($"http://localhost:{port}", $"/api/subscriptions/{subscriptionId}/updates", $"?unitId={unitId}", 200, "Hello from Laura"))
                .And(x => _steps.GivenThereIsAConfiguration(configuration))
                .And(x => _steps.GivenOcelotIsRunning())
                .When(x => _steps.WhenIGetUrlOnTheApiGateway($"/api/units/{subscriptionId}/{unitId}/updates"))
                .Then(x => _steps.ThenTheStatusCodeShouldBe(HttpStatusCode.OK))
                .And(x => _steps.ThenTheResponseBodyShouldBe("Hello from Laura"))
                .BDDfy();
        }

        [Fact]
        public void Should_return_response_200_with_query_string_template_different_keys()
        {
            var subscriptionId = Guid.NewGuid().ToString();
            var unitId = Guid.NewGuid().ToString();
            var port = PortFinder.GetRandomPort();

            var configuration = new FileConfiguration
            {
                Routes = new List<FileRoute>
                    {
                        new()
                        {
                            DownstreamPathTemplate = "/api/subscriptions/{subscriptionId}/updates?unitId={unitId}",
                            DownstreamScheme = "http",
                            DownstreamHostAndPorts = new List<FileHostAndPort>
                            {
                                new()
                                {
                                    Host = "localhost",
                                    Port = port,
                                },
                            },
                            UpstreamPathTemplate = "/api/units/{subscriptionId}/updates?unit={unitId}",
                            UpstreamHttpMethod = new List<string> { "Get" },
                        },
                    },
            };

            this.Given(x => x.GivenThereIsAServiceRunningOn($"http://localhost:{port}", $"/api/subscriptions/{subscriptionId}/updates", $"?unitId={unitId}", 200, "Hello from Laura"))
                .And(x => _steps.GivenThereIsAConfiguration(configuration))
                .And(x => _steps.GivenOcelotIsRunning())
                .When(x => _steps.WhenIGetUrlOnTheApiGateway($"/api/units/{subscriptionId}/updates?unit={unitId}"))
                .Then(x => _steps.ThenTheStatusCodeShouldBe(HttpStatusCode.OK))
                .And(x => _steps.ThenTheResponseBodyShouldBe("Hello from Laura"))
                .BDDfy();
        }

        [Fact(DisplayName = "Issue-327: " + nameof(Should_return_response_200_with_query_string_template_additional_key))]
        public void Should_return_response_200_with_query_string_template_additional_key()
        {
            var subscriptionId = Guid.NewGuid().ToString();
            var unitId = Guid.NewGuid().ToString();
            var port = PortFinder.GetRandomPort();

            var configuration = new FileConfiguration
            {
                Routes = new List<FileRoute>
                    {
                        new()
                        {
                            DownstreamPathTemplate = "/api/subscriptions/{subscriptionId}/updates?unitId={unitId}",
                            DownstreamScheme = "http",
                            DownstreamHostAndPorts = new List<FileHostAndPort>
                            {
                                new()
                                {
                                    Host = "localhost",
                                    Port = port,
                                },
                            },
                            UpstreamPathTemplate = "/api/units/{subscriptionId}/updates?unit={unitId}",
                            UpstreamHttpMethod = new List<string> { "Get" },
                        },
                    },
            };

            this.Given(x => x.GivenThereIsAServiceRunningOn($"http://localhost:{port}", $"/api/subscriptions/{subscriptionId}/updates", $"?unitId={unitId}&x=y", 200, "Hello from Laura"))
                .And(x => _steps.GivenThereIsAConfiguration(configuration))
                .And(x => _steps.GivenOcelotIsRunning())
                .When(x => _steps.WhenIGetUrlOnTheApiGateway($"/api/units/{subscriptionId}/updates?unit={unitId}&x=y"))
                .Then(x => _steps.ThenTheStatusCodeShouldBe(HttpStatusCode.OK))
                .And(x => _steps.ThenTheResponseBodyShouldBe("Hello from Laura"))
                .BDDfy();
        }

        [Fact]
        public void Should_return_response_200_with_odata_query_string()
        {
            var subscriptionId = Guid.NewGuid().ToString();
            var unitId = Guid.NewGuid().ToString();
            var port = PortFinder.GetRandomPort();

            var configuration = new FileConfiguration
            {
                Routes = new List<FileRoute>
                    {
                        new()
                        {
                            DownstreamPathTemplate = "/{everything}",
                            DownstreamScheme = "http",
                            DownstreamHostAndPorts = new List<FileHostAndPort>
                            {
                                new()
                                {
                                    Host = "localhost",
                                    Port = port,
                                },
                            },
                            UpstreamPathTemplate = "/{everything}",
                            UpstreamHttpMethod = new List<string> { "Get" },
                        },
                    },
            };

            this.Given(x => x.GivenThereIsAServiceRunningOn($"http://localhost:{port}", "/odata/customers", "?$filter=Name%20eq%20'Sam'", 200, "Hello from Laura"))
                .And(x => _steps.GivenThereIsAConfiguration(configuration))
                .And(x => _steps.GivenOcelotIsRunning())
                .When(x => _steps.WhenIGetUrlOnTheApiGateway("/odata/customers?$filter=Name eq 'Sam' "))
                .Then(x => _steps.ThenTheStatusCodeShouldBe(HttpStatusCode.OK))
                .And(x => _steps.ThenTheResponseBodyShouldBe("Hello from Laura"))
                .BDDfy();
        }

        [Fact]
        public void Should_return_response_200_with_query_string_upstream_template()
        {
            var subscriptionId = Guid.NewGuid().ToString();
            var unitId = Guid.NewGuid().ToString();
            var port = PortFinder.GetRandomPort();

            var configuration = new FileConfiguration
            {
                Routes = new List<FileRoute>
                {
                    new()
                    {
                        DownstreamPathTemplate = "/api/units/{subscriptionId}/{unitId}/updates",
                        DownstreamScheme = "http",
                        DownstreamHostAndPorts = new List<FileHostAndPort>
                        {
                            new()
                            {
                                Host = "localhost",
                                Port = port,
                            },
                        },
                        UpstreamPathTemplate = "/api/subscriptions/{subscriptionId}/updates?unitId={unitId}",
                        UpstreamHttpMethod = new List<string> { "Get" },
                    },
                },
            };

            this.Given(x => x.GivenThereIsAServiceRunningOn($"http://localhost:{port}", $"/api/units/{subscriptionId}/{unitId}/updates", string.Empty, 200, "Hello from Laura"))
                .And(x => _steps.GivenThereIsAConfiguration(configuration))
                .And(x => _steps.GivenOcelotIsRunning())
                .When(x => _steps.WhenIGetUrlOnTheApiGateway($"/api/subscriptions/{subscriptionId}/updates?unitId={unitId}"))
                .Then(x => _steps.ThenTheStatusCodeShouldBe(HttpStatusCode.OK))
                .And(x => _steps.ThenTheResponseBodyShouldBe("Hello from Laura"))
                .BDDfy();
        }

        [Fact]
        public void Should_return_response_404_with_query_string_upstream_template_no_query_string()
        {
            var subscriptionId = Guid.NewGuid().ToString();
            var unitId = Guid.NewGuid().ToString();
            var port = PortFinder.GetRandomPort();

            var configuration = new FileConfiguration
            {
                Routes = new List<FileRoute>
                {
                    new()
                    {
                        DownstreamPathTemplate = "/api/units/{subscriptionId}/{unitId}/updates",
                        DownstreamScheme = "http",
                        DownstreamHostAndPorts = new List<FileHostAndPort>
                        {
                            new()
                            {
                                Host = "localhost",
                                Port = port,
                            },
                        },
                        UpstreamPathTemplate = "/api/subscriptions/{subscriptionId}/updates?unitId={unitId}",
                        UpstreamHttpMethod = new List<string> { "Get" },
                    },
                },
            };

            this.Given(x => x.GivenThereIsAServiceRunningOn($"http://localhost:{port}", $"/api/units/{subscriptionId}/{unitId}/updates", string.Empty, 200, "Hello from Laura"))
                .And(x => _steps.GivenThereIsAConfiguration(configuration))
                .And(x => _steps.GivenOcelotIsRunning())
                .When(x => _steps.WhenIGetUrlOnTheApiGateway($"/api/subscriptions/{subscriptionId}/updates"))
                .Then(x => _steps.ThenTheStatusCodeShouldBe(HttpStatusCode.NotFound))
                .BDDfy();
        }

        [Fact]
        public void Should_return_response_404_with_query_string_upstream_template_different_query_string()
        {
            var subscriptionId = Guid.NewGuid().ToString();
            var unitId = Guid.NewGuid().ToString();
            var port = PortFinder.GetRandomPort();

            var configuration = new FileConfiguration
            {
                Routes = new List<FileRoute>
                {
                    new()
                    {
                        DownstreamPathTemplate = "/api/units/{subscriptionId}/{unitId}/updates",
                        DownstreamScheme = "http",
                        DownstreamHostAndPorts = new List<FileHostAndPort>
                        {
                            new()
                            {
                                Host = "localhost",
                                Port = port,
                            },
                        },
                        UpstreamPathTemplate = "/api/subscriptions/{subscriptionId}/updates?unitId={unitId}",
                        UpstreamHttpMethod = new List<string> { "Get" },
                    },
                },
            };

            this.Given(x => x.GivenThereIsAServiceRunningOn($"http://localhost:{port}", $"/api/units/{subscriptionId}/{unitId}/updates", string.Empty, 200, "Hello from Laura"))
                .And(x => _steps.GivenThereIsAConfiguration(configuration))
                .And(x => _steps.GivenOcelotIsRunning())
                .When(x => _steps.WhenIGetUrlOnTheApiGateway($"/api/subscriptions/{subscriptionId}/updates?test=1"))
                .Then(x => _steps.ThenTheStatusCodeShouldBe(HttpStatusCode.NotFound))
                .BDDfy();
        }

        [Fact]
        public void Should_return_response_200_with_query_string_upstream_template_multiple_params()
        {
            var subscriptionId = Guid.NewGuid().ToString();
            var unitId = Guid.NewGuid().ToString();
            var port = PortFinder.GetRandomPort();

            var configuration = new FileConfiguration
            {
                Routes = new List<FileRoute>
                {
                    new()
                    {
                        DownstreamPathTemplate = "/api/units/{subscriptionId}/{unitId}/updates",
                        DownstreamScheme = "http",
                        DownstreamHostAndPorts = new List<FileHostAndPort>
                        {
                            new()
                            {
                                Host = "localhost",
                                Port = port,
                            },
                        },
                        UpstreamPathTemplate = "/api/subscriptions/{subscriptionId}/updates?unitId={unitId}",
                        UpstreamHttpMethod = new List<string> { "Get" },
                    },
                },
            };

            this.Given(x => x.GivenThereIsAServiceRunningOn($"http://localhost:{port}", $"/api/units/{subscriptionId}/{unitId}/updates", "?productId=1", 200, "Hello from Laura"))
                .And(x => _steps.GivenThereIsAConfiguration(configuration))
                .And(x => _steps.GivenOcelotIsRunning())
                .When(x => _steps.WhenIGetUrlOnTheApiGateway($"/api/subscriptions/{subscriptionId}/updates?unitId={unitId}&productId=1"))
                .Then(x => _steps.ThenTheStatusCodeShouldBe(HttpStatusCode.OK))
                .And(x => _steps.ThenTheResponseBodyShouldBe("Hello from Laura"))
                .BDDfy();
        }

        /// <summary>
        /// To reproduce 1288: query string should contain the placeholder name and value.
        /// </summary>
        [Fact(DisplayName = "Issue-1288: " + nameof(Should_copy_query_string_to_downstream_path))]
        public void Should_copy_query_string_to_downstream_path()
        {
            var idName = "id";
            var idValue = "3";
            var queryName = idName + "1";
            var queryValue = "2" + idValue + "12";
            var port = PortFinder.GetRandomPort();

            var configuration = new FileConfiguration
            {
                Routes = new List<FileRoute>
                    {
                        new FileRoute
                        {
                            DownstreamPathTemplate = $"/cpx/t1/{{{idName}}}",
                            DownstreamScheme = "http",
                            DownstreamHostAndPorts = new List<FileHostAndPort>
                            {
                                new() { Host = "localhost", Port = port },
                            },
                            UpstreamPathTemplate = $"/safe/{{{idName}}}",
                            UpstreamHttpMethod = new List<string> { "Get" },
                        },
                    },
            };

            this.Given(x => x.GivenThereIsAServiceRunningOn($"http://localhost:{port}", $"/cpx/t1/{idValue}", $"?{queryName}={queryValue}", 200, "Hello from Laura"))
                .And(x => _steps.GivenThereIsAConfiguration(configuration))
                .And(x => _steps.GivenOcelotIsRunning())
                .When(x => _steps.WhenIGetUrlOnTheApiGateway($"/safe/{idValue}?{queryName}={queryValue}"))
                .Then(x => _steps.ThenTheStatusCodeShouldBe(HttpStatusCode.OK))
                .And(x => _steps.ThenTheResponseBodyShouldBe("Hello from Laura"))
                .BDDfy();
        }

        private void GivenThereIsAServiceRunningOn(string baseUrl, string basePath, string queryString, int statusCode, string responseBody)
        {
            _serviceHandler.GivenThereIsAServiceRunningOn(baseUrl, basePath, async context =>
            {
                if ((context.Request.PathBase.Value != basePath) || context.Request.QueryString.Value != queryString)
                {
                    context.Response.StatusCode = 500;
                    await context.Response.WriteAsync("downstream path didnt match base path");
                }
                else
                {
                    context.Response.StatusCode = statusCode;
                    await context.Response.WriteAsync(responseBody);
                }
            });
        }

        public void Dispose()
        {
            _serviceHandler?.Dispose();
            _steps.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
