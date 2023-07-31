// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace Microsoft.AspNetCore.Watch.BrowserRefresh
{
    public class BlazorWasmHotReloadMiddlewareTest
    {
        [Fact]
        public async Task DeltasAreSavedOnPost()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Request.Method = "post";
            var deltas = new[]
            {
                new BlazorWasmHotReloadMiddleware.UpdateDelta
                {
                    SequenceId = 0,
                    ModuleId = Guid.NewGuid().ToString(),
                    ILDelta = "ILDelta1",
                    MetadataDelta = "MetadataDelta1",
                },
                new BlazorWasmHotReloadMiddleware.UpdateDelta
                {
                    SequenceId = 1,
                    ModuleId = Guid.NewGuid().ToString(),
                    ILDelta = "ILDelta2",
                    MetadataDelta = "MetadataDelta2",
                }
            };
            context.Request.Body = GetJson(deltas);

            var middleware = new BlazorWasmHotReloadMiddleware(context => throw new TimeZoneNotFoundException());

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            AssertDeltas(deltas, middleware.Deltas);
            Assert.NotEmpty(context.Response.Headers["ETag"]);
        }

        [Fact]
        public async Task DuplicateDeltasOnPostAreIgnored()
        {
            // Arrange
            var deltas = new[]
            {
                new BlazorWasmHotReloadMiddleware.UpdateDelta
                {
                    SequenceId = 0,
                    ModuleId = Guid.NewGuid().ToString(),
                    ILDelta = "ILDelta1",
                    MetadataDelta = "MetadataDelta1",
                },
                new BlazorWasmHotReloadMiddleware.UpdateDelta
                {
                    SequenceId = 1,
                    ModuleId = Guid.NewGuid().ToString(),
                    ILDelta = "ILDelta2",
                    MetadataDelta = "MetadataDelta2",
                }
            };
            var context = new DefaultHttpContext();
            context.Request.Method = "post";
            context.Request.Body = GetJson(deltas);

            var middleware = new BlazorWasmHotReloadMiddleware(context => throw new TimeZoneNotFoundException());

            // Act 1
            await middleware.InvokeAsync(context);

            // Act 2
            context = new DefaultHttpContext();
            context.Request.Method = "post";
            context.Request.Body = GetJson(deltas);
            await middleware.InvokeAsync(context);

            // Assert
            AssertDeltas(deltas, middleware.Deltas);
            Assert.NotEmpty(context.Response.Headers["ETag"]);
        }

        [Fact]
        public async Task MultipleDeltaPayloadsCanBeAccepted()
        {
            // Arrange
            var deltas = new List<BlazorWasmHotReloadMiddleware.UpdateDelta>
            {
                new BlazorWasmHotReloadMiddleware.UpdateDelta
                {
                    SequenceId = 0,
                    ModuleId = Guid.NewGuid().ToString(),
                    ILDelta = "ILDelta1",
                    MetadataDelta = "MetadataDelta1",
                },
                new BlazorWasmHotReloadMiddleware.UpdateDelta
                {
                    SequenceId = 1,
                    ModuleId = Guid.NewGuid().ToString(),
                    ILDelta = "ILDelta2",
                    MetadataDelta = "MetadataDelta2",
                }
            };
            var context = new DefaultHttpContext();
            context.Request.Method = "post";
            context.Request.Body = GetJson(deltas);

            var middleware = new BlazorWasmHotReloadMiddleware(context => throw new TimeZoneNotFoundException());

            // Act 1
            await middleware.InvokeAsync(context);

            // Act 2
            var newDeltas = new[]
            {
                new BlazorWasmHotReloadMiddleware.UpdateDelta
                {
                    SequenceId = 3,
                    ModuleId = Guid.NewGuid().ToString(),
                    ILDelta = "ILDelta3",
                    MetadataDelta = "MetadataDelta3",
                },
                new BlazorWasmHotReloadMiddleware.UpdateDelta
                {
                    SequenceId = 4,
                    ModuleId = Guid.NewGuid().ToString(),
                    ILDelta = "ILDelta4",
                    MetadataDelta = "MetadataDelta4",
                },
                    new BlazorWasmHotReloadMiddleware.UpdateDelta
                {
                    SequenceId = 5,
                    ModuleId = Guid.NewGuid().ToString(),
                    ILDelta = "ILDelta5",
                    MetadataDelta = "MetadataDelta5",
                },
            };

            context = new DefaultHttpContext();
            context.Request.Method = "post";
            context.Request.Body = GetJson(newDeltas);
            await middleware.InvokeAsync(context);

            // Assert
            deltas.AddRange(newDeltas);
            AssertDeltas(deltas, middleware.Deltas);
            Assert.NotEmpty(context.Response.Headers["ETag"]);
        }

        [Fact]
        public async Task Get_Returns204_IfNoDeltasPresent()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Request.Method = "get";
            var middleware = new BlazorWasmHotReloadMiddleware(context => throw new TimeZoneNotFoundException());

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.Equal(204, context.Response.StatusCode);
        }

        [Fact]
        public async Task GetReturnsDeltas()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Request.Method = "get";
            var stream = new MemoryStream();
            context.Response.Body = stream;
            var middleware = new BlazorWasmHotReloadMiddleware(context => throw new TimeZoneNotFoundException());
            var deltas = new List<BlazorWasmHotReloadMiddleware.UpdateDelta>
            {
                new BlazorWasmHotReloadMiddleware.UpdateDelta
                {
                    SequenceId = 0,
                    ModuleId = Guid.NewGuid().ToString(),
                    ILDelta = "ILDelta1",
                    MetadataDelta = "MetadataDelta1",
                },
                new BlazorWasmHotReloadMiddleware.UpdateDelta
                {
                    SequenceId = 1,
                    ModuleId = Guid.NewGuid().ToString(),
                    ILDelta = "ILDelta2",
                    MetadataDelta = "MetadataDelta2",
                }
            };
            middleware.Deltas.AddRange(deltas);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.Equal(200, context.Response.StatusCode);
            Assert.Equal(
                JsonSerializer.SerializeToUtf8Bytes(deltas, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                stream.ToArray());
            Assert.NotEmpty(context.Response.Headers[HeaderNames.ETag]);
        }

        [Fact]
        public async Task GetReturnsNotModified_IfNoneMatchApplies()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Request.Method = "get";
            var middleware = new BlazorWasmHotReloadMiddleware(context => throw new TimeZoneNotFoundException());
            var deltas = new List<BlazorWasmHotReloadMiddleware.UpdateDelta>
            {
                new BlazorWasmHotReloadMiddleware.UpdateDelta
                {
                    SequenceId = 0,
                    ModuleId = Guid.NewGuid().ToString(),
                    ILDelta = "ILDelta1",
                    MetadataDelta = "MetadataDelta1",
                },
                new BlazorWasmHotReloadMiddleware.UpdateDelta
                {
                    SequenceId = 1,
                    ModuleId = Guid.NewGuid().ToString(),
                    ILDelta = "ILDelta2",
                    MetadataDelta = "MetadataDelta2",
                }
            };
            middleware.Deltas.AddRange(deltas);

            // Act 1
            await middleware.InvokeAsync(context);
            var etag = context.Response.Headers[HeaderNames.ETag];

            // Act 2
            context = new DefaultHttpContext();
            context.Request.Method = "get";
            context.Request.Headers[HeaderNames.IfNoneMatch] = etag;

            await middleware.InvokeAsync(context);

            // Assert 2
            Assert.Equal(StatusCodes.Status304NotModified, context.Response.StatusCode);
        }

        [Fact]
        public async Task GetReturnsUpdatedResults_IfNoneMatchFails()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Request.Method = "get";
            var middleware = new BlazorWasmHotReloadMiddleware(context => throw new TimeZoneNotFoundException());
            var deltas = new List<BlazorWasmHotReloadMiddleware.UpdateDelta>
            {
                new BlazorWasmHotReloadMiddleware.UpdateDelta
                {
                    SequenceId = 0,
                    ModuleId = Guid.NewGuid().ToString(),
                    ILDelta = "ILDelta1",
                    MetadataDelta = "MetadataDelta1",
                },
                new BlazorWasmHotReloadMiddleware.UpdateDelta
                {
                    SequenceId = 1,
                    ModuleId = Guid.NewGuid().ToString(),
                    ILDelta = "ILDelta2",
                    MetadataDelta = "MetadataDelta2",
                }
            };
            middleware.Deltas.AddRange(deltas);

            // Act 1
            await middleware.InvokeAsync(context);
            var etag = context.Response.Headers[HeaderNames.ETag];

            // Act 2
            var update = new BlazorWasmHotReloadMiddleware.UpdateDelta
            {
                SequenceId = 3,
                ModuleId = Guid.NewGuid().ToString(),
                ILDelta = "ILDelta3",
                MetadataDelta = "MetadataDelta3",
            };
            deltas.Add(update);
            middleware.Deltas.Add(update);
            context = new DefaultHttpContext();
            context.Request.Method = "get";
            context.Request.Headers[HeaderNames.IfNoneMatch] = etag;
            var stream = new MemoryStream();
            context.Response.Body = stream;

            await middleware.InvokeAsync(context);

            // Assert 2
            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
            Assert.Equal(
                JsonSerializer.SerializeToUtf8Bytes(deltas, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                stream.ToArray());
            Assert.NotEqual(etag, context.Response.Headers[HeaderNames.ETag]);
        }

        private static void AssertDeltas(IReadOnlyList<BlazorWasmHotReloadMiddleware.UpdateDelta> expected, IReadOnlyList<BlazorWasmHotReloadMiddleware.UpdateDelta> actual)
        {
            Assert.Equal(expected.Count, actual.Count);

            for (var i = 0; i < expected.Count; i++)
            {
                Assert.Equal(expected[i].ILDelta, actual[i].ILDelta);
                Assert.Equal(expected[i].MetadataDelta, actual[i].MetadataDelta);
                Assert.Equal(expected[i].ModuleId, actual[i].ModuleId);
                Assert.Equal(expected[i].SequenceId, actual[i].SequenceId);
            }
        }

        private Stream GetJson(IReadOnlyList<BlazorWasmHotReloadMiddleware.UpdateDelta> deltas)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(deltas, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return new MemoryStream(bytes);
        }
    }
}
