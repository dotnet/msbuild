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

        public override DirectoryInfo GetOutputDirectory(string targetFramework = "netcoreapp1.1", string configuration = "Debug", string runtimeIdentifier = "")
        {
            DirectoryInfo baseDirectory = base.GetOutputDirectory(targetFramework, configuration, runtimeIdentifier); 
            return new DirectoryInfo(Path.Combine(baseDirectory.FullName, PublishSubfolderName));
        }

        public string GetPublishedAppPath(string appName)
        {
            return Path.Combine(GetOutputDirectory().FullName, $"{appName}.dll");
        }
    }
}
