// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace Microsoft.AspNetCore.Watch.BrowserRefresh
{
    public class BrowserRefreshMiddleware
    {
        private static readonly MediaTypeHeaderValue _textHtmlMediaType = new MediaTypeHeaderValue("text/html");
        private readonly RequestDelegate _next;
        private readonly ILogger _logger;

        public BrowserRefreshMiddleware(RequestDelegate next, ILogger<BrowserRefreshMiddleware> logger) =>
            (_next, _logger) = (next, logger);

        public async Task InvokeAsync(HttpContext context)
        {
            // We only need to support this for requests that could be initiated by a browser.
            if (IsBrowserDocumentRequest(context))
            {
                // Use a custom StreamWrapper to rewrite output on Write/WriteAsync
                using var responseStreamWrapper = new ResponseStreamWrapper(context, _logger);
                var originalBodyFeature = context.Features.Get<IHttpResponseBodyFeature>();
                context.Features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(responseStreamWrapper));

                try
                {
                    await _next(context);
                }
                finally
                {
                    context.Features.Set(originalBodyFeature);
                }

                if (responseStreamWrapper.IsHtmlResponse)
                {
                    if (responseStreamWrapper.ScriptInjectionPerformed)
                    {
                        Log.BrowserConfiguredForRefreshes(_logger);
                    }
                    else if (context.Response.Headers.TryGetValue(HeaderNames.ContentEncoding, out var contentEncodings))
                    {
                        Log.ResponseCompressionDetected(_logger, contentEncodings);
                    }
                    else
                    {
                        Log.FailedToConfiguredForRefreshes(_logger);
                    }
                }
            }
            else
            {
                await _next(context);
            }
        }

        internal static bool IsBrowserDocumentRequest(HttpContext context)
        {
            var request = context.Request;
            if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsPost(request.Method))
            {
                return false;
            }

            if (request.Headers.TryGetValue("Sec-Fetch-Dest", out var values) &&
                !StringValues.IsNullOrEmpty(values) &&
                !string.Equals(values[0], "document", StringComparison.OrdinalIgnoreCase))
            {
                // See https://github.com/dotnet/aspnetcore/issues/37326.
                // Only inject scripts that are destined for a browser page.
                return false;
            }

            var typedHeaders = request.GetTypedHeaders();
            if (typedHeaders.Accept is not IList<MediaTypeHeaderValue> acceptHeaders)
            {
                return false;
            }

            for (var i = 0; i < acceptHeaders.Count; i++)
            {
                if (acceptHeaders[i].IsSubsetOf(_textHtmlMediaType))
                {
                    return true;
                }
            }

            return false;
        }

        internal static class Log
        {
            private static readonly Action<ILogger, Exception?> _setupResponseForBrowserRefresh = LoggerMessage.Define(
                LogLevel.Debug,
                new EventId(1, "SetUpResponseForBrowserRefresh"),
                "Response markup is scheduled to include browser refresh script injection.");

            private static readonly Action<ILogger, Exception?> _browserConfiguredForRefreshes = LoggerMessage.Define(
                LogLevel.Debug,
                new EventId(2, "BrowserConfiguredForRefreshes"),
                "Response markup was updated to include browser refresh script injection.");

            private static readonly Action<ILogger, Exception?> _failedToConfigureForRefreshes = LoggerMessage.Define(
                LogLevel.Warning,
                new EventId(3, "FailedToConfiguredForRefreshes"),
                "Unable to configure browser refresh script injection on the response. " +
                $"Consider manually adding '{WebSocketScriptInjection.InjectedScript}' to the body of the page.");

            private static readonly Action<ILogger, StringValues, Exception?> _responseCompressionDetected = LoggerMessage.Define<StringValues>(
                LogLevel.Warning,
                new EventId(4, "ResponseCompressionDetected"),
                "Unable to configure browser refresh script injection on the response. " +
                $"This may have been caused by the response's {HeaderNames.ContentEncoding}: '{{encoding}}'. " +
                "Consider disabling response compression.");

            public static void SetupResponseForBrowserRefresh(ILogger logger) => _setupResponseForBrowserRefresh(logger, null);
            public static void BrowserConfiguredForRefreshes(ILogger logger) => _browserConfiguredForRefreshes(logger, null);
            public static void FailedToConfiguredForRefreshes(ILogger logger) => _failedToConfigureForRefreshes(logger, null);
            public static void ResponseCompressionDetected(ILogger logger, StringValues encoding) => _responseCompressionDetected(logger, encoding, null);
        }
    }
}
