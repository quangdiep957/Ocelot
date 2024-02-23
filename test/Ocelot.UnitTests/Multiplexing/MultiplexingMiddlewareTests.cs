﻿using Microsoft.AspNetCore.Http;
using Ocelot.Configuration;
using Ocelot.Configuration.Builder;
using Ocelot.DownstreamRouteFinder.UrlMatcher;
using Ocelot.Logging;
using Ocelot.Middleware;
using Ocelot.Multiplexer;
using System.Reflection;
using System.Security.Claims;
using Moq.Protected;

namespace Ocelot.UnitTests.Multiplexing
{
    public class MultiplexingMiddlewareTests
    {
        private MultiplexingMiddleware _middleware;
        private Ocelot.DownstreamRouteFinder.DownstreamRouteHolder _downstreamRoute;
        private int _count;
        private readonly HttpContext _httpContext;
        private readonly Mock<IResponseAggregatorFactory> factory;
        private readonly Mock<IResponseAggregator> aggregator;
        private readonly Mock<IOcelotLoggerFactory> loggerFactory;
        private readonly Mock<IOcelotLogger> logger;

        public MultiplexingMiddlewareTests()
        {
            _httpContext = new DefaultHttpContext();
            factory = new Mock<IResponseAggregatorFactory>();
            aggregator = new Mock<IResponseAggregator>();
            factory.Setup(x => x.Get(It.IsAny<Route>())).Returns(aggregator.Object);
            loggerFactory = new Mock<IOcelotLoggerFactory>();
            logger = new Mock<IOcelotLogger>();
            loggerFactory.Setup(x => x.CreateLogger<MultiplexingMiddleware>()).Returns(logger.Object);
            _middleware = new MultiplexingMiddleware(Next, loggerFactory.Object, factory.Object);
        }

        private Task Next(HttpContext context) => Task.FromResult(_count++);

        [Fact]
        public void should_multiplex()
        {
            var route = GivenDefaultRoute(2);
            this.Given(x => GivenTheFollowing(route))
                .When(x => WhenIMultiplex())
                .Then(x => ThePipelineIsCalled(2))
                .BDDfy();
        }

        [Fact]
        public void should_not_multiplex()
        {
            var route = new RouteBuilder().WithDownstreamRoute(new DownstreamRouteBuilder().Build()).Build();

            this.Given(x => GivenTheFollowing(route))
                .When(x => WhenIMultiplex())
                .Then(x => ThePipelineIsCalled(1))
                .BDDfy();
        }

        [Fact]
        [Trait("Bug", "1396")]
        public void Copy_User_ToTarget()
        {
            // Arrange
            GivenUser("test", "Copy", nameof(Copy_User_ToTarget));

            // Act
            var method = _middleware.GetType().GetMethod("CreateThreadContext", BindingFlags.NonPublic | BindingFlags.Static);
            var actual = (HttpContext)method.Invoke(_middleware, [_httpContext]);

            // Assert
            AssertUsers(actual);
        }

        [Fact]
        [Trait("Bug", "1396")]
        public async Task Invoke_ContextUser_ForwardedToDownstreamContext()
        {
            // Setup
            HttpContext actualContext = null;
            _middleware = new MultiplexingMiddleware(NextMe, loggerFactory.Object, factory.Object);
            Task NextMe(HttpContext context)
            {
                actualContext = context;
                return Next(context);
            }

            // Arrange
            GivenUser("test", "Invoke", nameof(Invoke_ContextUser_ForwardedToDownstreamContext));
            GivenTheFollowing(GivenDefaultRoute(2));

            // Act
            await WhenIMultiplex();

            // Assert
            ThePipelineIsCalled(2);
            AssertUsers(actualContext);
        }

        [Fact]
        [Trait("PR", "1826")]
        public async Task Should_Not_Copy_Context_If_One_Downstream_Route()
        {
            _middleware = new MultiplexingMiddleware(NextMe, loggerFactory.Object, factory.Object);
            Task NextMe(HttpContext context)
            {
                Assert.Equal(_httpContext, context);
                return Next(context);
            }

            // Arrange
            GivenUser("test", "Invoke", nameof(Invoke_ContextUser_ForwardedToDownstreamContext));
            GivenTheFollowing(GivenDefaultRoute(1));

            // Act
            await WhenIMultiplex();

            // Assert
            ThePipelineIsCalled(1);
            
        }

        [Fact]
        [Trait("PR", "1826")]
        public async Task Should_Call_ProcessSingleRoute_Once_If_One_Downstream_Route()
        {
            var mock = MockMiddlewareFactory(null);

            _middleware = mock.Object;

            // Arrange
            GivenUser("test", "Invoke", nameof(Invoke_ContextUser_ForwardedToDownstreamContext));
            GivenTheFollowing(GivenDefaultRoute(1));

            // Act
            await WhenIMultiplex();

            // Assert
            mock.Protected().Verify<Task>(
                "ProcessSingleRoute",
                Times.Once(),
                ItExpr.IsAny<HttpContext>(),
                ItExpr.IsAny<DownstreamRoute>()
            );
        }

