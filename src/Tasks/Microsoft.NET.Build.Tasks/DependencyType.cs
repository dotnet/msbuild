// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
