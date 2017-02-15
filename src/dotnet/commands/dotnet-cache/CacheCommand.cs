// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.MSBuild;
using Microsoft.DotNet.Tools.Restore;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Tools.Cache
{
    public partial class CacheCommand
    {
        public string ProjectArgument { get; set; }
        public string Framework { get; set; }
        public string Runtime { get; set; }
        public string OutputPath { get; set; }
        public string FrameworkVersion { get; set; }
        public string IntermediateDir { get; set; }
        public string Verbosity { get; set; }
        private bool SkipOptimization { get; set; }
        private bool PreserveIntermediateDir { get; set; }

        public List<string> ExtraMSBuildArguments { get; set; }

        private CacheCommand()
        {
        }

        public int Execute()
        {
            var msbuildArgs = new List<string>();

            if (string.IsNullOrEmpty(ProjectArgument))
            {
                throw new InvalidOperationException(LocalizableStrings.SpecifyEntries);
            }

            msbuildArgs.Add("/t:ComposeCache");
            msbuildArgs.Add(ProjectArgument);

            if (!string.IsNullOrEmpty(Framework))
            {
                msbuildArgs.Add($"/p:TargetFramework={Framework}");
            }

            if (!string.IsNullOrEmpty(Runtime))
            {
                msbuildArgs.Add($"/p:RuntimeIdentifier={Runtime}");
            }

            if (!string.IsNullOrEmpty(OutputPath))
            {
                OutputPath = Path.GetFullPath(OutputPath);
                msbuildArgs.Add($"/p:ComposeDir={OutputPath}");
            }

            if (!string.IsNullOrEmpty(FrameworkVersion))
            {
                msbuildArgs.Add($"/p:FX_Version={FrameworkVersion}");
            }

            if (!string.IsNullOrEmpty(IntermediateDir))
            {
                msbuildArgs.Add($"/p:ComposeWorkingDir={IntermediateDir}");
            }

            if (SkipOptimization)
            {
                msbuildArgs.Add($"/p:SkipOptimization={SkipOptimization}");
            }

            if (PreserveIntermediateDir)
            {
                msbuildArgs.Add($"/p:PreserveComposeWorkingDir={PreserveIntermediateDir}");
            }

            if (!string.IsNullOrEmpty(Verbosity))
            {
                msbuildArgs.Add($"/verbosity:{Verbosity}");
            }

            msbuildArgs.AddRange(ExtraMSBuildArguments);

            return new MSBuildForwardingApp(msbuildArgs).Execute();
        }
    }
}

