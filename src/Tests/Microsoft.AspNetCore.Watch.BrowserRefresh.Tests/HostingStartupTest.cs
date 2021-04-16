// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

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

        private static RequestDelegate GetRequestDelegate()
        {
            var action = new HostingStartup().Configure(builder =>
            {
                builder.Run(context =>
                {
                    context.Response.StatusCode = StatusCodes.Status418ImATeapot;
                    return Task.CompletedTask;
                });
            });

            var serviceProvider = new ServiceCollection()
                .AddLogging()
                .BuildServiceProvider();
            var builder = new ApplicationBuilder(serviceProvider);
            action(builder);
            return builder.Build();
        }
    }
}
