// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.DotNet.ProjectModel.Server.Models;
using NuGet.Frameworks;

namespace Microsoft.DotNet.ProjectModel.Server.InternalModels
{
    internal class ProjectSnapshot
    {
        public NuGetFramework TargetFramework { get; set; }
        public IReadOnlyList<string> SourceFiles { get; set; }
        public CommonCompilerOptions CompilerOptions { get; set; }
        public IReadOnlyList<ProjectReferenceDescription> ProjectReferences { get; set; }
        public IReadOnlyList<string> FileReferences { get; set; }
        public IReadOnlyList<DiagnosticMessage> DependencyDiagnostics { get; set; }
        public IDictionary<string, DependencyDescription> Dependencies { get; set; }
        public string RootDependency { get; set; }
    }
}
