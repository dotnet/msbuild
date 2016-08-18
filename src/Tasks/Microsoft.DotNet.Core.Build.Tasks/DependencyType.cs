// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Core.Build.Tasks
{
    public enum DependencyType
    {
        Unknown,
        Target,
        Diagnostic,
        Package,
        Assembly,
        FrameworkAssembly,
        Content,
        Project,
        ExternalProject,
        Reference,
        Winmd,
        Unresolved
    }
}
