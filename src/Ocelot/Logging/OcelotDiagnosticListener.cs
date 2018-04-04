﻿using System;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DiagnosticAdapter;
using Butterfly.Client.AspNetCore;
using Butterfly.OpenTracing;
using Ocelot.Middleware;
using Butterfly.Client.Tracing;
using System.Linq;
using System.Collections.Generic;
using Ocelot.Infrastructure.Extensions;
using Microsoft.Extensions.Logging;
using Ocelot.Requester;

namespace Ocelot.Logging
{
    public class OcelotDiagnosticListener
    {
        private IServiceTracer _tracer;
        private IOcelotLogger _logger;

        public OcelotDiagnosticListener(IOcelotLoggerFactory factory, IServiceTracer tracer)
        {
            _tracer = tracer;
            _logger = factory.CreateLogger<OcelotDiagnosticListener>();
        }

        [DiagnosticName("Ocelot.MiddlewareException")]
        public virtual void OcelotMiddlewareException(Exception exception, DownstreamContext context, string name)
        {
            _logger.LogTrace("Ocelot.MiddlewareException: {name}; {Message};", name, exception.Message);
            Event(context.HttpContext, $"Ocelot.MiddlewareStarted: {name}; {context.HttpContext.Request.Path}");
        }

        [DiagnosticName("Ocelot.MiddlewareStarted")]
        public virtual void OcelotMiddlewareStarted(DownstreamContext context, string name)
        {
            _logger.LogTrace("Ocelot.MiddlewareStarted: {name}; {Path}", name, context.HttpContext.Request.Path);
            Event(context.HttpContext, $"Ocelot.MiddlewareStarted: {name}; {context.HttpContext.Request.Path}");
        }

        [DiagnosticName("Ocelot.MiddlewareFinished")]
        public virtual void OcelotMiddlewareFinished(DownstreamContext context, string name)
        {
            _logger.LogTrace("OcelotMiddlewareFinished: {name}; {Path}", name, context.HttpContext.Request.Path);
            Event(context.HttpContext, $"OcelotMiddlewareFinished: {name}; {context.HttpContext.Request.Path}");
        }

        [DiagnosticName("Microsoft.AspNetCore.MiddlewareAnalysis.MiddlewareStarting")]
        public virtual void OnMiddlewareStarting(HttpContext httpContext, string name)
        {
            //_logger.LogTrace("MiddlewareStarting: {name}; {Path}", name, httpContext.Request.Path);
            Event(httpContext, $"MiddlewareStarting: {name}; {httpContext.Request.Path}");
        }

        [DiagnosticName("Microsoft.AspNetCore.MiddlewareAnalysis.MiddlewareException")]
        public virtual void OnMiddlewareException(Exception exception, string name)
        {
            //_logger.LogTrace("MiddlewareException: {name}; {Message}", name, exception.Message);
        }

        [DiagnosticName("Microsoft.AspNetCore.MiddlewareAnalysis.MiddlewareFinished")]
        public virtual void OnMiddlewareFinished(HttpContext httpContext, string name)
        {
            //_logger.LogTrace("MiddlewareFinished: {name}; {StatusCode}", name, httpContext.Response.StatusCode);
            Event(httpContext, $"MiddlewareFinished: {name}; {httpContext.Response.StatusCode}");
        }

        private void Event(HttpContext httpContext, string @event)
        {  
            // Hack - if the user isnt using tracing the code gets here and will blow up on 
            // _tracer.Tracer.TryExtract. We already use the fake tracer for another scenario
            // so sticking it here as well..I guess we need a factory for this but no idea
            // how to hook that into the diagnostic framework at the moment.
            if(_tracer.GetType() == typeof(FakeServiceTracer))
            {
                return;
            }

            var span = httpContext.GetSpan();
            if(span == null)
            {
                var spanBuilder = new SpanBuilder($"server {httpContext.Request.Method} {httpContext.Request.Path}");
                if (_tracer.Tracer.TryExtract(out var spanContext, httpContext.Request.Headers, (c, k) => c[k].GetValue(),
                    c => c.Select(x => new KeyValuePair<string, string>(x.Key, x.Value.GetValue())).GetEnumerator()))
                {
                    spanBuilder.AsChildOf(spanContext);
                };
                span = _tracer.Start(spanBuilder);        
                httpContext.SetSpan(span);   
            }
            span?.Log(LogField.CreateNew().Event(@event));
        }
    }
}
