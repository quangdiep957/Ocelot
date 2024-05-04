using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using Ocelot.Configuration.File;
using Ocelot.Values;

namespace Ocelot.AcceptanceTests
{
    public class ClientRateLimitTests : Steps, IDisposable
    {
        private int _counterOne;
        private readonly ServiceHandler _serviceHandler;

        public ClientRateLimitTests()
        {
            _serviceHandler = new ServiceHandler();
        }

        [Fact]
        public void should_call_withratelimiting()
        {
            var port = PortFinder.GetRandomPort();

            var configuration = new FileConfiguration
            {
                Routes = new List<FileRoute>
                {
                    new()
                    {
                        DownstreamPathTemplate = "/api/ClientRateLimit",
                        DownstreamHostAndPorts = new List<FileHostAndPort>
                        {
                            new()
                            {
                                Host = "localhost",
                                Port = port,
                            },
                        },
                        DownstreamScheme = "http",
                        UpstreamPathTemplate = "/api/ClientRateLimit",
                        UpstreamHttpMethod = new List<string> { "Get" },
                        RequestIdKey = this.RequestIdKey,
                        RateLimitOptions = new FileRateLimitRule
                        {
                            EnableRateLimiting = true,
                            ClientWhitelist = new List<string>(),
                            Limit = 3,
                            Period = "1s",
                            PeriodTimespan = 1000,
                        },
                    },
                },
                GlobalConfiguration = new FileGlobalConfiguration
                {
                    RateLimitOptions = new FileRateLimitOptions
                    {
                        ClientIdHeader = "ClientId",
                        DisableRateLimitHeaders = false,
                        QuotaExceededMessage = string.Empty,
                        RateLimitCounterPrefix = string.Empty,
                        HttpStatusCode = 428,
                    },
                    RequestIdKey = "oceclientrequest",
                },
            };

            this.Given(x => x.GivenThereIsAServiceRunningOn($"http://localhost:{port}", "/api/ClientRateLimit"))
                .And(x => GivenThereIsAConfiguration(configuration))
                .And(x => GivenOcelotIsRunning())
                .When(x => WhenIGetUrlOnTheApiGatewayMultipleTimesForRateLimit("/api/ClientRateLimit", 1))
                .Then(x => ThenTheStatusCodeShouldBe(200))
                .When(x => WhenIGetUrlOnTheApiGatewayMultipleTimesForRateLimit("/api/ClientRateLimit", 2))
                .Then(x => ThenTheStatusCodeShouldBe(200))
                .When(x => WhenIGetUrlOnTheApiGatewayMultipleTimesForRateLimit("/api/ClientRateLimit", 1))
                .Then(x => ThenTheStatusCodeShouldBe(428))
                .BDDfy();
        }

        [Fact]
        public void should_wait_for_period_timespan_to_elapse_before_making_next_request()
        {
            var port = PortFinder.GetRandomPort();

            var configuration = new FileConfiguration
            {
                Routes = new List<FileRoute>
                {
                    new()
                    {
                        DownstreamPathTemplate = "/api/ClientRateLimit",
                        DownstreamHostAndPorts = new List<FileHostAndPort>
                        {
                            new()
                            {
                                Host = "localhost",
                                Port = port,
                            },
                        },
                        DownstreamScheme = "http",
                        UpstreamPathTemplate = "/api/ClientRateLimit",
                        UpstreamHttpMethod = new List<string> { "Get" },
                        RequestIdKey = this.RequestIdKey,

                        RateLimitOptions = new FileRateLimitRule
                        {
                            EnableRateLimiting = true,
                            ClientWhitelist = new List<string>(),
                            Limit = 3,
                            Period = "1s",
                            PeriodTimespan = 2,
                        },
                    },
                },
                GlobalConfiguration = new FileGlobalConfiguration
                {
                    RateLimitOptions = new FileRateLimitOptions
                    {
                        ClientIdHeader = "ClientId",
                        DisableRateLimitHeaders = false,
                        QuotaExceededMessage = string.Empty,
                        RateLimitCounterPrefix = string.Empty,
                        HttpStatusCode = 428,
                    },
                    RequestIdKey = "oceclientrequest",
                },
            };

            this.Given(x => x.GivenThereIsAServiceRunningOn($"http://localhost:{port}", "/api/ClientRateLimit"))
                .And(x => GivenThereIsAConfiguration(configuration))
                .And(x => GivenOcelotIsRunning())
                .When(x => WhenIGetUrlOnTheApiGatewayMultipleTimesForRateLimit("/api/ClientRateLimit", 1))
                .Then(x => ThenTheStatusCodeShouldBe(200))
                .When(x => WhenIGetUrlOnTheApiGatewayMultipleTimesForRateLimit("/api/ClientRateLimit", 2))
                .Then(x => ThenTheStatusCodeShouldBe(200))
                .When(x => WhenIGetUrlOnTheApiGatewayMultipleTimesForRateLimit("/api/ClientRateLimit", 1))
                .Then(x => ThenTheStatusCodeShouldBe(428))
                .And(x => GivenIWait(1000))
                .When(x => WhenIGetUrlOnTheApiGatewayMultipleTimesForRateLimit("/api/ClientRateLimit", 1))
                .Then(x => ThenTheStatusCodeShouldBe(428))
                .And(x => GivenIWait(1000))
                .When(x => WhenIGetUrlOnTheApiGatewayMultipleTimesForRateLimit("/api/ClientRateLimit", 1))
                .Then(x => ThenTheStatusCodeShouldBe(200))
                .BDDfy();
        }

