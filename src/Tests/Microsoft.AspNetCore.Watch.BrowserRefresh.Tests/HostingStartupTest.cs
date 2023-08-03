// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.Watch.BrowserRefresh
{
    public class HostingStartupTest
    {
        [Fact]
        public async Task ClearSiteDataWorks()
        {
            // Arrange
            var requestDelegate = GetRequestDelegate();
            var context = new DefaultHttpContext();
            context.Request.Path = "/_framework/clear-browser-cache";

            // Act
            await requestDelegate(context);

            // Assert
            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
            Assert.Equal("\"cache\"", context.Response.Headers["Clear-Site-Data"]);
        }

        [Fact]
        public async Task GetBlazorHotReloadMiddlewareWorks()
        {
            // Arrange
            var requestDelegate = GetRequestDelegate();
            var context = new DefaultHttpContext();
            context.Request.Method = "GET";
            context.Request.Path = "/_framework/blazor-hotreload";
            context.Response.Body = new MemoryStream();

            // Act
            await requestDelegate(context);

            // Assert
            Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);
        }

        [Fact]
        public async Task PostBlazorHotReloadMiddlewareWorks()
        {
            // Arrange
            var requestDelegate = GetRequestDelegate();
            var context = new DefaultHttpContext();
            context.Request.Path = "/_framework/blazor-hotreload";
            context.Request.Method = "POST";
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("[]"));

            // Act
            await requestDelegate(context);

            // Assert
            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        }

        [Fact]
        public async Task GetBlazorHotReloadJsWorks()
        {
            // Arrange
            var requestDelegate = GetRequestDelegate();
            var context = new DefaultHttpContext();
            context.Request.Path = "/_framework/blazor-hotreload.js";
            var responseBody = new MemoryStream();
            context.Response.Body = responseBody;

            // Act
            await requestDelegate(context);

            // Assert
            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
            Assert.NotEmpty(responseBody.ToArray());
        }

        [Fact]
        public async Task GetAspNetCoreBrowserRefreshWorks()
        {
            // Arrange
            var requestDelegate = GetRequestDelegate();
            var context = new DefaultHttpContext();
            context.Request.Path = "/_framework/aspnetcore-browser-refresh.js";
            var responseBody = new MemoryStream();
            context.Response.Body = responseBody;

            // Act
            await requestDelegate(context);

            // Assert
            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
            Assert.NotEmpty(responseBody.ToArray());
        }

        [Fact]
        public async Task GetUnknownUrlWorks()
        {
            // Arrange
            var requestDelegate = GetRequestDelegate();
            var context = new DefaultHttpContext();
            context.Request.Path = "/someurl";

            // Act
            await requestDelegate(context);

            // Assert
            Assert.Equal(StatusCodes.Status418ImATeapot, context.Response.StatusCode);
        }

        [Fact]
        public async Task GetUnknownFrameworkPathWorks()
        {
            // Arrange
            var requestDelegate = GetRequestDelegate(builder =>
            {
                builder.Use((context, next) =>
                {
                    var path = context.Request.Path;
                    if (path == "/_framework/blazor.webassembly.js")
                    {
                        context.Response.StatusCode = StatusCodes.Status206PartialContent;
                        return Task.CompletedTask;
                    }
                    else if (path == "/_framework/System.dll")
                    {
                        context.Response.StatusCode = StatusCodes.Status226IMUsed;
                        return Task.CompletedTask;
                    }

                    return next();
                });
            });

            var context = new DefaultHttpContext();
            context.Request.Path = "/_framework/blazor.webassembly.js";

            // Act
            await requestDelegate(context);

            // Assert
            Assert.Equal(StatusCodes.Status206PartialContent, context.Response.StatusCode);


            // Act - 2
            context.Request.Path = "/_framework/System.dll";
            await requestDelegate(context);

            // Assert
            Assert.Equal(StatusCodes.Status226IMUsed, context.Response.StatusCode);
        }

        private static RequestDelegate GetRequestDelegate(Action<IApplicationBuilder>? configureBuilder = null)
        {
            configureBuilder ??= static builder =>
            {
                builder.Run(context =>
                {
                    context.Response.StatusCode = StatusCodes.Status418ImATeapot;
                    return Task.CompletedTask;
                });
            };

            var action = new HostingStartup().Configure(configureBuilder);

            var serviceProvider = new ServiceCollection()
                .AddLogging()
                .BuildServiceProvider();
            var builder = new ApplicationBuilder(serviceProvider);
            action(builder);
            return builder.Build();
        }
    }
}
