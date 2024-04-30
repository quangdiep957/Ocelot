﻿using Microsoft.AspNetCore.Http;
using Ocelot.Configuration.File;
using Ocelot.Values;

namespace Ocelot.AcceptanceTests
{
    public class ClientRateLimitTests : IDisposable
    {
        private readonly Steps _steps;
        private int _counterOne;
        private readonly ServiceHandler _serviceHandler;

        public ClientRateLimitTests()
        {
            _serviceHandler = new ServiceHandler();
            _steps = new Steps();
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
                        RequestIdKey = _steps.RequestIdKey,
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
                .And(x => _steps.GivenThereIsAConfiguration(configuration))
                .And(x => _steps.GivenOcelotIsRunning())
                .When(x => _steps.WhenIGetUrlOnTheApiGatewayMultipleTimesForRateLimit("/api/ClientRateLimit", 1))
                .Then(x => _steps.ThenTheStatusCodeShouldBe(200))
                .When(x => _steps.WhenIGetUrlOnTheApiGatewayMultipleTimesForRateLimit("/api/ClientRateLimit", 2))
                .Then(x => _steps.ThenTheStatusCodeShouldBe(200))
                .When(x => _steps.WhenIGetUrlOnTheApiGatewayMultipleTimesForRateLimit("/api/ClientRateLimit", 1))
                .Then(x => _steps.ThenTheStatusCodeShouldBe(428))
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
                        RequestIdKey = _steps.RequestIdKey,

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
                .And(x => _steps.GivenThereIsAConfiguration(configuration))
                .And(x => _steps.GivenOcelotIsRunning())
                .When(x => _steps.WhenIGetUrlOnTheApiGatewayMultipleTimesForRateLimit("/api/ClientRateLimit", 1))
                .Then(x => _steps.ThenTheStatusCodeShouldBe(200))
                .When(x => _steps.WhenIGetUrlOnTheApiGatewayMultipleTimesForRateLimit("/api/ClientRateLimit", 2))
                .Then(x => _steps.ThenTheStatusCodeShouldBe(200))
                .When(x => _steps.WhenIGetUrlOnTheApiGatewayMultipleTimesForRateLimit("/api/ClientRateLimit", 1))
                .Then(x => _steps.ThenTheStatusCodeShouldBe(428))
                .And(x => _steps.GivenIWait(1000))
                .When(x => _steps.WhenIGetUrlOnTheApiGatewayMultipleTimesForRateLimit("/api/ClientRateLimit", 1))
                .Then(x => _steps.ThenTheStatusCodeShouldBe(428))
                .And(x => _steps.GivenIWait(1000))
                .When(x => _steps.WhenIGetUrlOnTheApiGatewayMultipleTimesForRateLimit("/api/ClientRateLimit", 1))
                .Then(x => _steps.ThenTheStatusCodeShouldBe(200))
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
                        RequestIdKey = _steps.RequestIdKey,

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
                .And(x => _steps.GivenThereIsAConfiguration(configuration))
                .And(x => _steps.GivenOcelotIsRunning())
                .When(x => _steps.WhenIGetUrlOnTheApiGatewayMultipleTimesForRateLimit("/api/ClientRateLimit", 4))
                .Then(x => _steps.ThenTheStatusCodeShouldBe(200))
                .BDDfy();
        }

        [Fact]
        public void should_set_ratelimiting_headers_on_response_when_DisableRateLimitHeaders_set_to_false()
        {
            int port = PortFinder.GetRandomPort();

            var configuration = CreateConfigurationForCheckingHeaders(port, false);

            this.Given(x => x.GivenThereIsAServiceRunningOn($"http://localhost:{port}", "/api/ClientRateLimit"))
                .And(x => _steps.GivenThereIsAConfiguration(configuration))
                .And(x => _steps.GivenOcelotIsRunning())
                .When(x => _steps.WhenIGetUrlOnTheApiGatewayMultipleTimesForRateLimit("/api/ClientRateLimit", 1))
                .Then(x => _steps.ThenRateLimitingHeadersExistInResponse(true))
                .When(x => _steps.WhenIGetUrlOnTheApiGatewayMultipleTimesForRateLimit("/api/ClientRateLimit", 2))
                .Then(x => _steps.ThenRateLimitingHeadersExistInResponse(true))
                .When(x => _steps.WhenIGetUrlOnTheApiGatewayMultipleTimesForRateLimit("/api/ClientRateLimit", 1))
                .Then(x => _steps.ThenRateLimitingHeadersExistInResponse(false))
                .And(x => _steps.ThenRetryAfterHeaderExistsInResponse(true))
                .BDDfy();
        }

        [Fact]
        public void should_not_set_ratelimiting_headers_on_response_when_DisableRateLimitHeaders_set_to_true()
        {
            int port = PortFinder.GetRandomPort();

            var configuration = CreateConfigurationForCheckingHeaders(port, true);

            this.Given(x => x.GivenThereIsAServiceRunningOn($"http://localhost:{port}", "/api/ClientRateLimit"))
                .And(x => _steps.GivenThereIsAConfiguration(configuration))
                .And(x => _steps.GivenOcelotIsRunning())
                .When(x => _steps.WhenIGetUrlOnTheApiGatewayMultipleTimesForRateLimit("/api/ClientRateLimit", 1))
                .Then(x => _steps.ThenRateLimitingHeadersExistInResponse(false))
                .When(x => _steps.WhenIGetUrlOnTheApiGatewayMultipleTimesForRateLimit("/api/ClientRateLimit", 2))
                .Then(x => _steps.ThenRateLimitingHeadersExistInResponse(false))
                .When(x => _steps.WhenIGetUrlOnTheApiGatewayMultipleTimesForRateLimit("/api/ClientRateLimit", 1))
                .Then(x => _steps.ThenRateLimitingHeadersExistInResponse(false))
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

        public void Dispose()
        {
            _steps.Dispose();
        }
    }
}
