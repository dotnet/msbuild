// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Text;
using Microsoft.Extensions.Logging;

namespace Microsoft.NET.Build.Containers;

internal static class HttpExtensions
{
    /// <summary>
    /// Logs the details of <paramref name="response"/> using <paramref name="logger"/> to trace level.
    /// </summary>
    public static async Task LogHttpResponseAsync(this HttpResponseMessage response, ILogger logger, CancellationToken cancellationToken)
    {
        StringBuilder s = new();
        s.AppendLine($"Last request URI: {response.RequestMessage?.RequestUri?.ToString()}");
        s.AppendLine($"Status code: {response.StatusCode}");
        s.AppendLine($"Response headers:");
        s.AppendLine(response.Headers.ToString());
        string detail = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        s.AppendLine($"Response content: {(string.IsNullOrWhiteSpace(detail) ? "<empty>" : detail)}");
        logger.LogTrace(s.ToString());
    }
}
