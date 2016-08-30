// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

namespace Microsoft.DotNet.Tests.EndToEnd
{
    public class GivenDotNetUsesMSBuild : TestBase
    {
        [Fact]
        public void ItCanNewRestoreBuildRunMSBuildProject()
        {
            // The current ResolveNuGetAssets target does not work on case-sensitive file systems.  We need https://github.com/dotnet/sdk/pull/10
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
                RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                using (DisposableDirectory directory = Temp.CreateDirectory())
                {
                    string projectDirectory = directory.Path;

                    new NewCommand()
                        .WithWorkingDirectory(projectDirectory)
                        .Execute("-t msbuild")
                        .Should()
                        .Pass();

                    new RestoreCommand()
                        .WithWorkingDirectory(projectDirectory)
                        .Execute()
                        .Should()
                        .Pass();

                    new Build3Command()
                        .WithWorkingDirectory(projectDirectory)
                        .Execute()
                        .Should()
                        .Pass();

                    new Run3Command()
                        .WithWorkingDirectory(projectDirectory)
                        .ExecuteWithCapturedOutput()
                        .Should()
                        .Pass()
                        .And
                        .HaveStdOutContaining("Hello World!");
                }
            }
        }
    }
}
