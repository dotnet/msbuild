// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;

namespace Microsoft.NET.Build.Containers;

internal static class AuthHeaderCache
{
    private static readonly ConcurrentDictionary<string, AuthenticationHeaderValue> s_hostAuthenticationCache = new();

    public static bool TryGet(Uri uri, [NotNullWhen(true)] out AuthenticationHeaderValue? header)
    {
        return s_hostAuthenticationCache.TryGetValue(GetCacheKey(uri), out header); ;
    }

    public static AuthenticationHeaderValue AddOrUpdate(Uri uri, AuthenticationHeaderValue header)
    {
        return s_hostAuthenticationCache.AddOrUpdate(GetCacheKey(uri), header, (_, _) => header);
    }

    internal static string GetCacheKey(Uri uri)
    {
        string finalUri = uri.Host + uri.AbsolutePath;

        //trim uri parameters
        //cases:
        //push: 
        //POST /v2/<name>/blobs/uploads/
        //HEAD /v2/<name>/blobs/<digest>
        //GET /v2/<name>/blobs/uploads/<uuid>
        //PUT /v2/<name>/blobs/uploads/<uuid>?digest=<digest>
        //PATCH /v2/<name>/blobs/uploads/<uuid>
        //PUT /v2/<name>/manifests/<reference>

        //pull:
        //GET /v2/<name>/manifests/<reference>
        //HEAD /v2/<name>/manifests/<reference>
        //GET /v2/<name>/blobs/<digest>

        IReadOnlyList<string> possibleUris = new[] { "blobs/uploads/", "blobs/", "manifests/" };

        foreach (string end in possibleUris)
        {
            int index = finalUri.IndexOf(end);
            if (index == -1)
            {
                continue;
            }
            else
            {
                finalUri = finalUri.Substring(0, index + end.Length);
                break;
            }
        }
        return finalUri;
    }
}
