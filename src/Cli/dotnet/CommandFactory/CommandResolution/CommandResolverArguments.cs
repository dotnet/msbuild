// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using NuGet.Frameworks;

namespace Microsoft.DotNet.CommandFactory
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

        public string DepsJsonFile { get; set; }

        public string ApplicationName { get; set; }
    }
}
