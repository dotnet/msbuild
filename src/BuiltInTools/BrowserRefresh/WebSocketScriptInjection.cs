// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.Watch.BrowserRefresh
{
    /// <summary>
    /// Helper class that handles the HTML injection into
    /// a string or byte array.
    /// </summary>
    public static class WebSocketScriptInjection
    {
        private static readonly byte[] s_closingTagStartIndicator = "</"u8.ToArray();
        private static readonly byte[] s_bodyElementName = "body"u8.ToArray();
        private static readonly byte[] s_closingTagEndIndicator = ">"u8.ToArray();

        internal static string InjectedScript { get; } = $"<script src=\"{ApplicationPaths.BrowserRefreshJS}\"></script>";

        private static readonly byte[] s_injectedScriptBytes = Encoding.UTF8.GetBytes(InjectedScript);

        public static async ValueTask<bool> TryInjectLiveReloadScriptAsync(Stream baseStream, ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var index = LastIndexOfClosingBodyTag(buffer.Span);
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
            await baseStream.WriteAsync(s_injectedScriptBytes, cancellationToken);

            // Write the rest of the buffer/HTML doc
            await baseStream.WriteAsync(buffer, cancellationToken);
            return true;
        }

        private static int LastIndexOfClosingBodyTag(ReadOnlySpan<byte> buffer)
        {
            while (true)
            {
                // Find the character sequence for the end of the closing tag.
                var index = buffer.LastIndexOf(s_closingTagEndIndicator);
                if (index == -1)
                {
                    return -1;
                }
                buffer = buffer[..index];

                // Find the first non-whitespace character inside the tag.
                index = buffer.LastIndexOfNonWhiteSpace();
                if (index == -1)
                {
                    return -1;
                }
                buffer = buffer[..(index + 1)];

                // Determine if the characters inside the tag match "body".
                if (!buffer.EndsWithIgnoreCase(s_bodyElementName))
                {
                    continue;
                }
                buffer = buffer[..^s_bodyElementName.Length];

                // Find the first non-whitespace character before the tag name.
                index = buffer.LastIndexOfNonWhiteSpace();
                if (index == -1)
                {
                    return -1;
                }
                buffer = buffer[..(index + 1)];

                // Determine if the characters preceding tag name match the closing tag start indicator.
                if (!buffer.EndsWith(s_closingTagStartIndicator))
                {
                    continue;
                }
                buffer = buffer[..^s_closingTagStartIndicator.Length];

                return buffer.Length;
            }
        }
    }
}