        [Fact]
        public void should_call_middleware_withWhitelistClient()
        {
            var port = PortFinder.GetRandomPort();

            var configuration = new FileConfiguration
            {
                Routes = new List<FileRoute>
                {
                    new()
                    {
                        DownstreamPathTemplate = "/api/ClientRateLimit",
                        DownstreamHostAndPorts = new List<FileHostAndPort>
                        {
                            new()
                            {
                                Host = "localhost",
                                Port = port,
                            },
                        },
                        DownstreamScheme = "http",
                        UpstreamPathTemplate = "/api/ClientRateLimit",
                        UpstreamHttpMethod = new List<string> { "Get" },
                        RequestIdKey = this.RequestIdKey,

                        RateLimitOptions = new FileRateLimitRule
                        {
                            EnableRateLimiting = true,
                            ClientWhitelist = new List<string> { "ocelotclient1"},
                            Limit = 3,
                            Period = "1s",
                            PeriodTimespan = 100,
                        },
                    },
                },
                GlobalConfiguration = new FileGlobalConfiguration
                {
                    RateLimitOptions = new FileRateLimitOptions
                    {
                        ClientIdHeader = "ClientId",
                        DisableRateLimitHeaders = false,
                        QuotaExceededMessage = string.Empty,
                        RateLimitCounterPrefix = string.Empty,
                    },
                    RequestIdKey = "oceclientrequest",
                },
            };

            this.Given(x => x.GivenThereIsAServiceRunningOn($"http://localhost:{port}", "/api/ClientRateLimit"))
                .And(x => GivenThereIsAConfiguration(configuration))
                .And(x => GivenOcelotIsRunning())
                .When(x => WhenIGetUrlOnTheApiGatewayMultipleTimesForRateLimit("/api/ClientRateLimit", 4))
                .Then(x => ThenTheStatusCodeShouldBe(200))
                .BDDfy();
        }

        [Fact]
        public void should_set_ratelimiting_headers_on_response_when_DisableRateLimitHeaders_set_to_false()
        {
            int port = PortFinder.GetRandomPort();

            var configuration = CreateConfigurationForCheckingHeaders(port, false);

            this.Given(x => x.GivenThereIsAServiceRunningOn($"http://localhost:{port}", "/api/ClientRateLimit"))
                .And(x => GivenThereIsAConfiguration(configuration))
                .And(x => GivenOcelotIsRunning())
                .When(x => WhenIGetUrlOnTheApiGatewayMultipleTimesForRateLimit("/api/ClientRateLimit", 1))
                .Then(x => ThenRateLimitingHeadersExistInResponse(true))
                .And(x => ThenRetryAfterHeaderExistsInResponse(false))
                .When(x => WhenIGetUrlOnTheApiGatewayMultipleTimesForRateLimit("/api/ClientRateLimit", 2))
                .Then(x => ThenRateLimitingHeadersExistInResponse(true))
                .And(x => ThenRetryAfterHeaderExistsInResponse(false))
                .When(x => WhenIGetUrlOnTheApiGatewayMultipleTimesForRateLimit("/api/ClientRateLimit", 1))
                .Then(x => ThenRateLimitingHeadersExistInResponse(false))
                .And(x => ThenRetryAfterHeaderExistsInResponse(true))
                .BDDfy();
        }

