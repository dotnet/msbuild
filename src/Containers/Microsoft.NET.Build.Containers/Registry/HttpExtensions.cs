// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.Extensions.Logging;

namespace Microsoft.NET.Build.Containers;

internal static class HttpExtensions
{
    internal static HttpRequestMessage AcceptManifestFormats(this HttpRequestMessage request)
    {
        request.Headers.Accept.Clear();
        request.Headers.Accept.Add(new("application/json"));
        request.Headers.Accept.Add(new(SchemaTypes.DockerManifestListV2));
        request.Headers.Accept.Add(new(SchemaTypes.DockerManifestV2));
        request.Headers.Accept.Add(new(SchemaTypes.OciManifestV1));
        request.Headers.Accept.Add(new(SchemaTypes.DockerContainerV1));
        return request;
    }

    /// <summary>
    /// Servers send the Location header on each response, which tells us where to send the next chunk.
    /// </summary>
    public static Uri GetNextLocation(this HttpResponseMessage response)
    {
        if (response.Headers.Location is { IsAbsoluteUri: true })
        {
            return response.Headers.Location;
        }
        else
        {
            // if we don't trim the BaseUri and relative Uri of slashes, you can get invalid urls.
            // Uri constructor does this on our behalf.
            return new Uri(response.RequestMessage!.RequestUri!, response.Headers.Location?.OriginalString ?? "");
        }
    }

    internal static bool IsAmazonECRRegistry(this Uri uri)
    {
        // If this the registry is to public ECR the name will contain "public.ecr.aws".
        if (uri.Authority.Contains("public.ecr.aws"))
        {
            return true;
        }

        // If the registry is to a private ECR the registry will start with an account id which is a 12 digit number and will container either
        // ".ecr." or ".ecr-" if pushed to a FIPS endpoint.
        string accountId = uri.Authority.Split('.')[0];
        if ((uri.Authority.Contains(".ecr.") || uri.Authority.Contains(".ecr-")) && accountId.Length == 12 && long.TryParse(accountId, out _))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Logs the details of <paramref name="response"/> using <paramref name="logger"/> to trace level.
    /// </summary>
    internal static async Task LogHttpResponseAsync(this HttpResponseMessage response, ILogger logger, CancellationToken cancellationToken)
    {
        if (logger.IsEnabled(LogLevel.Trace))
        {
            StringBuilder s = new();
            s.AppendLine($"Request URI: {response.RequestMessage?.Method} {response.RequestMessage?.RequestUri?.ToString()}");
            s.AppendLine($"Status code: {response.StatusCode}");
            s.AppendLine($"Response headers:");
            s.AppendLine(response.Headers.ToString());
            string detail = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            s.AppendLine($"Response content: {(string.IsNullOrWhiteSpace(detail) ? "<empty>" : detail)}");
            logger.LogTrace(s.ToString());
        }
    }
}
