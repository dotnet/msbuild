// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using NuGet.Frameworks;

namespace Microsoft.DotNet.ProjectModel.Server.Models
{
    public class DiagnosticMessageGroup
    {
        public DiagnosticMessageGroup(IEnumerable<DiagnosticMessage> diagnostics)
            : this(framework: null, diagnostics: diagnostics)
        { }

        public DiagnosticMessageGroup(NuGetFramework framework, IEnumerable<DiagnosticMessage> diagnostics)
        {
            Framework = framework;
            Diagnostics = diagnostics;
        }

        public IEnumerable<DiagnosticMessage> Diagnostics { get; }

        public NuGetFramework Framework { get; }
    }
}
