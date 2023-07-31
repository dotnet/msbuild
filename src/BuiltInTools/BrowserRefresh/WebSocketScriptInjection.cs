// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Watch.BrowserRefresh
{
    /// <summary>
    /// Helper class that handles the HTML injection into
    /// a string or byte array.
    /// </summary>
    public static class WebSocketScriptInjection
    {
        private const string BodyMarker = "</body>";

        private static readonly byte[] _bodyBytes = Encoding.UTF8.GetBytes(BodyMarker);

        internal static string InjectedScript { get; } = $"<script src=\"{ApplicationPaths.BrowserRefreshJS}\"></script>";

        private static readonly byte[] _injectedScriptBytes = Encoding.UTF8.GetBytes(InjectedScript);

        public static bool TryInjectLiveReloadScript(Stream baseStream, ReadOnlySpan<byte> buffer)
        {
            var index = buffer.LastIndexOf(_bodyBytes);
            if (index == -1)
            {
                baseStream.Write(buffer);
                return false;
            }

            if (index > 0)
            {
                baseStream.Write(buffer.Slice(0, index));
                buffer = buffer[index..];
            }

            // Write the injected script
            baseStream.Write(_injectedScriptBytes);

            // Write the rest of the buffer/HTML doc
            baseStream.Write(buffer);
            return true;
        }

        public static async ValueTask<bool> TryInjectLiveReloadScriptAsync(Stream baseStream, ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var index = buffer.Span.LastIndexOf(_bodyBytes);
            if (index == -1)
            {
                await baseStream.WriteAsync(buffer, cancellationToken);
                return false;
            }

            if (index > 0)
            {
                await baseStream.WriteAsync(buffer.Slice(0, index), cancellationToken);
                buffer = buffer[index..];
            }

            // Write the injected script
            await baseStream.WriteAsync(_injectedScriptBytes, cancellationToken);

            // Write the rest of the buffer/HTML doc
            await baseStream.WriteAsync(buffer, cancellationToken);
            return true;
        }

    }
}
