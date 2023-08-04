// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.NET.Build.Containers;

internal sealed class UnableToAccessRepositoryException : Exception
{
    public UnableToAccessRepositoryException(string registry)
        : base(string.Format("Unable to access the registry '{0}'. Please confirm your credentials are correct and that you have access to this registry.", registry))
    {
    }
    public UnableToAccessRepositoryException(string registry, string repository)
        : base(string.Format("Unable to access the repository '{0}' in the registry '{1}'. Please confirm your credentials are correct and that you have access to this repository and registry.", repository, registry))
    {
    }
}
