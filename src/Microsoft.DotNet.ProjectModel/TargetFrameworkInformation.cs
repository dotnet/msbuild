// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using NuGet.Frameworks;

namespace Microsoft.DotNet.ProjectModel
{
    public class TargetFrameworkInformation
    {
        public NuGetFramework FrameworkName { get; set; }

        public IReadOnlyList<ProjectLibraryDependency> Dependencies { get; set; }

        public CommonCompilerOptions CompilerOptions { get; set; }

        public int Line { get; set; }

        public int Column { get; set; }

        // REVIEW: Wrapping, we might do this differntly
        public string WrappedProject { get; set; }

        public string AssemblyPath { get; set; }
    }
}