        [Fact]
        public void should_not_set_ratelimiting_headers_on_response_when_DisableRateLimitHeaders_set_to_true()
        {
            int port = PortFinder.GetRandomPort();

            var configuration = CreateConfigurationForCheckingHeaders(port, true);

            this.Given(x => x.GivenThereIsAServiceRunningOn($"http://localhost:{port}", "/api/ClientRateLimit"))
                .And(x => GivenThereIsAConfiguration(configuration))
                .And(x => GivenOcelotIsRunning())
                .When(x => WhenIGetUrlOnTheApiGatewayMultipleTimesForRateLimit("/api/ClientRateLimit", 1))
                .Then(x => ThenRateLimitingHeadersExistInResponse(false))
                .And(x => ThenRetryAfterHeaderExistsInResponse(false))
                .When(x => WhenIGetUrlOnTheApiGatewayMultipleTimesForRateLimit("/api/ClientRateLimit", 2))
                .Then(x => ThenRateLimitingHeadersExistInResponse(false))
                .And(x => ThenRetryAfterHeaderExistsInResponse(false))
                .When(x => WhenIGetUrlOnTheApiGatewayMultipleTimesForRateLimit("/api/ClientRateLimit", 1))
                .Then(x => ThenRateLimitingHeadersExistInResponse(false))
                .And(x => ThenRetryAfterHeaderExistsInResponse(false))
                .BDDfy();
        }

        private void GivenThereIsAServiceRunningOn(string baseUrl, string basePath)
        {
            _serviceHandler.GivenThereIsAServiceRunningOn(baseUrl, basePath, context =>
            {
                _counterOne++;
                context.Response.StatusCode = 200;
                context.Response.WriteAsync(_counterOne.ToString());
                return Task.CompletedTask;
            });
        }

        private FileConfiguration CreateConfigurationForCheckingHeaders(int port, bool disableRateLimitHeaders)
        {
            return new FileConfiguration
            {
                Routes = new List<FileRoute>
                {
                    new()
                    {
                        DownstreamPathTemplate = "/api/ClientRateLimit",
                        DownstreamHostAndPorts = new List<FileHostAndPort>
                        {
                            new()
                            {
                                Host = "localhost",
                                Port = port,
                            },
                        },
                        DownstreamScheme = "http",
                        UpstreamPathTemplate = "/api/ClientRateLimit",
                        UpstreamHttpMethod = new List<string> { "Get" },
                        RateLimitOptions = new FileRateLimitRule()
                        {
                            EnableRateLimiting = true,
                            ClientWhitelist = new List<string>(),
                            Limit = 3,
                            Period = "100s",
                            PeriodTimespan = 1000,
                        },
                    },
                },
                GlobalConfiguration = new FileGlobalConfiguration()
                {
                    RateLimitOptions = new FileRateLimitOptions()
                    {
                        DisableRateLimitHeaders = disableRateLimitHeaders,
                        QuotaExceededMessage = "",
                        HttpStatusCode = 428,
                    },
                },
            };
        }

        private void ThenRateLimitingHeadersExistInResponse(bool headersExist)
        {
            _response.Headers.Contains("X-Rate-Limit-Limit").ShouldBe(headersExist);
            _response.Headers.Contains("X-Rate-Limit-Remaining").ShouldBe(headersExist);
            _response.Headers.Contains("X-Rate-Limit-Reset").ShouldBe(headersExist);
        }

        private void ThenRetryAfterHeaderExistsInResponse(bool headersExist)
        {
            _response.Headers.Contains(HeaderNames.RetryAfter).ShouldBe(headersExist);
        }

        public override void Dispose()
        {
            _serviceHandler.Dispose();
            base.Dispose();
        }
    }
}
