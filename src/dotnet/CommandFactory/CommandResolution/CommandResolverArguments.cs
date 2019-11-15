// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
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
