// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;

namespace Microsoft.NET.Build.Containers;

internal static class AuthHeaderCache
{

    private static ConcurrentDictionary<string, AuthenticationHeaderValue> HostAuthenticationCache = new();

    public static bool TryGet(Uri uri, [NotNullWhen(true)] out AuthenticationHeaderValue? header)
    {
        header = null;

        // observed quirk in Azure Container Registry: if you present a token to blobs/uploads and it's wrong,
        // it won't give back a www-authenticate header for the reauth mechanism to work. So never return
        // a cache for that URI pattern
        string[] segments = uri.Segments;
        if (segments is [.., "blobs/", "uploads/"])
        {
            return false;
        }

        return HostAuthenticationCache.TryGetValue(GetCacheKey(uri), out header);
    }

    public static AuthenticationHeaderValue AddOrUpdate(Uri uri, AuthenticationHeaderValue header)
    {
        return HostAuthenticationCache.AddOrUpdate(GetCacheKey(uri), header, (_, _) => header);
    }

    private static string GetCacheKey(Uri uri)
    {
        return uri.Host + uri.AbsolutePath;
    }
}
