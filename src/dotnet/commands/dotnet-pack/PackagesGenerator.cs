// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.Tools.Compiler;

namespace Microsoft.DotNet.Tools.Pack
{
    public class PackagesGenerator
    {
        private readonly IEnumerable<ProjectContext> _contexts;
        private readonly ArtifactPathsCalculator _artifactPathsCalculator;
        private readonly string _configuration;

        public PackagesGenerator(
            IEnumerable<ProjectContext> contexts,
            ArtifactPathsCalculator artifactPathsCalculator,
            string configuration)
        {
            _contexts = contexts;
            _artifactPathsCalculator = artifactPathsCalculator;
            _configuration = configuration;
        }

        public int Build()
        {
            var project = _contexts.First().ProjectFile;

            var packDiagnostics = new List<DiagnosticMessage>();

            var mainPackageGenerator = new PackageGenerator(project, _configuration, _artifactPathsCalculator);
            var symbolsPackageGenerator =
                new SymbolPackageGenerator(project, _configuration, _artifactPathsCalculator);

            return mainPackageGenerator.BuildPackage(_contexts, packDiagnostics) &&
                symbolsPackageGenerator.BuildPackage(_contexts, packDiagnostics) ? 0 : 1;
        }
    }
}
