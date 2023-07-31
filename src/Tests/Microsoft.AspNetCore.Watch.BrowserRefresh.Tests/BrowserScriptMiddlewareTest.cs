// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Http;

namespace Microsoft.AspNetCore.Watch.BrowserRefresh
{
    public class BrowserScriptMiddlewareTest
    {
        private readonly RequestDelegate _next = (context) => Task.CompletedTask;

        [Fact]
        public async Task InvokeAsync_ReturnsScript()
        {
            // Arrange
            var context = new DefaultHttpContext();
            var stream = new MemoryStream();
            context.Response.Body = stream;
            var middleware = new BrowserScriptMiddleware(_next, BrowserScriptMiddleware.GetWebSocketClientJavaScript("some-host", "test-key"));

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            stream.Position = 0;
            var script = Encoding.UTF8.GetString(stream.ToArray());
            Assert.Contains("// dotnet-watch browser reload script", script);
            Assert.Contains("'some-host'", script);
            Assert.Contains("'test-key'", script);
        }

        [Fact]
        public async Task InvokeAsync_ConfiguresHeaders()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();
            var middleware = new BrowserScriptMiddleware(_next, BrowserScriptMiddleware.GetWebSocketClientJavaScript("some-host", "test-key"));

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            var response = context.Response;
            Assert.Collection(
                response.Headers.OrderBy(h => h.Key),
                kvp =>
                {
                    Assert.Equal("Cache-Control", kvp.Key);
                    Assert.Equal("no-store", kvp.Value);
                },
                kvp =>
                {
                    Assert.Equal("Content-Length", kvp.Key);
                    Assert.NotEmpty(kvp.Value);
                },
                kvp =>
                {
                    Assert.Equal("Content-Type", kvp.Key);
                    Assert.Equal("application/javascript; charset=utf-8", kvp.Value);
                });
        }
    }
}
