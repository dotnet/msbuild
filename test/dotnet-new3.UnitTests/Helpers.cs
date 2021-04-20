// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using FluentAssertions;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.TemplateEngine.TestHelper;
using Xunit.Abstractions;

namespace Dotnet_new3.IntegrationTests
{
    internal static class Helpers
    {
        internal static void InstallNuGetTemplate(string packageName, ITestOutputHelper log, string workingDirectory, string homeDirectory)
        {
            new DotnetNewCommand(log, "-i", packageName)
                  .WithCustomHive()
                  .WithWorkingDirectory(workingDirectory)
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
                  .WithCustomHive(homeDirectory)
                  .WithWorkingDirectory(workingDirectory)
                  .Execute()
                  .Should()
                  .ExitWith(0)
                  .And
                  .NotHaveStdErr();
            return Path.GetFullPath(testTemplate);
        }
    }
}
