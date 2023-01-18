// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
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
                // Use a custom stream to buffer the response body for rewriting.
                using var memoryStream = new MemoryStream();
                var originalBodyFeature = context.Features.Get<IHttpResponseBodyFeature>();
                context.Features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(memoryStream));

                try
                {
                    await _next(context);
                }
                finally
                {
                    context.Features.Set(originalBodyFeature);
                }

                if (memoryStream.TryGetBuffer(out var buffer) && buffer.Count > 0)
                {
                    var response = context.Response;
                    var baseStream = response.Body;

                    if (IsHtmlResponse(response))
                    {
                        Log.SetupResponseForBrowserRefresh(_logger);

                        // Since we're changing the markup content, reset the content-length
                        response.Headers.ContentLength = null;

                        var scriptInjectionPerformed = await WebSocketScriptInjection.TryInjectLiveReloadScriptAsync(baseStream, buffer);
                        if (scriptInjectionPerformed)
                        {
                            Log.BrowserConfiguredForRefreshes(_logger);
                        }
                        else if (response.Headers.TryGetValue(HeaderNames.ContentEncoding, out var contentEncodings))
                        {
                            Log.ResponseCompressionDetected(_logger, contentEncodings);
                        }
                        else
                        {
                            Log.FailedToConfiguredForRefreshes(_logger);
                        }
                    }
                    else
                    {
                        await baseStream.WriteAsync(buffer);
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

        private bool IsHtmlResponse(HttpResponse response)
            => (response.StatusCode == StatusCodes.Status200OK || response.StatusCode == StatusCodes.Status500InternalServerError) &&
                MediaTypeHeaderValue.TryParse(response.ContentType, out var mediaType) &&
                mediaType.IsSubsetOf(_textHtmlMediaType) &&
                (!mediaType.Charset.HasValue || mediaType.Charset.Equals("utf-8", StringComparison.OrdinalIgnoreCase));

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
