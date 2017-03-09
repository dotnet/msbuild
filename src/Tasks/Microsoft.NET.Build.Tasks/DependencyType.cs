// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.NET.Build.Tasks
{
    internal enum DependencyType
    {
        Unknown,
        Target,
        Diagnostic,
        Package,
        Assembly,
        FrameworkAssembly,
        AnalyzerAssembly,
        Content,
        Project,
        ExternalProject,
        Reference,
        Winmd,
        Unresolved
    }
}
