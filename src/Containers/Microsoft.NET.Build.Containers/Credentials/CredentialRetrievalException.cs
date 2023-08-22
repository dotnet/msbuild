// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Build.Containers.Resources;

namespace Microsoft.NET.Build.Containers.Credentials;

internal sealed class CredentialRetrievalException : Exception
{
    public CredentialRetrievalException(string registry, Exception innerException)
        : base(
            Resource.FormatString(nameof(Strings.FailedRetrievingCredentials), registry, innerException.Message),
            innerException)
    { }
}
