// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace Microsoft.AspNetCore.Watch.BrowserRefresh
{
    /// <summary>
    /// A middleware that manages receiving and sending deltas from a BlazorWebAssembly app.
    /// This assembly is shared between Visual Studio and dotnet-watch. By putting some of the complexity
    /// in here, we can avoid duplicating work in watch and VS.
    /// </summary>
    internal sealed class BlazorWasmHotReloadMiddleware
    {
        private readonly object @lock = new();
        private readonly string EtagDiscriminator = Guid.NewGuid().ToString();
        private readonly JsonSerializerOptions _jsonSerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public BlazorWasmHotReloadMiddleware(RequestDelegate next)
        {
        }

        internal List<UpdateDelta> Deltas { get; } = new();

        public Task InvokeAsync(HttpContext context)
        {
            // Multiple instances of the BlazorWebAssembly app could be running (multiple tabs or multiple browsers).
            // We want to avoid serialize reads and writes between then
            lock (@lock)
            {
                if (HttpMethods.IsGet(context.Request.Method))
                {
                    return OnGet(context);
                }
                else if (HttpMethods.IsPost(context.Request.Method))
                {
                    return OnPost(context);
                }
                else
                {
                    context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                    return Task.CompletedTask;
                }
            }

            // Don't call next(). This middleware is terminal.
        }

        private async Task OnGet(HttpContext context)
        {
            if (Deltas.Count == 0)
            {
                context.Response.StatusCode = StatusCodes.Status204NoContent;
                return;
            }

            if (EtagMatches(context))
            {
                context.Response.StatusCode = StatusCodes.Status304NotModified;
                return;
            }

            WriteETag(context);
            await JsonSerializer.SerializeAsync(context.Response.Body, Deltas, _jsonSerializerOptions);
        }

        private bool EtagMatches(HttpContext context)
        {
            if (context.Request.Headers[HeaderNames.IfNoneMatch] is not { Count: 1 } ifNoneMatch)
            {
                return false;
            }

            var expected = GetETag();
            return string.Equals(expected, ifNoneMatch[0], StringComparison.Ordinal);
        }

        private async Task OnPost(HttpContext context)
        {
            var updateDeltas = await JsonSerializer.DeserializeAsync<UpdateDelta[]>(context.Request.Body, _jsonSerializerOptions);
            AppendDeltas(updateDeltas);

            WriteETag(context);
        }

        private void WriteETag(HttpContext context)
        {
            var etag = GetETag();
            if (etag is not null)
            {
                context.Response.Headers[HeaderNames.ETag] = etag;
            }
        }

        private string? GetETag()
        {
            if (Deltas.Count == 0)
            {
                return null;
            }

            return string.Format(CultureInfo.InvariantCulture, "W/\"{0}{1}\"", EtagDiscriminator, Deltas[^1].SequenceId);
        }

        private void AppendDeltas(UpdateDelta[]? updateDeltas)
        {
            if (updateDeltas == null || updateDeltas.Length == 0)
            {
                return;
            }

            // It's possible that multiple instances of the BlazorWasm are simultaneously executing and could be posting the same deltas
            // We'll use the sequence id to ensure that we're not recording duplicate entries. Replaying duplicated values would cause
            // ApplyDelta to fail.
            // It's currently not possible to receive different ranges of sequences from different clients (for e.g client 1 sends deltas 1 - 3,
            // and client 2 sends deltas 2 - 4, client 3 sends 1 - 5 etc), so we only need to verify that the first item in the sequence has not already been seen.
            if (Deltas.Count == 0 || Deltas[^1].SequenceId < updateDeltas[0].SequenceId)
            {
                Deltas.AddRange(updateDeltas);
            }
        }

        internal class UpdateDelta
        {
            public int SequenceId { get; set; }
            public string ModuleId { get; set; } = default!;
            public string MetadataDelta { get; set; } = default!;
            public string ILDelta { get; set; } = default!;
        }
    }
}
