// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.Build.Construction;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Execution;
using Microsoft.Build.FileSystem;
using Microsoft.Build.Framework;
using Microsoft.Build.Unittest;
using Microsoft.Win32;
using Shouldly;
using Xunit;
using SdkResult = Microsoft.Build.BackEnd.SdkResolution.SdkResult;

namespace Microsoft.Build.UnitTests.Evaluation;

public sealed class EvaluationInputRecording_Tests : IDisposable
{
    private const string EnableVariable = "MSBUILDRECORDEVALUATIONINPUTS";

    private readonly ITestOutputHelper _output;
    private readonly TestEnvironment _env;
    private readonly TransientTestFolder _folder;

    public EvaluationInputRecording_Tests(ITestOutputHelper output)
    {
        _output = output;
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
    public void RecordsRootProjectAsExistingFile()
    {
        string project = CreateProject("<Project />");

        EvaluationInputs inputs = Evaluate(project);

        inputs.NonCacheable.ShouldBe(NonCacheableReason.None);
        inputs.Key.ProjectFullPath.ShouldBe(project);
        inputs.Key.Culture.ShouldBe(CultureInfo.CurrentCulture.Name);
        inputs.Key.ToolsPath.ShouldNotBeNullOrEmpty();
        FileDependency root = inputs.Files[project];
        root.Kind.ShouldBe(PathKind.File);
        root.LastWriteTimeUtc.ShouldBe(File.GetLastWriteTimeUtc(project));
        root.Length.ShouldBe(new FileInfo(project).Length);
    }

    [Fact]
    public void IgnoredMissingImportIsRecordedAsMissing()
    {
        string project = CreateProject("""
            <Project>
              <Import Project="missing.props" />
            </Project>
            """);
        ProjectOptions options = CreateOptions();
        options.LoadSettings = ProjectLoadSettings.IgnoreMissingImports;

        EvaluationInputs inputs = Evaluate(project, options);

        inputs.Files[Path.Combine(_folder.Path, "missing.props")].Kind.ShouldBe(PathKind.Missing);
    }

    [Theory]
    [InlineData("FileExists", "optional.props")]
    [InlineData("DirectoryExists", "generated")]
    public void IntrinsicExistsProbeIsRecorded(string function, string name)
    {
        string project = CreateProject($"""
            <Project>
              <PropertyGroup>
                <Found>$([MSBuild]::{function}('$(MSBuildThisFileDirectory){name}'))</Found>
              </PropertyGroup>
            </Project>
            """);
        string probed = Path.Combine(_folder.Path, name);

        EvaluationInputs inputs = Evaluate(project);

        inputs.NonCacheable.ShouldBe(NonCacheableReason.None);
        inputs.Files[probed].Kind.ShouldBe(PathKind.Missing);
    }

    [Fact]
    public void ExistsOnLoadedProjectIsRecorded()
    {
        // Only design-time evaluation consults the loaded projects, and only for import conditions; ProjectInstance
        // evaluation always asks the file system.
        ProjectCollection collection = _env.CreateProjectCollection().Collection;
        string loaded = _env.CreateFile(_folder, "loaded.proj", "<Project />").Path;
        ProjectRootElement loadedElement = ProjectRootElement.Open(loaded, collection);
        _env.CreateFile(_folder, "other.props", "<Project><PropertyGroup><HasOther>true</HasOther></PropertyGroup></Project>");
        string project = CreateProject("""
            <Project>
              <Import Project="other.props" Condition="Exists('$(MSBuildThisFileDirectory)loaded.proj')" />
            </Project>
            """);

        var evaluated = new Project(project, globalProperties: null, toolsVersion: null, collection);

        evaluated.GetPropertyValue("HasOther").ShouldBe("true");
        evaluated.EvaluationInputs.ShouldNotBeNull().Files[loaded].Kind.ShouldBe(PathKind.File);
        GC.KeepAlive(loadedElement);
    }

    [Fact]
    public void IgnoredEmptyImportIsRecordedWithItsLength()
    {
        string empty = _env.CreateFile(_folder, "empty.props", string.Empty).Path;
        string project = CreateProject("""
            <Project>
              <Import Project="empty.props" />
            </Project>
            """);
        ProjectOptions options = CreateOptions();
        options.LoadSettings = ProjectLoadSettings.IgnoreEmptyImports;
        EvaluationInputs inputs = Evaluate(project, options);
        inputs.Files[empty].ShouldBe(new FileDependency(PathKind.File, File.GetLastWriteTimeUtc(empty), 0));
    }

    [Fact]
    public void UnsavedProjectChangesAreNotCacheable()
    {
        ProjectCollection collection = _env.CreateProjectCollection().Collection;
        string project = CreateProject("<Project />");
        ProjectRootElement xml = ProjectRootElement.Open(project, collection);
        xml.AddProperty("Edited", "true");

        var instance = new ProjectInstance(xml, globalProperties: null, toolsVersion: null, collection);

        instance.EvaluationInputs.ShouldNotBeNull().NonCacheable.ShouldBe(NonCacheableReason.InMemoryProject);
    }

    [Fact]
    public void ProjectSourceChangedAfterItWasReadIsNotCacheable()
    {
        ProjectCollection collection = _env.CreateProjectCollection().Collection;
        string project = CreateProject("<Project />");
        ProjectRootElement xml = ProjectRootElement.Open(project, collection);
        Touch(project, "<Project><PropertyGroup /></Project>");

        var instance = new ProjectInstance(xml, globalProperties: null, toolsVersion: null, collection);

        instance.EvaluationInputs.ShouldNotBeNull().NonCacheable.ShouldBe(NonCacheableReason.ConflictingObservation);
    }

    [Fact]
    public void FileReadPropertyFunctionRecordsTheFile()
    {
        string version = _env.CreateFile(_folder, "version.txt", "1.0").Path;
        string project = CreateProject("""
            <Project>
              <PropertyGroup>
                <Version>$([System.IO.File]::ReadAllText('$(MSBuildProjectDirectory)/version.txt'))</Version>
              </PropertyGroup>
            </Project>
            """);
        EvaluationInputs inputs = Evaluate(project);
        inputs.NonCacheable.ShouldBe(NonCacheableReason.None);
        inputs.Files[version].Kind.ShouldBe(PathKind.File);
    }

    [Theory]
    [InlineData("second")]
    [InlineData(null)]
    public void EnvironmentReadIsRecordedButNotRevalidated(string? currentValue)
    {
        _env.SetEnvironmentVariable("MSBUILD_TEST_INPUT", "first");
        string project = CreateProject("""
            <Project>
              <PropertyGroup>
                <Value>$([System.Environment]::GetEnvironmentVariable('MSBUILD_TEST_INPUT'))</Value>
              </PropertyGroup>
            </Project>
            """);
        EvaluationInputs inputs = Evaluate(project);
        inputs.EnvironmentReads["MSBUILD_TEST_INPUT"].ShouldBe("first");

        _env.SetEnvironmentVariable("MSBUILD_TEST_INPUT", currentValue);

        inputs.EnvironmentReads["MSBUILD_TEST_INPUT"].ShouldBe("first");
    }

    [Theory]
    [InlineData("$([System.DateTime]::Now)")]
    [InlineData("$([System.Guid]::NewGuid())")]
    [InlineData("$([System.DateTime]::Parse('12:34'))")]
    [InlineData("$([System.Convert]::ToDateTime('12:34'))")]
    public void VolatilePropertyFunctionIsNotCacheable(string expression)
    {
        string project = CreateProject($"""
            <Project>
              <PropertyGroup>
                <Value>{expression}</Value>
              </PropertyGroup>
            </Project>
            """);

        EvaluationInputs inputs = Evaluate(project);

        inputs.NonCacheable.ShouldBe(NonCacheableReason.VolatilePropertyFunction);
    }

    [Theory]
    [InlineData("$([System.IO.File]::GetCreationTime('$(MSBuildProjectFullPath)'))")]
    [InlineData("$([System.IO.File]::GetAttributes('$(MSBuildProjectFullPath)'))")]
    [InlineData("$([System.IO.Directory]::GetLastAccessTime('$(MSBuildProjectDirectory)'))")]
    public void ReadsOfFieldsTheManifestDoesNotHoldAreNotCacheable(string expression)
    {
        string project = CreateProject($"""
            <Project>
              <PropertyGroup>
                <Value>{expression}</Value>
              </PropertyGroup>
            </Project>
            """);

        Evaluate(project).NonCacheable.ShouldBe(NonCacheableReason.UnclassifiedPropertyFunction);
    }

    [Fact]
    public void PurePropertyFunctionsAreCacheable()
    {
        string project = CreateProject("""
            <Project>
              <PropertyGroup>
                <A>value</A>
                <B>$(A.ToUpper())_$([System.String]::Join('-', 'x', 'y'))_$([MSBuild]::Add(1, 2))</B>
              </PropertyGroup>
            </Project>
            """);

        EvaluationInputs inputs = Evaluate(project);

        inputs.NonCacheable.ShouldBe(NonCacheableReason.None);
    }

    [Fact]
    public void UnclassifiedPropertyFunctionIsNotCacheable()
    {
        string project = CreateProject("""
            <Project>
              <PropertyGroup>
                <Drives>$([System.Environment]::GetLogicalDrives())</Drives>
              </PropertyGroup>
            </Project>
            """);

        EvaluationInputs inputs = Evaluate(project);

        inputs.NonCacheable.ShouldBe(NonCacheableReason.UnclassifiedPropertyFunction);
        inputs.NonCacheableDetail.ShouldNotBeNull().ShouldContain("GetLogicalDrives");
    }

    [Fact]
    public void AllPropertyFunctionsSwitchIsNotCacheable()
    {
        // The AppContext switch overrides the variable when a previous test left it set, so honor whichever is active.
        const string switchName = "Microsoft.Build.EnableAllPropertyFunctions";
        bool switchWasSet = AppContext.TryGetSwitch(switchName, out bool original);
        _env.SetEnvironmentVariable("MSBUILDENABLEALLPROPERTYFUNCTIONS", "1");
        if (switchWasSet)
        {
            AppContext.SetSwitch(switchName, true);
        }

        try
        {
            EvaluationInputs inputs = Evaluate(CreateProject("<Project />"));

            inputs.NonCacheable.ShouldBe(NonCacheableReason.AllPropertyFunctionsEnabled);
        }
        finally
        {
            if (switchWasSet)
            {
                AppContext.SetSwitch(switchName, original);
            }
        }
    }

    [WindowsOnlyTheory]
    [InlineData("$(Registry:{0}@Value)")]
    [InlineData("$([MSBuild]::GetRegistryValue('{0}', 'Value'))")]
    [InlineData("$([MSBuild]::GetRegistryValueFromView('{0}', 'Value', null, RegistryView.Default, RegistryView.Registry32))")]
    [SupportedOSPlatform("windows")]
    public void RegistryReadsAreRecordedWithoutStoppingObservation(string expression)
    {
        TestRegistryKey registry = _env.WithTransientTestState(new TestRegistryKey());
        registry.Key.SetValue("Value", "first;value%", RegistryValueKind.String);
        _env.SetEnvironmentVariable("MSBUILD_TEST_AFTER_REGISTRY", "observed");
        string import = _env.CreateFile(_folder, "after.props", "<Project />").Path;
        expression = string.Format(CultureInfo.InvariantCulture, expression, registry.Key.Name);
        string project = CreateProject($"""
            <Project>
              <PropertyGroup>
                <First>{expression}</First>
                <Second>{expression}</Second>
                <After>$([System.Environment]::GetEnvironmentVariable('MSBUILD_TEST_AFTER_REGISTRY'))</After>
              </PropertyGroup>
              <Import Project="after.props" />
            </Project>
            """);

        ProjectInstance recorded = EvaluateWithAndWithoutRecording(project);
        EvaluationInputs inputs = recorded.EvaluationInputs.ShouldNotBeNull();

        recorded.GetPropertyValue("First").ShouldBe("first;value%");
        inputs.NonCacheable.ShouldBe(NonCacheableReason.None);
        inputs.RegistryReads.Length.ShouldBe(2);
        foreach (RegistryRead read in inputs.RegistryReads)
        {
            read.KeyName.ShouldBe(registry.Key.Name);
            read.ValueName.ShouldBe("Value");
            read.Value.ShouldBe("first;value%");
            if (expression.Contains("GetRegistryValueFromView"))
            {
                read.RequestedViews.ShouldBe(["RegistryView.Default", "RegistryView.Registry32"]);
            }
            else
            {
                read.RequestedViews.ShouldBeEmpty();
            }
        }
        inputs.EnvironmentReads["MSBUILD_TEST_AFTER_REGISTRY"].ShouldBe("observed");
        inputs.Files.ContainsKey(import).ShouldBeTrue();

        registry.Key.SetValue("Value", "changed", RegistryValueKind.String);

        inputs.RegistryReads[0].Value.ShouldBe("first;value%");
    }

    [WindowsOnlyFact]
    [SupportedOSPlatform("windows")]
    public void MissingDefaultAndFallbackRegistryValuesAreRecorded()
    {
        TestRegistryKey registry = _env.WithTransientTestState(new TestRegistryKey());
        registry.Key.SetValue(string.Empty, "default", RegistryValueKind.String);
        registry.Key.SetValue("Empty", string.Empty, RegistryValueKind.String);
        string keyName = registry.Key.Name;
        string project = CreateProject($"""
            <Project>
              <PropertyGroup>
                <Default>$(Registry:{keyName})</Default>
                <Empty>$(Registry:{keyName}@Empty)</Empty>
                <Missing>$(Registry:{keyName}@Missing)</Missing>
                <MissingFunction>$([MSBuild]::GetRegistryValue('{keyName}', 'Missing'))</MissingFunction>
                <Fallback>$([MSBuild]::GetRegistryValue('{keyName}', 'Missing', 'fallback'))</Fallback>
                <ViewFallback>$([MSBuild]::GetRegistryValueFromView('{keyName}\MissingKey', 'Missing', 'view fallback', RegistryView.Default))</ViewFallback>
              </PropertyGroup>
            </Project>
            """);

        EvaluationInputs inputs = Evaluate(project);

        inputs.NonCacheable.ShouldBe(NonCacheableReason.None);
        inputs.RegistryReads.Length.ShouldBe(6);
        inputs.RegistryReads[0].ValueName.ShouldBe(string.Empty);
        inputs.RegistryReads[0].Value.ShouldBe("default");
        inputs.RegistryReads[1].Value.ShouldBe(string.Empty);
        inputs.RegistryReads[2].Value.ShouldBeNull();
        inputs.RegistryReads[3].Value.ShouldBeNull();
        inputs.RegistryReads[4].Value.ShouldBe("fallback");
        inputs.RegistryReads[5].KeyName.ShouldBe(keyName + @"\MissingKey");
        inputs.RegistryReads[5].Value.ShouldBe("view fallback");
    }

    [WindowsOnlyTheory]
    [InlineData(RegistryValueKind.DWord)]
    [InlineData(RegistryValueKind.QWord)]
    [InlineData(RegistryValueKind.Binary)]
    [InlineData(RegistryValueKind.MultiString)]
    [InlineData(RegistryValueKind.ExpandString)]
    [SupportedOSPlatform("windows")]
    public void RegistryObservationsPreserveReturnedValueTypes(RegistryValueKind kind)
    {
        TestRegistryKey registry = _env.WithTransientTestState(new TestRegistryKey());
        _env.SetEnvironmentVariable("MSBUILD_TEST_REGISTRY_EXPANSION", "expanded");
        object value = kind switch
        {
            RegistryValueKind.DWord => 123,
            RegistryValueKind.QWord => 123456789123456789L,
            RegistryValueKind.Binary => (byte[])[1, 2, 3],
            RegistryValueKind.MultiString => (string[])["one;two", "three"],
            RegistryValueKind.ExpandString => "%MSBUILD_TEST_REGISTRY_EXPANSION%",
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        registry.Key.SetValue("Value", value, kind);
        string project = CreateProject($"""
            <Project>
              <PropertyGroup>
                <Direct>$(Registry:{registry.Key.Name}@Value)</Direct>
                <Function>$([MSBuild]::GetRegistryValue('{registry.Key.Name}', 'Value'))</Function>
              </PropertyGroup>
            </Project>
            """);

        EvaluationInputs inputs = Evaluate(project);

        inputs.RegistryReads.Length.ShouldBe(2);
        foreach (RegistryRead read in inputs.RegistryReads)
        {
            if (value is byte[] bytes)
            {
                read.Value.ShouldBeOfType<ImmutableArray<byte>>().ToArray().ShouldBe(bytes);
            }
            else if (value is string[] strings)
            {
                read.Value.ShouldBeOfType<ImmutableArray<string>>().ToArray().ShouldBe(strings);
            }
            else
            {
                read.Value.ShouldBe(kind == RegistryValueKind.ExpandString ? "expanded" : value);
            }
        }
        inputs.NonCacheable.ShouldBe(NonCacheableReason.None);
    }

    [Fact]
    public void RegistryObservationCopiesArraysAndPreservesRepeatedReads()
    {
        var recorder = new EvaluationInputRecorder();
        byte[] bytes = [1, 2];
        string[] strings = ["first", "second"];
        recorder.RecordRegistryRead("key", "bytes", bytes);
        recorder.RecordRegistryRead("key", "strings", strings);
        bytes[0] = 3;
        strings[0] = "changed";
        recorder.RecordRegistryRead("key", "bytes", bytes);
        EvaluationInputKey key = Evaluate(CreateProject("<Project />")).Key;
        EvaluationInputs inputs = recorder.Freeze(key);
        recorder.RecordRegistryRead("key", "after-freeze", "ignored");

        inputs.RegistryReads.Length.ShouldBe(3);
        inputs.RegistryReads[0].Value.ShouldBeOfType<ImmutableArray<byte>>().ToArray().ShouldBe([1, 2]);
        inputs.RegistryReads[1].Value.ShouldBeOfType<ImmutableArray<string>>().ToArray().ShouldBe(["first", "second"]);
        inputs.RegistryReads[2].Value.ShouldBeOfType<ImmutableArray<byte>>().ToArray().ShouldBe([3, 2]);
    }

    [WindowsOnlyFact]
    [SupportedOSPlatform("windows")]
    public void RegistryObservationUsesDecodedKeyAndValueNames()
    {
        TestRegistryKey registry = _env.WithTransientTestState(new TestRegistryKey());
        using RegistryKey nested = registry.Key.CreateSubKey("key@part");
        nested.SetValue("value@part", "value", RegistryValueKind.String);
        string expressionKey = nested.Name.Replace("@", "%40");
        string project = CreateProject($"""
            <Project>
              <PropertyGroup>
                <Value>$(Registry:{expressionKey}@value%40part)</Value>
              </PropertyGroup>
            </Project>
            """);

        RegistryRead read = Evaluate(project).RegistryReads.ShouldHaveSingleItem();

        read.KeyName.ShouldBe(nested.Name);
        read.ValueName.ShouldBe("value@part");
        read.Value.ShouldBe("value");
    }

    [WindowsOnlyFact]
    [SupportedOSPlatform("windows")]
    public void RegistryObservationUsesCoercedValueName()
    {
        TestRegistryKey registry = _env.WithTransientTestState(new TestRegistryKey());
        registry.Key.SetValue("123", "numbered value", RegistryValueKind.String);
        string project = CreateProject($"""
            <Project>
              <PropertyGroup>
                <Value>$([MSBuild]::GetRegistryValue('{registry.Key.Name}', $([System.Int32]::Parse('123')), 'fallback'))</Value>
              </PropertyGroup>
            </Project>
            """);

        ProjectInstance instance = EvaluateWithAndWithoutRecording(project);
        RegistryRead read = instance.EvaluationInputs.ShouldNotBeNull().RegistryReads.ShouldHaveSingleItem();

        instance.GetPropertyValue("Value").ShouldBe("numbered value");
        read.KeyName.ShouldBe(registry.Key.Name);
        read.ValueName.ShouldBe("123");
        read.Value.ShouldBe("numbered value");
    }

    [WindowsOnlyTheory]
    [InlineData("$([MSBuild]::GetRegistryValueFromView('{0}', 'Value', 'fallback'))", "fallback", false)]
    [InlineData("$([MSBuild]::GetRegistryValueFromView('{0}', 'Value', null))", null, false)]
    [InlineData("$([MSBuild]::GetRegistryValueFromView(null, null, 'fallback'))", "fallback", true)]
    [SupportedOSPlatform("windows")]
    public void RegistryRequestsWithoutExplicitViewsPreserveExistingBehavior(string expression, string? expected, bool nullKey)
    {
        TestRegistryKey registry = _env.WithTransientTestState(new TestRegistryKey());
        registry.Key.SetValue("Value", "stored", RegistryValueKind.String);
        expression = string.Format(CultureInfo.InvariantCulture, expression, registry.Key.Name);
        string project = CreateProject($"""
            <Project>
              <PropertyGroup>
                <Value>{expression}</Value>
              </PropertyGroup>
            </Project>
            """);

        ProjectInstance instance = EvaluateWithAndWithoutRecording(project);
        EvaluationInputs inputs = instance.EvaluationInputs.ShouldNotBeNull();
        RegistryRead read = inputs.RegistryReads.ShouldHaveSingleItem();

        instance.GetPropertyValue("Value").ShouldBe(expected ?? string.Empty);
        inputs.NonCacheable.ShouldBe(NonCacheableReason.None);
        read.KeyName.ShouldBe(nullKey ? null : registry.Key.Name);
        read.ValueName.ShouldBe(nullKey ? string.Empty : "Value");
        read.Value.ShouldBe(expected);
        read.RequestedViews.ShouldBeEmpty();
    }

    [WindowsOnlyTheory]
    [InlineData(false)]
    [InlineData(true)]
    [SupportedOSPlatform("windows")]
    public void RegistryViewParamsArrayIsNotConfusedWithANestedArray(bool nested)
    {
        TestRegistryKey registry = _env.WithTransientTestState(new TestRegistryKey());
        registry.Key.SetValue("Value", "stored", RegistryValueKind.String);
        string views = "$([System.String]::Copy('RegistryView.Default;RegistryView.Registry32').Split(';'))";
        if (nested)
        {
            views += ", RegistryView.Registry64";
        }
        string project = CreateProject($"""
            <Project>
              <PropertyGroup>
                <Value>$([MSBuild]::GetRegistryValueFromView('{registry.Key.Name}', 'Value', null, {views}))</Value>
              </PropertyGroup>
            </Project>
            """);

        ProjectInstance instance = EvaluateWithAndWithoutRecording(project);
        EvaluationInputs inputs = instance.EvaluationInputs.ShouldNotBeNull();
        RegistryRead read = inputs.RegistryReads.ShouldHaveSingleItem();

        instance.GetPropertyValue("Value").ShouldBe("stored");
        inputs.NonCacheable.ShouldBe(NonCacheableReason.None);
        read.Value.ShouldBe("stored");
        read.RequestedViews.ShouldBe(nested
            ? ["RegistryView.Registry64"]
            : ["RegistryView.Default", "RegistryView.Registry32"]);
    }

    [WindowsOnlyFact]
    [SupportedOSPlatform("windows")]
    public void IgnoredRegistryViewDoesNotInvokeToString()
    {
        TestRegistryKey registry = _env.WithTransientTestState(new TestRegistryKey());
        string project = CreateProject($"""
            <Project>
              <PropertyGroup>
                <Value>$([MSBuild]::GetRegistryValueFromView('{registry.Key.Name}', 'Missing', 'fallback', $([System.UriBuilder]::new('http://:x@localhost'))))</Value>
              </PropertyGroup>
            </Project>
            """);

        ProjectInstance instance = EvaluateWithAndWithoutRecording(project);
        EvaluationInputs inputs = instance.EvaluationInputs.ShouldNotBeNull();
        RegistryRead read = inputs.RegistryReads.ShouldHaveSingleItem();

        instance.GetPropertyValue("Value").ShouldBe("fallback");
        inputs.NonCacheable.ShouldBe(NonCacheableReason.None);
        read.Value.ShouldBe("fallback");
        read.RequestedViews.ShouldBeEmpty();
    }

    [WindowsOnlyFact]
    [SupportedOSPlatform("windows")]
    public void RegistryFallbackIsCapturedBeforeChainedMutation()
    {
        TestRegistryKey registry = _env.WithTransientTestState(new TestRegistryKey());
        string project = CreateProject($"""
            <Project>
              <PropertyGroup>
                <Value>$([MSBuild]::GetRegistryValue('{registry.Key.Name}', 'Missing', $([System.String]::Copy('ab').ToCharArray())).SetValue($([System.Char]::Parse('z')), 0))</Value>
              </PropertyGroup>
            </Project>
            """);

        ProjectInstance instance = EvaluateWithAndWithoutRecording(project);
        RegistryRead read = instance.EvaluationInputs.ShouldNotBeNull().RegistryReads.ShouldHaveSingleItem();

        read.Value.ShouldBeOfType<ImmutableArray<char>>().ToArray().ShouldBe(['a', 'b']);
    }

    [WindowsOnlyFact]
    [SupportedOSPlatform("windows")]
    public void UnsupportedRegistryFallbackRejectsRecordingWithoutChangingEvaluation()
    {
        TestRegistryKey registry = _env.WithTransientTestState(new TestRegistryKey());
        string project = CreateProject($"""
            <Project>
              <PropertyGroup>
                <Value>$([MSBuild]::GetRegistryValue('{registry.Key.Name}', 'Missing', $([System.UriBuilder]::new('https://example.invalid/path'))))</Value>
              </PropertyGroup>
            </Project>
            """);

        ProjectInstance instance = EvaluateWithAndWithoutRecording(project);
        EvaluationInputs inputs = instance.EvaluationInputs.ShouldNotBeNull();

        inputs.NonCacheable.ShouldBe(NonCacheableReason.UnsupportedRegistryValue);
        inputs.RegistryReads.ShouldBeEmpty();
    }

    [Fact]
    public void SdkResolutionIsRecordedAndImportedFilesAreValidated()
    {
        TransientTestFolder sdkFolder = _env.CreateFolder(Path.Combine(_folder.Path, "sdk"), createFolder: true);
        string sdkProps = _env.CreateFile(sdkFolder, "Sdk.props", "<Project><PropertyGroup><FromSdk>props</FromSdk></PropertyGroup></Project>").Path;
        _env.CreateFile(sdkFolder, "Sdk.targets", "<Project />");
        string project = CreateProject("""
            <Project Sdk="TestSdk">
              <PropertyGroup>
                <A>$(FromSdk)</A>
              </PropertyGroup>
            </Project>
            """);
        var recorded = new SdkResult(new SdkReference("TestSdk", null, null), sdkFolder.Path, "1.0", warnings: null);
        ProjectOptions options = SdkUtilities.CreateProjectOptionsWithResolver(new SdkUtilities.ConfigurableMockSdkResolver(recorded));
        options.ProjectCollection = _env.CreateProjectCollection().Collection;

        EvaluationInputs inputs = Evaluate(project, options);

        inputs.SdkResolutions.ShouldHaveSingleItem().Reference.Name.ShouldBe("TestSdk");
        inputs.Files[sdkProps].Kind.ShouldBe(PathKind.File);
        inputs.SdkResolutions[0].Result.ShouldBe(recorded);
    }

    [Theory]
    [InlineData("<Stamp Include=\"@(Compile->'%(ModifiedTime)')\" />")]
    [InlineData("<Stamp Include=\"@(Compile->ModifiedTime())\" />")]
    [InlineData("<Stamp Include=\"@(Compile)\"><Time>%(CreatedTime)</Time></Stamp>")]
    public void ItemTimestampMetadataIsNotCacheable(string stampItem)
    {
        _env.CreateFile(_folder, "a.cs", string.Empty);
        string project = CreateProject($"""
            <Project>
              <ItemGroup>
                <Compile Include="a.cs" />
                {stampItem}
              </ItemGroup>
            </Project>
            """);

        EvaluationInputs inputs = Evaluate(project);

        inputs.NonCacheable.ShouldBe(NonCacheableReason.ItemTimestampMetadata);
    }

    [Fact]
    public void NamesContainingTimestampModifiersAreCacheable()
    {
        string project = CreateProject("""
            <Project>
              <PropertyGroup>
                <LastModifiedTime>never</LastModifiedTime>
                <Value>$(LastModifiedTime)</Value>
              </PropertyGroup>
              <ItemGroup>
                <Compile Include="a.cs">
                  <CreatedTimeZone>UTC</CreatedTimeZone>
                </Compile>
                <Stamp Include="@(Compile->'%(CreatedTimeZone)')" />
              </ItemGroup>
            </Project>
            """);

        EvaluationInputs inputs = Evaluate(project);

        inputs.NonCacheable.ShouldBe(NonCacheableReason.None);
    }

    [WindowsOnlyFact]
    public void RecorderFailureMarksNonCacheableWithoutChangingEvaluation()
    {
        // Path.GetFullPath rejects paths over 32,767 characters on Windows; File.Exists just returns false.
        string project = CreateProject("""
            <Project>
              <PropertyGroup>
                <Name>a</Name>
                <LongPath>$(Name.PadLeft(40000, 'a'))</LongPath>
                <Found>$([System.IO.File]::Exists('$(LongPath)'))</Found>
              </PropertyGroup>
            </Project>
            """);

        ProjectInstance instance = ProjectInstance.FromFile(project, CreateOptions());

        instance.GetPropertyValue("Found").ShouldBe("False");
        instance.EvaluationInputs.ShouldNotBeNull().NonCacheable.ShouldBe(NonCacheableReason.RecorderFailure);
    }

    [Fact]
    public void ConcurrentEvaluationsOnSharedContextRecordIndependently()
    {
        const string xml = """
            <Project>
              <ItemGroup>
                <Compile Include="*.cs" />
              </ItemGroup>
            </Project>
            """;
        string first = CreateProject(xml, "first.proj");
        TransientTestFolder secondFolder = _env.CreateFolder(createFolder: true);
        string second = _env.CreateFile(secondFolder, "second.proj", xml.Cleanup()).Path;
        EvaluationContext shared = EvaluationContext.Create(EvaluationContext.SharingPolicy.Shared);
        ProjectCollection collection = _env.CreateProjectCollection().Collection;
        var instances = new ProjectInstance[2];

        Parallel.Invoke(
            () => instances[0] = ProjectInstance.FromFile(first, new ProjectOptions { ProjectCollection = collection, EvaluationContext = shared }),
            () => instances[1] = ProjectInstance.FromFile(second, new ProjectOptions { ProjectCollection = collection, EvaluationContext = shared }));

        EvaluationInputs firstInputs = instances[0].EvaluationInputs.ShouldNotBeNull();
        EvaluationInputs secondInputs = instances[1].EvaluationInputs.ShouldNotBeNull();
        firstInputs.Files.ContainsKey(first).ShouldBeTrue();
        firstInputs.Files.ContainsKey(_folder.Path).ShouldBeTrue();
        firstInputs.Files.ContainsKey(second).ShouldBeFalse();
        firstInputs.Files.ContainsKey(secondFolder.Path).ShouldBeFalse();
        secondInputs.Files.ContainsKey(second).ShouldBeTrue();
        secondInputs.Files.ContainsKey(first).ShouldBeFalse();
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
    public void CommonTargetsProjectIsCacheableAndValidatesAsCurrent()
    {
        string project = CreateCommonTargetsProject();

        EvaluationInputs inputs = Evaluate(project);

        inputs.NonCacheable.ShouldBe(NonCacheableReason.None, inputs.NonCacheableDetail);
        inputs.Files.Count.ShouldBeGreaterThan(5);
        Evaluate(project).Files.Keys.ShouldBe(inputs.Files.Keys, ignoreOrder: true);
    }

    [Fact]
    public void RecordingDoesNotChangeEvaluationResult()
    {
        string project = CreateCommonTargetsProject();
        SetRecording(enabled: false);
        ProjectInstance plain = ProjectInstance.FromFile(project, CreateOptions());
        SetRecording(enabled: true);
        ProjectInstance recorded = ProjectInstance.FromFile(project, CreateOptions());

        plain.EvaluationInputs.ShouldBeNull();
        recorded.EvaluationInputs.ShouldNotBeNull();
        List<string> plainSnapshot = Snapshot(plain);
        List<string> recordedSnapshot = Snapshot(recorded);
        recordedSnapshot.Except(plainSnapshot).ShouldBeEmpty();
        plainSnapshot.Except(recordedSnapshot).ShouldBeEmpty();
    }

    [Fact]
    public void RecordingThroughASharedContextDoesNotChangeEvaluationResult()
    {
        // The recording copy shares the context's glob cache, so the second evaluation reuses the expansions and must still
        // record every traversed directory, replayed from the cache, as a dependency a new file invalidates.
        string project = CreateCommonTargetsProject();
        SetRecording(enabled: false);
        ProjectInstance plain = ProjectInstance.FromFile(project, CreateOptions());
        SetRecording(enabled: true);
        EvaluationContext shared = EvaluationContext.Create(EvaluationContext.SharingPolicy.Shared);
        ProjectInstance first = ProjectInstance.FromFile(project, new ProjectOptions { ProjectCollection = CreateOptions().ProjectCollection, EvaluationContext = shared });
        ProjectInstance second = ProjectInstance.FromFile(project, new ProjectOptions { ProjectCollection = CreateOptions().ProjectCollection, EvaluationContext = shared });

        List<string> plainSnapshot = Snapshot(plain);
        Snapshot(first).Except(plainSnapshot).ShouldBeEmpty();
        Snapshot(second).Except(plainSnapshot).ShouldBeEmpty();
        plainSnapshot.Except(Snapshot(second)).ShouldBeEmpty();
        EvaluationInputs firstInputs = first.EvaluationInputs.ShouldNotBeNull();
        EvaluationInputs secondInputs = second.EvaluationInputs.ShouldNotBeNull();
        secondInputs.NonCacheable.ShouldBe(NonCacheableReason.None);
        secondInputs.Files.Keys.Except(firstInputs.Files.Keys, StringComparer.OrdinalIgnoreCase).ShouldBeEmpty();
        firstInputs.Files.Keys.Except(secondInputs.Files.Keys, StringComparer.OrdinalIgnoreCase).ShouldBeEmpty();
    }

#if FEATURE_SYMLINK_TARGET
    [RequiresSymbolicLinksFact]
    public void LinkedFileIsNotCacheable()
    {
        string target = _env.CreateFile(_folder, "target.props", "<Project />").Path;
        string link = Path.Combine(_folder.Path, "linked.props");
        File.CreateSymbolicLink(link, target);
        string project = CreateProject("""
            <Project>
              <Import Project="linked.props" Condition="Exists('linked.props')" />
            </Project>
            """);

        EvaluationInputs inputs = Evaluate(project);

        inputs.NonCacheable.ShouldBe(NonCacheableReason.Link);
        inputs.NonCacheableDetail.ShouldBe(link);
    }

    [RequiresSymbolicLinksFact]
    public void LinkedDirectoryIsNotCacheable()
    {
        string target = _env.CreateFolder(Path.Combine(_folder.Path, "target"), createFolder: true).Path;
        File.WriteAllText(Path.Combine(target, "a.cs"), string.Empty);
        string link = Path.Combine(_folder.Path, "linked");
        Directory.CreateSymbolicLink(link, target);
        string project = CreateProject("""
            <Project>
              <ItemGroup>
                <Compile Include="linked\**\*.cs" />
              </ItemGroup>
            </Project>
            """);

        EvaluationInputs inputs = Evaluate(project);

        inputs.NonCacheable.ShouldBe(NonCacheableReason.Link);
        inputs.NonCacheableDetail.ShouldBe(link);
    }
#endif

    [Fact]
    public void ExpandEnvironmentVariablesRecordsEachReferencedVariable()
    {
        _env.SetEnvironmentVariable("MSBUILD_TEST_EXPAND_ROOT", "first");
        _env.SetEnvironmentVariable("MSBUILD_TEST_EXPAND_MISSING", null);
        string project = CreateProject("""
            <Project>
              <PropertyGroup>
                <Value>$([System.Environment]::ExpandEnvironmentVariables('%MSBUILD_TEST_EXPAND_ROOT%\src\%MSBUILD_TEST_EXPAND_MISSING%'))</Value>
              </PropertyGroup>
            </Project>
            """);

        EvaluationInputs inputs = Evaluate(project);

        inputs.NonCacheable.ShouldBe(NonCacheableReason.None);
        inputs.EnvironmentReads["MSBUILD_TEST_EXPAND_ROOT"].ShouldBe("first");
        inputs.EnvironmentReads["MSBUILD_TEST_EXPAND_MISSING"].ShouldBeNull();

        _env.SetEnvironmentVariable("MSBUILD_TEST_EXPAND_MISSING", "now set");

        inputs.EnvironmentReads["MSBUILD_TEST_EXPAND_MISSING"].ShouldBeNull();
    }

    [Fact]
    public void RecordedPathsShareOneStringAcrossEvaluations()
    {
        _env.CreateFile(_folder, "shared.props", "<Project />");
        string project = CreateProject("""
            <Project>
              <Import Project="shared.props" />
              <ItemGroup>
                <None Include="**/*.txt" />
              </ItemGroup>
            </Project>
            """);

        EvaluationInputs first = Evaluate(project);
        EvaluationInputs second = Evaluate(project);

        first.Files.Count.ShouldBeGreaterThan(1);
        foreach (string path in first.Files.Keys)
        {
            string counterpart = second.Files.Keys.Single(key => string.Equals(key, path, StringComparison.OrdinalIgnoreCase));
            ReferenceEquals(path, counterpart).ShouldBeTrue($"{path} was allocated again");
        }
    }

    [Fact]
    public void ItemExistsTransformRecordsProbes()
    {
        _env.CreateFile(_folder, "present.txt", "x");
        string missing = Path.Combine(_folder.Path, "missing.txt");
        string project = CreateProject("""
            <Project>
              <ItemGroup>
                <Candidate Include="present.txt;missing.txt" />
                <Kept Include="@(Candidate->Exists())" />
              </ItemGroup>
            </Project>
            """);

        ProjectInstance instance = ProjectInstance.FromFile(project, CreateOptions());
        EvaluationInputs inputs = instance.EvaluationInputs.ShouldNotBeNull();

        instance.GetItems("Kept").Select(item => item.EvaluatedInclude).ShouldBe(["present.txt"]);
        inputs.NonCacheable.ShouldBe(NonCacheableReason.None);
        inputs.Files[Path.Combine(_folder.Path, "present.txt")].Kind.ShouldBe(PathKind.File);
        inputs.Files[missing].Kind.ShouldBe(PathKind.Missing);
    }

    [Theory]
    [InlineData("Metadata('ModifiedTime')")]
    [InlineData("HasMetadata('CreatedTime')")]
    [InlineData("WithMetadataValue('AccessedTime', '')")]
    [InlineData("WithoutMetadataValue('ModifiedTime', '')")]
    [InlineData("AnyHaveMetadataValue('ModifiedTime', '')")]
    public void TransformsReadingTimestampMetadataByNameAreNotCacheable(string transform)
    {
        _env.CreateFile(_folder, "a.cs", "class A {}");
        string project = CreateProject($$"""
            <Project>
              <ItemGroup>
                <Compile Include="a.cs" />
                <Stamp Include="@(Compile->{{transform}})" />
              </ItemGroup>
            </Project>
            """);

        Evaluate(project).NonCacheable.ShouldBe(NonCacheableReason.ItemTimestampMetadata);
    }

    [Fact]
    public void MetadataReferenceWithWhitespaceReadingTimestampIsNotCacheable()
    {
        _env.CreateFile(_folder, "a.cs", "class A {}");
        string project = CreateProject("""
            <Project>
              <ItemGroup>
                <Compile Include="a.cs" />
                <Stamp Include="@(Compile->'%( ModifiedTime )')" />
              </ItemGroup>
            </Project>
            """);

        Evaluate(project).NonCacheable.ShouldBe(NonCacheableReason.ItemTimestampMetadata);
    }

    [Fact]
    public void RemoveMatchingOnTimestampMetadataIsNotCacheable()
    {
        _env.CreateFile(_folder, "a.cs", "class A {}");
        string project = CreateProject("""
            <Project>
              <ItemGroup>
                <Compile Include="a.cs" />
                <Old Include="a.cs" />
                <Compile Remove="@(Old)" MatchOnMetadata="ModifiedTime" />
              </ItemGroup>
            </Project>
            """);

        Evaluate(project).NonCacheable.ShouldBe(NonCacheableReason.ItemTimestampMetadata);
    }

    [Fact]
    public void ExistsOnLoadedProjectDeletedFromDiskIsNotCacheable()
    {
        ProjectCollection collection = _env.CreateProjectCollection().Collection;
        string loaded = _env.CreateFile(_folder, "loaded.props", "<Project />").Path;
        ProjectRootElement loadedElement = ProjectRootElement.Open(loaded, collection);
        File.Delete(loaded);
        collection.ProjectRootElementCache.TryGet(loaded).ShouldBeSameAs(loadedElement);
        _env.CreateFile(_folder, "other.props", "<Project><PropertyGroup><Loaded>true</Loaded></PropertyGroup></Project>");
        string project = CreateProject("""
            <Project>
              <Import Project="other.props" Condition="Exists('loaded.props')" />
            </Project>
            """);

        var evaluated = new Project(project, globalProperties: null, toolsVersion: null, collection);
        EvaluationInputs inputs = evaluated.EvaluationInputs.ShouldNotBeNull();

        evaluated.GetPropertyValue("Loaded").ShouldBe("true");
        inputs.NonCacheable.ShouldBe(NonCacheableReason.ConflictingObservation);
        inputs.NonCacheableDetail.ShouldBe(loaded);
        GC.KeepAlive(loadedElement);
    }

    [Fact]
    public void ToolLocationHelperProbingCallerPathsIsNotCacheable()
    {
        _env.CreateFile(_folder, "a.txt", "x");
        string project = CreateProject("""
            <Project>
              <PropertyGroup>
                <Root>$([Microsoft.Build.Utilities.ToolLocationHelper]::FindRootFolderWhereAllFilesExist('$(MSBuildProjectDirectory)', 'a.txt'))</Root>
              </PropertyGroup>
            </Project>
            """);

        EvaluationInputs inputs = Evaluate(project);

        inputs.NonCacheable.ShouldBe(NonCacheableReason.UnclassifiedPropertyFunction);
        inputs.NonCacheableDetail.ShouldNotBeNull().ShouldContain("FindRootFolderWhereAllFilesExist");
    }

    [Theory]
    [InlineData(@"custom\root")]
    [InlineData("custom/root")]
    public void ToolLocationHelperGivenARelativeRootIsNotCacheable(string root)
    {
        string project = CreateProject($"""
            <Project>
              <PropertyGroup>
                <Assemblies>$([Microsoft.Build.Utilities.ToolLocationHelper]::GetPathToReferenceAssemblies('.NETFramework', 'v4.7.2', '', '{root}'))</Assemblies>
              </PropertyGroup>
            </Project>
            """);

        ProjectInstance instance = EvaluateWithAndWithoutRecording(project);
        EvaluationInputs inputs = instance.EvaluationInputs.ShouldNotBeNull();

        inputs.NonCacheable.ShouldBe(NonCacheableReason.UnclassifiedPropertyFunction);
        inputs.NonCacheableDetail.ShouldNotBeNull().ShouldContain("GetPathToReferenceAssemblies");
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
    public void ToolLocationHelperInstalledStateIsCacheable()
    {
        string project = CreateProject("""
            <Project>
              <PropertyGroup>
                <Libraries>$([Microsoft.Build.Utilities.ToolLocationHelper]::GetPathToStandardLibraries('.NETFramework', 'v4.7.2', ''))</Libraries>
              </PropertyGroup>
            </Project>
            """);

        Evaluate(project).NonCacheable.ShouldBe(NonCacheableReason.None);
    }

    [Fact]
    public void HostDirectoryCacheIsNotCacheable()
    {
        string project = CreateProject("<Project />");
        ProjectOptions options = CreateOptions();
        options.DirectoryCacheFactory = new PassThroughDirectoryCache();

        Evaluate(project, options).NonCacheable.ShouldBe(NonCacheableReason.HostFileSystem);
    }

    [Fact]
    public void HostFileSystemIsNotCacheable()
    {
        string project = CreateProject("<Project />");
        ProjectOptions options = CreateOptions();
        options.EvaluationContext = EvaluationContext.Create(EvaluationContext.SharingPolicy.Shared, new PassThroughFileSystem());

        Evaluate(project, options).NonCacheable.ShouldBe(NonCacheableReason.HostFileSystem);
    }

    [Theory]
    [InlineData("MsBuildCacheFileExistence")]
    [InlineData("MsBuildCacheFileEnumerations")]
    public void ProcessWideFileCachesAreNotCacheable(string variable)
    {
        _env.SetEnvironmentVariable(variable, "1");
        Traits.UpdateFromEnvironment();
        string project = CreateProject("<Project />");

        Evaluate(project).NonCacheable.ShouldBe(NonCacheableReason.ProcessWideCache);
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
        with.Files[config].Kind.ShouldBe(PathKind.File);
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

    private string CreateCommonTargetsProject()
    {
        _env.CreateFile(_folder, "Class1.cs", "class C {}");
        // Keep this cacheable fixture independent of the .NET Framework Windows SDK registry probe.
        return CreateProject("""
            <Project>
              <Import Project="$(MSBuildBinPath)\Microsoft.Common.props" />
              <PropertyGroup>
                <_EnableDefaultWindowsPlatform>false</_EnableDefaultWindowsPlatform>
                <OutputType>Library</OutputType>
                <TargetFrameworkVersion>v4.7.2</TargetFrameworkVersion>
              </PropertyGroup>
              <ItemGroup>
                <Compile Include="**/*.cs" />
              </ItemGroup>
              <Import Project="$(MSBuildBinPath)\Microsoft.CSharp.targets" />
            </Project>
            """);
    }

    /// <summary>
    /// Evaluated properties, items with their metadata, item definitions, imports, and targets, without the environment
    /// variable that turns recording on.
    /// </summary>
    private static List<string> Snapshot(ProjectInstance instance)
    {
        List<string> snapshot = [];
        foreach (ProjectPropertyInstance property in instance.Properties)
        {
            if (!string.Equals(property.Name, EnableVariable, StringComparison.OrdinalIgnoreCase))
            {
                snapshot.Add($"{property.Name}={property.EvaluatedValue}");
            }
        }

        foreach (ProjectItemInstance item in instance.Items)
        {
            snapshot.Add($"{item.ItemType}:{item.EvaluatedInclude}");
            foreach (ProjectMetadataInstance metadata in item.Metadata)
            {
                snapshot.Add($"{item.ItemType}:{item.EvaluatedInclude}:{metadata.Name}={metadata.EvaluatedValue}");
            }
        }

        foreach (KeyValuePair<string, ProjectItemDefinitionInstance> definition in instance.ItemDefinitions)
        {
            foreach (ProjectMetadataInstance metadata in definition.Value.Metadata)
            {
                snapshot.Add($"definition {definition.Key}:{metadata.Name}={metadata.EvaluatedValue}");
            }
        }

        foreach (string import in instance.ImportPaths)
        {
            snapshot.Add($"import {import}");
        }

        foreach (string target in instance.Targets.Keys)
        {
            snapshot.Add($"target {target}");
        }

        return snapshot;
    }

    private ProjectInstance EvaluateWithAndWithoutRecording(string project)
    {
        SetRecording(enabled: false);
        ProjectInstance plain = ProjectInstance.FromFile(project, CreateOptions());
        SetRecording(enabled: true);
        ProjectInstance recorded = ProjectInstance.FromFile(project, CreateOptions());

        Snapshot(recorded).ShouldBe(Snapshot(plain), ignoreOrder: true);
        return recorded;
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
        _output.WriteLine($"{inputs.Files.Count} files, non-cacheable: {inputs.NonCacheable} {inputs.NonCacheableDetail}");
        return inputs;
    }

    /// <summary>
    /// Rewrites a file and moves its timestamp forward so the change is visible on file systems with coarse timestamps.
    /// </summary>
    private static void Touch(string path, string contents)
    {
        File.WriteAllText(path, contents);
        File.SetLastWriteTimeUtc(path, File.GetLastWriteTimeUtc(path).AddSeconds(2));
    }

    [SupportedOSPlatform("windows")]
    private sealed class TestRegistryKey : TransientTestState
    {
        private readonly string _subKey = $@"Software\MSBuild_EvaluationInputs_{Guid.NewGuid():N}";

        internal TestRegistryKey()
        {
            Key = Registry.CurrentUser.CreateSubKey(_subKey);
        }

        internal RegistryKey Key { get; }

        public override void Revert()
        {
            Key.Dispose();
            Registry.CurrentUser.DeleteSubKeyTree(_subKey, throwOnMissingSubKey: false);
        }
    }

    private sealed class PassThroughFileSystem : MSBuildFileSystemBase
    {
    }

    private sealed class PassThroughDirectoryCache : IDirectoryCacheFactory, IDirectoryCache
    {
        public IDirectoryCache GetDirectoryCacheForEvaluation(int evaluationId) => this;

        public bool FileExists(string path) => File.Exists(path);

        public bool DirectoryExists(string path) => Directory.Exists(path);

        public IEnumerable<TResult> EnumerateFiles<TResult>(string path, string pattern, FindPredicate predicate, FindTransform<TResult> transform) =>
            Select(Directory.EnumerateFiles(path, pattern), predicate, transform);

        public IEnumerable<TResult> EnumerateDirectories<TResult>(string path, string pattern, FindPredicate predicate, FindTransform<TResult> transform) =>
            Select(Directory.EnumerateDirectories(path, pattern), predicate, transform);

        private static List<TResult> Select<TResult>(IEnumerable<string> paths, FindPredicate predicate, FindTransform<TResult> transform)
        {
            List<TResult> results = [];
            foreach (string path in paths)
            {
                ReadOnlySpan<char> name = Path.GetFileName(path).AsSpan();
                if (predicate(ref name))
                {
                    results.Add(transform(ref name));
                }
            }

            return results;
        }
    }
}
