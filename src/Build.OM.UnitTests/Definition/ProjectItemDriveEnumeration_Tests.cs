// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Engine.UnitTests.Globbing;
using Microsoft.Build.Evaluation;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.OM.Definition;

// These tests only add items, so repeating them with dictionary-based item removal enabled
// would enumerate the drive again without exercising a different code path.
public class ProjectItemDriveEnumeration_Tests
{
    private readonly ITestOutputHelper _output;

    public ProjectItemDriveEnumeration_Tests(ITestOutputHelper output)
    {
        _output = output;
    }

    [WindowsOnlyTheory]
    [InlineData(@"%DRIVE%:\**\*.log")]
    [InlineData(@"%DRIVE%:$(empty)\**\*.log")]
    [InlineData(@"%DRIVE%:\**")]
    [InlineData(@"%DRIVE%:\\**")]
    [InlineData(@"%DRIVE%:\\\\\\\\**")]
    [InlineData(@"%DRIVE%:\**\*.cs")]
    public void ProjectGetterResultsInWindowsDriveEnumerationWarning(string unevaluatedInclude)
    {
        using DummyMappedDrive mappedDrive = new();
        unevaluatedInclude = DummyMappedDriveUtils.UpdatePathToMappedDrive(unevaluatedInclude, mappedDrive.MappedDriveLetter);
        ProjectGetterResultsInDriveEnumerationWarning(unevaluatedInclude);
    }

    [UnixOnlyTheory]
    [InlineData(@"/**/*.log")]
    [InlineData(@"$(empty)/**/*.log")]
    [InlineData(@"/$(empty)**/*.log")]
    [InlineData(@"/*$(empty)*/*.log")]
    public void ProjectGetterResultsInUnixDriveEnumerationWarning(string unevaluatedInclude)
    {
        ProjectGetterResultsInDriveEnumerationWarning(unevaluatedInclude);
    }

    private void ProjectGetterResultsInDriveEnumerationWarning(string unevaluatedInclude)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        Helpers.ResetStateForDriveEnumeratingWildcardTests(env, "0");

        using ProjectCollection projectCollection = new();
        MockLogger collectionLogger = new(_output);
        projectCollection.RegisterLogger(collectionLogger);
        Project project = new(projectCollection);

        _ = project.AddItem("i", unevaluatedInclude);

        collectionLogger.Warnings.ShouldHaveSingleItem().Code.ShouldBe("MSB5029");
        projectCollection.UnregisterAllLoggers();
    }
}
