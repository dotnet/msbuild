// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.IO;
using Xunit.Abstractions;

namespace Microsoft.NET.TestFramework.Commands
{
    public sealed class PublishCommand : MSBuildCommand
    {
        private const string PublishSubfolderName = "publish";

        //  Encourage use of the other overload, which is generally simpler to use
        [EditorBrowsable(EditorBrowsableState.Never)]
        public PublishCommand(ITestOutputHelper log, string projectPath)
            : base(log, "Publish", projectPath, relativePathToProject: null)
        {
        }

        public PublishCommand(TestAsset testAsset, string relativePathToProject = null)
            : base(testAsset, "Publish", relativePathToProject)
        {

        }

        public override DirectoryInfo GetOutputDirectory(string targetFramework = null, string configuration = "Debug", string runtimeIdentifier = "")
        {
            if (TestAsset != null)
            {
                return new DirectoryInfo(OutputPathCalculator.FromProject(ProjectFile, TestAsset).GetPublishDirectory(targetFramework, configuration, runtimeIdentifier));
            }

            if (string.IsNullOrEmpty(targetFramework))
            {
                targetFramework = "netcoreapp1.1";
            }

            DirectoryInfo baseDirectory = base.GetOutputDirectory(targetFramework, configuration, runtimeIdentifier); 
            return new DirectoryInfo(Path.Combine(baseDirectory.FullName, PublishSubfolderName));
        }

        public string GetPublishedAppPath(string appName, string targetFramework = "")
        {
            return Path.Combine(GetOutputDirectory(targetFramework).FullName, $"{appName}.dll");
        }
    }
}
