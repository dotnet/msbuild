// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.Test.Utilities;

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
