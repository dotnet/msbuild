// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Sdk.Publish.Tasks;


namespace Microsoft.Net.Sdk.Publish.Tasks.Tests
{
    public class WebJobsCommandGeneratorTests
    {
        [Theory]
        [InlineData("c:/test/WebApplication1.dll", false, ".exe", "dotnet WebApplication1.dll %*")]

        [InlineData("c:/test/WebApplication1.dll", true, ".exe", "WebApplication1.exe %*")]
        [InlineData("c:/test/WebApplication1.dll", true, "", "WebApplication1 %*")]

        [InlineData("c:/test/WebApplication1.exe", true, ".exe", "WebApplication1.exe %*")]
        [InlineData("c:/test/WebApplication1.exe", false, ".exe", "WebApplication1.exe %*")]

        [InlineData("/usr/test/WebApplication1.dll", true, ".sh", "WebApplication1.sh %*")]
        [InlineData("/usr/test/WebApplication1.dll", false, ".sh", "dotnet WebApplication1.dll %*")]
        public void WebJobsCommandGenerator_Generates_Correct_RunCmd(string targetPath, bool useAppHost, string executableExtension, string expected)
        {
            // Arrange

            // Test
            string generatedRunCommand = WebJobsCommandGenerator.RunCommand(targetPath, useAppHost, executableExtension);

            // Assert
            Assert.Equal(expected, generatedRunCommand);
        }
    }
}
