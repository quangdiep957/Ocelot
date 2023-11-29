﻿using Microsoft.Extensions.DependencyInjection;

namespace Ocelot.Requester;

public static class ServiceCollectionExtensions
{
    public static void AddOcelotHttpClient(this IServiceCollection services)
    {
        services.AddSingleton<IMessageInvokerPool, MessageInvokerPool>();
    }
}
