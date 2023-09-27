// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers;

public sealed class BaseImageNotFoundException : Exception
{
    internal BaseImageNotFoundException(string specifiedRuntimeIdentifier, string repositoryName, string reference, IEnumerable<string> supportedRuntimeIdentifiers)
            : base($"The RuntimeIdentifier '{specifiedRuntimeIdentifier}' is not supported by {repositoryName}:{reference}. The supported RuntimeIdentifiers are {string.Join(",", supportedRuntimeIdentifiers)}") { }
}
