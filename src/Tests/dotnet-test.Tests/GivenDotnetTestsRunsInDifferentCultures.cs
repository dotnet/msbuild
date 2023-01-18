// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using System.IO;
using System;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Cli.Test.Tests;

public class CultureAwareTestProject : SdkTest
{
    private const string TestAppName = "TestAppSimple";

    public CultureAwareTestProject(ITestOutputHelper log) : base(log)
    {
    }

    [InlineData("en-US")]
    [InlineData("de-DE")]
    [Theory]
    public void CanRunTestsAgainstProjectInLocale(string locale)
    {
        var testAsset = _testAssetsManager.CopyTestAsset(TestAppName)
                .WithSource()
                .WithVersionVariables();

        var command = new DotnetTestCommand(Log).WithWorkingDirectory(testAsset.Path).WithCulture(locale);
        var result = command.Execute();

        result.ExitCode.Should().Be(0);
    }
}