        [Theory]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        [Trait("PR", "1826")]
        public async Task Should_Not_Call_ProcessSingleRoute_If_More_Than_One_Downstream_Route(int routesCount)
        {
            var mock = MockMiddlewareFactory(null);

            // Arrange
            GivenUser("test", "Invoke", nameof(Invoke_ContextUser_ForwardedToDownstreamContext));
            GivenTheFollowing(GivenDefaultRoute(routesCount));

            // Act
            await WhenIMultiplex();

            // Assert
            mock.Protected().Verify<Task>(
                "ProcessSingleRoute",
                Times.Never(),
                ItExpr.IsAny<HttpContext>(),
                ItExpr.IsAny<DownstreamRoute>()
            );
        }

        [Theory]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        [Trait("PR", "1826")]
        public async Task Should_Create_As_Many_Contexts_As_Routes_And_Map_Is_Called_Once(int routesCount)
        {
            var mock = MockMiddlewareFactory(routesCount);

            // Arrange
            GivenUser("test", "Invoke", nameof(Invoke_ContextUser_ForwardedToDownstreamContext));
            GivenTheFollowing(GivenDefaultRoute(routesCount));

            // Act
            await WhenIMultiplex();

            // Assert
            mock.Protected().Verify<Task>(
                "Map",
                Times.Once(),
                ItExpr.IsAny<HttpContext>(),
                ItExpr.IsAny<Route>(),
                ItExpr.Is<List<HttpContext>>(list => list.Count == routesCount)
            );
        }

        [Fact]
        [Trait("PR", "1826")]
        public async Task Should_Not_Call_ProcessSingleRoute_Or_Map_If_No_Route()
        {
            var mock = MockMiddlewareFactory(null);

            // Arrange
            GivenUser("test", "Invoke", nameof(Invoke_ContextUser_ForwardedToDownstreamContext));
            GivenTheFollowing(GivenDefaultRoute(0));

            // Act
            await WhenIMultiplex();

            // Assert
            mock.Protected().Verify<Task>(
                "ProcessSingleRoute",
                Times.Never(),
                ItExpr.IsAny<HttpContext>(),
                ItExpr.IsAny<DownstreamRoute>()
            );

            mock.Protected().Verify<Task>(
                "Map",
                Times.Never(),
                ItExpr.IsAny<HttpContext>(),
                ItExpr.IsAny<Route>(),
                ItExpr.IsAny<List<HttpContext>>());
        }

        private Mock<MultiplexingMiddleware> MockMiddlewareFactory(int? downstreamRoutesCount)
        {
            static Task MockRequestDelegate(HttpContext context) => Task.CompletedTask;

            var mock = new Mock<MultiplexingMiddleware>((RequestDelegate)MockRequestDelegate, loggerFactory.Object, factory.Object) { CallBase = true };

            mock.Protected().Setup<Task>(
                "Map",
                ItExpr.IsAny<HttpContext>(),
                ItExpr.IsAny<Route>(),
                downstreamRoutesCount == null ? ItExpr.IsAny<List<HttpContext>>() : ItExpr.Is<List<HttpContext>>(list => list.Count == downstreamRoutesCount)
            ).Returns(Task.CompletedTask).Verifiable();

            mock.Protected().Setup<Task>(
                "ProcessSingleRoute",
                ItExpr.IsAny<HttpContext>(),
                ItExpr.IsAny<DownstreamRoute>()
            ).Returns(Task.CompletedTask).Verifiable();

            _middleware = mock.Object;
            return mock;
        }

        private void GivenUser(string authentication, string name, string role)
        {
            var user = new ClaimsPrincipal();
            user.AddIdentity(new(authentication, name, role));
            _httpContext.User = user;
        }

        private void AssertUsers(HttpContext actual)
        {
            Assert.NotNull(actual);
            Assert.Same(_httpContext.User, actual.User);
            Assert.NotNull(actual.User.Identity);
            var identity = _httpContext.User.Identity as ClaimsIdentity;
            var actualIdentity = actual.User.Identity as ClaimsIdentity;
            Assert.Equal(identity.AuthenticationType, actualIdentity.AuthenticationType);
            Assert.Equal(identity.NameClaimType, actualIdentity.NameClaimType);
            Assert.Equal(identity.RoleClaimType, actualIdentity.RoleClaimType);
        }

        private static Route GivenDefaultRoute(int count)
        {
            var b = new RouteBuilder();
            for (var i = 0; i < count; i++)
            {
                b.WithDownstreamRoute(new DownstreamRouteBuilder().Build());
            }

            return b.Build();
        }

        private void GivenTheFollowing(Route route)
        {
            _downstreamRoute = new Ocelot.DownstreamRouteFinder.DownstreamRouteHolder(new List<PlaceholderNameAndValue>(), route);
            _httpContext.Items.UpsertDownstreamRoute(_downstreamRoute);
        }

        private async Task WhenIMultiplex()
        {
            await _middleware.Invoke(_httpContext);
        }

        private void ThePipelineIsCalled(int expected)
        {
            _count.ShouldBe(expected);
        }
    }
}
