// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Build.Construction;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Evaluation;

public sealed class EvaluationInputRecording_Tests : IDisposable
{
    private const string EnableVariable = "MSBUILDRECORDEVALUATIONINPUTS";

    private readonly TestEnvironment _env;
    private readonly TransientTestFolder _folder;

    public EvaluationInputRecording_Tests(ITestOutputHelper output)
    {
        _env = TestEnvironment.Create(output);
        _folder = _env.CreateFolder(createFolder: true);
        SetRecording(enabled: true);
    }

    public void Dispose()
    {
        _env.Dispose();
        Traits.UpdateFromEnvironment();
    }

    [Fact]
    public void DisabledRecordingProducesNoInputs()
    {
        SetRecording(enabled: false);
        string project = CreateProject("""
            <Project>
              <PropertyGroup>
                <A>1</A>
              </PropertyGroup>
            </Project>
            """);

        ProjectInstance instance = ProjectInstance.FromFile(project, CreateOptions());

        instance.EvaluationInputs.ShouldBeNull();
        instance.GetPropertyValue("A").ShouldBe("1");
    }

    [Fact]
    public void InMemoryProjectIsNotCacheable()
    {
        ProjectCollection collection = _env.CreateProjectCollection().Collection;
        ProjectRootElement xml = ProjectRootElement.Create(collection);

        var instance = new ProjectInstance(xml, globalProperties: null, toolsVersion: null, collection);

        instance.EvaluationInputs.ShouldNotBeNull().NonCacheable.ShouldBe(NonCacheableReason.InMemoryProject);
    }

    [Fact]
    public void ThreadWorkingDirectoryIsTheWorkingDirectoryInTheKey()
    {
        // The in-process node gives each build thread its own working directory, which is what Path.GetFullPath resolves against.
        string project = CreateProject("<Project />");
        string? saved = FileUtilities.CurrentThreadWorkingDirectory;
        try
        {
            FileUtilities.CurrentThreadWorkingDirectory = _folder.Path;

            Evaluate(project).Key.WorkingDirectory.ShouldBe(_folder.Path);
        }
        finally
        {
            FileUtilities.CurrentThreadWorkingDirectory = saved;
        }
    }

    [Fact]
    public void WorkingDirectoryIsInTheKeySoRelativeGetFullPathIsCacheable()
    {
        string project = CreateProject("""
            <Project>
              <PropertyGroup>
                <Relative>$([System.IO.Path]::GetFullPath('a.txt'))</Relative>
              </PropertyGroup>
            </Project>
            """);

        EvaluationInputs inputs = Evaluate(project);

        inputs.NonCacheable.ShouldBe(NonCacheableReason.None);
        inputs.Key.WorkingDirectory.ShouldBe(Directory.GetCurrentDirectory());
    }

    [Fact]
    public void ParserConfigurationIsRecordedAndInTheKey()
    {
        // The parser skips what Directory.Parse.config allows, so the files are inputs and their content is in the key.
        string project = CreateProject("<Project />");
        EvaluationInputs without = Evaluate(project);
        string config = _env.CreateFile(_folder, ParserIgnoreConfiguration.ConfigFileName, """<ParseConfig><IgnoreAttributes><Ignore Element="Target" Name="Foo" /></IgnoreAttributes></ParseConfig>""").Path;
        _env.SetEnvironmentVariable(ParserIgnoreConfiguration.EnvironmentVariableName, config);

        EvaluationInputs with = Evaluate(project);

        with.NonCacheable.ShouldBe(NonCacheableReason.None);
        with.Key.ParserConfigurationFingerprint.ShouldNotBe(without.Key.ParserConfigurationFingerprint);
    }

    [Fact]
    public void KeysOfEqualEvaluationsAreEqual()
    {
        string project = CreateProject("<Project />");
        ProjectOptions options = CreateOptions();
        options.GlobalProperties = new Dictionary<string, string> { ["Configuration"] = "Release", ["Platform"] = "x64" };

        EvaluationInputKey first = Evaluate(project, options).Key;
        EvaluationInputKey second = Evaluate(project, options).Key;

        second.ShouldBe(first);
        second.GetHashCode().ShouldBe(first.GetHashCode());
    }

    [Fact]
    public void KeyDistinguishesToolsetsWithTheSameVersion()
    {
        string project = CreateProject("<Project />");

        EvaluationInputKey first = EvaluateWithToolsetProperty(project, "1").Key;
        EvaluationInputKey same = EvaluateWithToolsetProperty(project, "1").Key;
        EvaluationInputKey other = EvaluateWithToolsetProperty(project, "2").Key;

        same.ShouldBe(first);
        other.ShouldNotBe(first);
    }

    private EvaluationInputs EvaluateWithToolsetProperty(string project, string value)
    {
        ProjectCollection collection = _env.CreateProjectCollection().Collection;
        Toolset current = collection.GetToolset(ObjectModelHelpers.MSBuildDefaultToolsVersion);
        collection.AddToolset(new Toolset("Custom", current.ToolsPath, new Dictionary<string, string> { ["Contoso"] = value }, collection, msbuildOverrideTasksPath: null));
        return Evaluate(project, new ProjectOptions { ProjectCollection = collection, ToolsVersion = "Custom" });
    }

    private void SetRecording(bool enabled)
    {
        _env.SetEnvironmentVariable(EnableVariable, enabled ? "1" : null);
        Traits.UpdateFromEnvironment();
    }

    private string CreateProject(string xml, string name = "test.proj") =>
        _env.CreateFile(_folder, name, xml.Cleanup()).Path;

    private ProjectOptions CreateOptions() =>
        new() { ProjectCollection = _env.CreateProjectCollection().Collection };

    private EvaluationInputs Evaluate(string project, ProjectOptions? options = null)
    {
        ProjectInstance instance = ProjectInstance.FromFile(project, options ?? CreateOptions());
        EvaluationInputs inputs = instance.EvaluationInputs.ShouldNotBeNull();
        return inputs;
    }
}
