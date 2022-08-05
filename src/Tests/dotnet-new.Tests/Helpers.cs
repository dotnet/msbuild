// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.IO;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.TemplateEngine.TestHelper;
using Xunit.Abstractions;

namespace Microsoft.DotNet.New.Tests
{
    internal static class Helpers
    {
        internal static void InstallNuGetTemplate(string packageName, ITestOutputHelper log, string homeDirectory, string? workingDirectory = null)
        {
            DotnetNewCommand command = new DotnetNewCommand(log, "-i", packageName)
                  .WithCustomHive(homeDirectory);
            if (!string.IsNullOrWhiteSpace(workingDirectory))
            {
                command.WithWorkingDirectory(workingDirectory);
            }

            command.Execute()
                  .Should()
                  .ExitWith(0)
                  .And
                  .NotHaveStdErr();
        }

        internal static string InstallTestTemplate(string templateName, ITestOutputHelper log, string homeDirectory, string? workingDirectory = null)
        {
            string testTemplate = TestUtils.GetTestTemplateLocation(templateName);

            DotnetNewCommand command = new DotnetNewCommand(log, "-i", testTemplate)
                .WithCustomHive(homeDirectory);

            if (!string.IsNullOrWhiteSpace(workingDirectory))
            {
                command.WithWorkingDirectory(workingDirectory);
            }

            command.Execute()
                  .Should()
                  .ExitWith(0)
                  .And
                  .NotHaveStdErr();
            return Path.GetFullPath(testTemplate);
        }

        internal static string FormatOutputStreams(this CommandResult commandResult)
        {
            return "StdErr:" + Environment.NewLine + commandResult.StdErr + Environment.NewLine + "StdOut:" + Environment.NewLine + commandResult.StdOut;
        }
    }
}
