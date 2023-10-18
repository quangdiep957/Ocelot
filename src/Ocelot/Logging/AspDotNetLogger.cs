using Microsoft.Extensions.Logging;
using Ocelot.Infrastructure.RequestData;

namespace Ocelot.Logging
{
    public class AspDotNetLogger : IOcelotLogger
    {
        private readonly ILogger _logger;
        private readonly IRequestScopedDataRepository _scopedDataRepository;
        private readonly Func<string, Exception, string> _func;

        public AspDotNetLogger(ILogger logger, IRequestScopedDataRepository scopedDataRepository)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _scopedDataRepository = scopedDataRepository ?? throw new ArgumentNullException(nameof(scopedDataRepository));
            _func = (state, exception) => exception == null ? state : $"{state}, exception: {exception}";
        }

        public void LogTrace(Func<string> messageFactory)
        {
            WriteLog(LogLevel.Trace, messageFactory);
        }

        public void LogDebug(Func<string> messageFactory)
        {
            WriteLog(LogLevel.Debug, messageFactory);
        }

        public void LogInformation(Func<string> messageFactory)
        {
            WriteLog(LogLevel.Information, messageFactory);
        }

        public void LogWarning(Func<string> messageFactory)
        {
            WriteLog(LogLevel.Warning, messageFactory);
        }

        public void LogError(Func<string> messageFactory, Exception exception)
        {
            WriteLog(LogLevel.Error, messageFactory, exception);
        }

        public void LogCritical(Func<string> messageFactory, Exception exception)
        {
            WriteLog(LogLevel.Critical, messageFactory, exception);
        }

        private string GetOcelotRequestId()
        {
            var requestId = _scopedDataRepository.Get<string>("RequestId");

            return requestId == null || requestId.IsError ? "no request id" : requestId.Data;
        }

        private string GetOcelotPreviousRequestId()
        {
            var requestId = _scopedDataRepository.Get<string>("PreviousRequestId");

            return requestId == null || requestId.IsError ? "no previous request id" : requestId.Data;
        }

        private void WriteLog(LogLevel logLevel, Func<string> messageFactory, Exception exception = null)
        {
            if (!_logger.IsEnabled(logLevel))
            {
                return;
            }

            var requestId = GetOcelotRequestId();
            var previousRequestId = GetOcelotPreviousRequestId();

            var state = $"requestId: {requestId}, previousRequestId: {previousRequestId}, message: {messageFactory.Invoke()}";

            _logger.Log(logLevel, default, state, exception, _func);
        }
    }
}
