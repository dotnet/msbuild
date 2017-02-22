// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using System.Collections.Generic;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.MSBuild;
using Microsoft.DotNet.Tools.Restore;

namespace Microsoft.DotNet.Tools.Publish
{
    public partial class PublishCommand
    {
        private string _msbuildPath;

        public string ProjectPath { get; set; }
        public string Framework { get; set; }
        public string Runtime { get; set; }
        public string OutputPath { get; set; }
        public string Configuration { get; set; }
        public string VersionSuffix { get; set; }
        public string FilterProject { get; set; }
        public string Verbosity { get; set; }

        public List<string> ExtraMSBuildArguments { get; set; }

        private PublishCommand(string msbuildPath = null)
        {
            _msbuildPath = msbuildPath;
        }

        private MSBuildForwardingApp CreateForwardingApp(string msbuildPath)
        {
            List<string> msbuildArgs = new List<string>();

            msbuildArgs.Add("/t:Publish");

            if (!string.IsNullOrEmpty(ProjectPath))
            {
                msbuildArgs.Add(ProjectPath);
            }

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
                msbuildArgs.Add($"/p:PublishDir={OutputPath}");
            }

            if (!string.IsNullOrEmpty(Configuration))
            {
                msbuildArgs.Add($"/p:Configuration={Configuration}");
            }

            if (!string.IsNullOrEmpty(VersionSuffix))
            {
                msbuildArgs.Add($"/p:VersionSuffix={VersionSuffix}");
            }

            if (!string.IsNullOrEmpty(FilterProject))
            {
                msbuildArgs.Add($"/p:FilterProjFile={FilterProject}");
            }

            if (!string.IsNullOrEmpty(Verbosity))
            {
                msbuildArgs.Add($"/verbosity:{Verbosity}");
            }

            msbuildArgs.AddRange(ExtraMSBuildArguments);

            return new MSBuildForwardingApp(msbuildArgs, msbuildPath);
        }
    }
}
