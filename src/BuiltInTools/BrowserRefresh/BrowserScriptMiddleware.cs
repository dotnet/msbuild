// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.AspNetCore.Http;

namespace Microsoft.AspNetCore.Watch.BrowserRefresh
{
    /// <summary>
    /// Responds with the contents of WebSocketScriptInjection.js with the stub WebSocket url replaced by the
    /// one specified by the launching app.
    /// </summary>
    public sealed class BrowserScriptMiddleware
    {
        private readonly byte[] _scriptBytes;
        private readonly string _contentLength;

        public BrowserScriptMiddleware(RequestDelegate next, byte[] scriptBytes)
        {
            _scriptBytes = scriptBytes;
            _contentLength = _scriptBytes.Length.ToString(CultureInfo.InvariantCulture);
        }

        public async Task InvokeAsync(HttpContext context)
        {
            context.Response.Headers["Cache-Control"] = "no-store";
            context.Response.Headers["Content-Length"] = _contentLength;
            context.Response.Headers["Content-Type"] = "application/javascript; charset=utf-8";

            await context.Response.Body.WriteAsync(_scriptBytes.AsMemory(), context.RequestAborted);
        }

        internal static byte[] GetBlazorHotReloadJS()
        {
            var jsFileName = "Microsoft.AspNetCore.Watch.BrowserRefresh.BlazorHotReload.js";
            using var stream = new MemoryStream();
            var manifestStream = typeof(WebSocketScriptInjection).Assembly.GetManifestResourceStream(jsFileName)!;
            manifestStream.CopyTo(stream);

            return stream.ToArray();
        }

        internal static byte[] GetBrowserRefreshJS()
        {
            var endpoint = Environment.GetEnvironmentVariable("ASPNETCORE_AUTO_RELOAD_WS_ENDPOINT")!;
            var serverKey = Environment.GetEnvironmentVariable("ASPNETCORE_AUTO_RELOAD_WS_KEY") ?? string.Empty;

            return GetWebSocketClientJavaScript(endpoint, serverKey);
        }

        internal static byte[] GetWebSocketClientJavaScript(string hostString, string serverKey)
        {
            var jsFileName = "Microsoft.AspNetCore.Watch.BrowserRefresh.WebSocketScriptInjection.js";
            using var reader = new StreamReader(typeof(WebSocketScriptInjection).Assembly.GetManifestResourceStream(jsFileName)!);
            var script = reader.ReadToEnd()
                .Replace("{{hostString}}", hostString)
                .Replace("{{ServerKey}}", serverKey);

            return Encoding.UTF8.GetBytes(script);
        }
    }
}
