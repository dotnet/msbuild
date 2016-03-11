using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.ProjectModel.Graph;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Cli.Utils
{
    public class CommandResolverArguments
    {
        public string CommandName { get; set; }

        public IEnumerable<string> CommandArguments { get; set; }

        public NuGetFramework Framework { get; set; }

        public string OutputPath { get; set; }

        public string ProjectDirectory { get; set; }

        public string Configuration { get; set; }

        public IEnumerable<string> InferredExtensions { get; set; }

        public string BuildBasePath { get; set; }
    }
}
