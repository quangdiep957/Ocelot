using System;
using System.Collections.Generic;
using System.Net.Http;
using Ocelot.Requester;
using Shouldly;
using TestStack.BDDfy;
using Xunit;

namespace Ocelot.UnitTests.Requester
{
    public class DelegatingHandlerHandlerProviderTests
    {
        private DelegatingHandlerHandlerProvider _provider;
        private List<Func<DelegatingHandler>> _handlers;

        public DelegatingHandlerHandlerProviderTests()
        {
            _provider = new DelegatingHandlerHandlerProvider();
        }

        [Fact]
        public void should_get_delegating_handlers_in_order_last_one_in_last_one_out()
        {   
            this.Given(x => GivenTheHandlers())
                .When(x => WhenIGet())
                .Then(x => ThenTheHandlersAreReturnedInOrder())
                .BDDfy();
        }

        private void ThenTheHandlersAreReturnedInOrder()
        {
             var handler = (FakeDelegatingHandler)_handlers[0].Invoke();
            handler.Order.ShouldBe(0);
            handler = (FakeDelegatingHandler)_handlers[1].Invoke();
            handler.Order.ShouldBe(1);
        }

        private void WhenIGet()
        {
            _handlers = _provider.Get();
        }

        private void GivenTheHandlers()
        {
            _provider.Add(() => new FakeDelegatingHandler(0));
            _provider.Add(() => new FakeDelegatingHandler(1));
        }
    }
}