// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Microsoft.AspNetCore.Watch.BrowserRefresh
{
    public class BrowserRefreshMiddlewareTest
    {
        [Theory]
        [InlineData("DELETE")]
        [InlineData("head")]
        [InlineData("Put")]
        public void IsBrowserDocumentRequest_ReturnsFalse_ForNonGetOrPostRequests(string method)
        {
            // Arrange
            var context = new DefaultHttpContext
            {
                Request =
                {
                    Method = method,
                    Headers =
                    {
                        ["Accept"] = "application/html",
                    },
                },
            };

            // Act
            var result = BrowserRefreshMiddleware.IsBrowserDocumentRequest(context);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsBrowserDocumentRequest_ReturnsFalse_IsRequestDoesNotAcceptHtml()
        {
            // Arrange
            var context = new DefaultHttpContext
            {
                Request =
                {
                    Method = "GET",
                    Headers =
                    {
                        ["Accept"] = "application/xml",
                    },
                },
            };

            // Act
            var result = BrowserRefreshMiddleware.IsBrowserDocumentRequest(context);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsBrowserDocumentRequest_ReturnsTrue_ForGetRequestsThatAcceptHtml()
        {
            // Arrange
            var context = new DefaultHttpContext
            {
                Request =
                {
                    Method = "GET",
                    Headers =
                    {
                        ["Accept"] = "application/json,text/html;q=0.9",
                    },
                },
            };

            // Act
            var result = BrowserRefreshMiddleware.IsBrowserDocumentRequest(context);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsBrowserDocumentRequest_ReturnsTrue_ForRequestsThatAcceptAnyHtml()
        {
            // Arrange
            var context = new DefaultHttpContext
            {
                Request =
                {
                    Method = "Post",
                    Headers =
                    {
                        ["Accept"] = "application/json,text/*+html;q=0.9",
                    },
                },
            };

            // Act
            var result = BrowserRefreshMiddleware.IsBrowserDocumentRequest(context);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsBrowserDocumentRequest_ReturnsTrue_IfRequestDoesNotHaveFetchMetadataRequestHeader()
        {
            // Arrange
            var context = new DefaultHttpContext
            {
                Request =
                {
                    Method = "GET",
                    Headers =
                    {
                        ["Accept"] = "text/html",
                    },
                },
            };

            // Act
            var result = BrowserRefreshMiddleware.IsBrowserDocumentRequest(context);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsBrowserDocumentRequest_ReturnsTrue_IfRequestFetchMetadataRequestHeaderIsEmpty()
        {
            // Arrange
            var context = new DefaultHttpContext
            {
                Request =
                {
                    Method = "Post",
                    Headers =
                    {
                        ["Accept"] = "text/html",
                        ["Sec-Fetch-Dest"] = string.Empty,
                    },
                },
            };

            // Act
            var result = BrowserRefreshMiddleware.IsBrowserDocumentRequest(context);

            // Assert
            Assert.True(result);
        }

        [Theory]
        [InlineData("document")]
        [InlineData("Document")]
        public void IsBrowserDocumentRequest_ReturnsTrue_IfRequestFetchMetadataRequestHeaderIsDocument(string headerValue)
        {
            // Arrange
            var context = new DefaultHttpContext
            {
                Request =
                {
                    Method = "Post",
                    Headers =
                    {
                        ["Accept"] = "text/html",
                        ["Sec-Fetch-Dest"] = headerValue,
                    },
                },
            };

            // Act
            var result = BrowserRefreshMiddleware.IsBrowserDocumentRequest(context);

            // Assert
            Assert.True(result);
        }

        [Theory]
        [InlineData("frame")]
        [InlineData("iframe")]
        [InlineData("serviceworker")]
        public void IsBrowserDocumentRequest_ReturnsFalse_IfRequestFetchMetadataRequestHeaderIsNotDocument(string headerValue)
        {
            // Arrange
            var context = new DefaultHttpContext
            {
                Request =
                {
                    Method = "Post",
                    Headers =
                    {
                        ["Accept"] = "text/html",
                        ["Sec-Fetch-Dest"] = headerValue,
                    },
                },
            };

            // Act
            var result = BrowserRefreshMiddleware.IsBrowserDocumentRequest(context);

            // Assert
            Assert.False(result);
        }

        [Theory]
        [MemberData(nameof(ClosingBodyTagParts))]
        public async Task InvokeAsync_AddsScriptToThePage(string[] closingBodyTagParts)
        {
            // Arrange
            var stream = new MemoryStream();
            var context = new DefaultHttpContext
            {
                Request =
                {
                    Method = "GET",
                    Headers = { ["Accept"] = "text/html" },
                },
                Response =
                {
                    Body = stream
                },
            };

            var middleware = new BrowserRefreshMiddleware(async (context) =>
            {
                context.Response.ContentType = "text/html";

                await context.Response.WriteAsync("<html>");
                await context.Response.WriteAsync("<body>");
                await context.Response.WriteAsync("<h1>");
                await context.Response.WriteAsync("Hello world");
                await context.Response.WriteAsync("</h1>");
                foreach (var part in closingBodyTagParts)
                {
                    await context.Response.WriteAsync(part);
                }
                await context.Response.WriteAsync("</html>");
            }, NullLogger<BrowserRefreshMiddleware>.Instance);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            var responseContent = Encoding.UTF8.GetString(stream.ToArray());
            Assert.Equal("<html><body><h1>Hello world</h1><script src=\"/_framework/aspnetcore-browser-refresh.js\"></script></body></html>", responseContent);
        }

        public static IEnumerable<object[]> ClosingBodyTagParts => new[]
        {
            new[] { new[] { "</body>" } },
            new[] { new[] { "</", "body>" } },
            new[] { new[] { "<", "/bo", "d", "y>" } },
        };
    }
}
