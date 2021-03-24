// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using FluentAssertions;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.TemplateEngine.TestHelper;
using Xunit.Abstractions;

namespace dotnet_new3.UnitTests
{
    internal static class Helpers
    {
        internal static void InstallNuGetTemplate(string packageName, ITestOutputHelper log, string workingDirectory, string homeDirectory)
        {
            new DotnetNewCommand(log, "-i", packageName)
                  .WithWorkingDirectory(workingDirectory)
                  .WithEnvironmentVariable(TestUtils.HomeEnvironmentVariableName, homeDirectory)
                  .Execute()
                  .Should()
                  .ExitWith(0)
                  .And
                  .NotHaveStdErr();
        }

        internal static string InstallTestTemplate(string templateName, ITestOutputHelper log, string workingDirectory, string homeDirectory)
        {
            string testTemplate = TestUtils.GetTestTemplateLocation(templateName);
            new DotnetNewCommand(log, "-i", testTemplate)
                  .WithWorkingDirectory(workingDirectory)
                  .WithEnvironmentVariable(TestUtils.HomeEnvironmentVariableName, homeDirectory)
                  .Execute()
                  .Should()
                  .ExitWith(0)
                  .And
                  .NotHaveStdErr();
            return Path.GetFullPath(testTemplate);
        }
    }
}
