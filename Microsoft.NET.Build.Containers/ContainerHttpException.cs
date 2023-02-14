// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers;

internal sealed class ContainerHttpException : Exception
{
    private const string ErrorPrefix = "Containerize: error CONTAINER004:";
    string? jsonResponse;
    string? uri;
    public ContainerHttpException(string message, string? targetUri, string? jsonResp)
            : base($"{ErrorPrefix} {message}\nURI: {targetUri ?? "Unknown"}\nJson Response: {jsonResp ?? "None."}")
    {
        jsonResponse = jsonResp;
        uri = targetUri;
    }
}
