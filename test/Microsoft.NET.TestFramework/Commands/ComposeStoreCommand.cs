// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.PlatformAbstractions;
using System.IO;
using System.Linq;
using Xunit.Abstractions;

namespace Microsoft.NET.TestFramework.Commands
{
    public sealed class ComposeStoreCommand : MSBuildCommand
    {
        private const string PublishSubfolderName = "packages";

        public ComposeStoreCommand(ITestOutputHelper log, string projectPath, string relativePathToProject = null)
            : base(log, "ComposeStore", projectPath, relativePathToProject)
        {
        }

       

        public override DirectoryInfo GetOutputDirectory(string targetFramework = "netcoreapp1.0", string configuration = "Debug", string runtimeIdentifier = "")
        {
            string output = Path.Combine(ProjectRootPath, "bin", BuildRelativeOutputPath(targetFramework, configuration, runtimeIdentifier));
            return new DirectoryInfo(output);
        }

        public string GetPublishedAppPath(string appName)
        {
            return Path.Combine(GetOutputDirectory().FullName, $"{appName}.dll");
        }

        private string BuildRelativeOutputPath(string targetFramework, string configuration, string runtimeIdentifier)
        {
            if (runtimeIdentifier.Length == 0)
            {
                runtimeIdentifier = RuntimeEnvironment.GetRuntimeIdentifier();
            }
            string arch = runtimeIdentifier.Substring(runtimeIdentifier.LastIndexOf("-") + 1);
            return Path.Combine(configuration, arch, targetFramework, PublishSubfolderName);
        }
    }
}
