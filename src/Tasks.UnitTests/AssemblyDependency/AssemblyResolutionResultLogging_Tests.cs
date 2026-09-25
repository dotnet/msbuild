// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Shouldly;
using Xunit;
using TaskItem = Microsoft.Build.Utilities.TaskItem;

namespace Microsoft.Build.UnitTests.ResolveAssemblyReference_Tests;

public sealed class AssemblyResolutionResultLogging_Tests(ITestOutputHelper output)
{
    private const string RuntimeVersion = "v4.0.30319";

    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(true, null)]
    [InlineData(false, null)]
    public void ResultReconstructsLegacyMessages(bool isPrimary, bool? privateValue)
    {
        Reference reference = CreateReference(isPrimary, privateValue);
        MockEngine legacy = LogReference(reference, enabled: false);
        MockEngine grouped = LogReference(reference, enabled: true);

        var result = grouped.MessageEvents.ShouldHaveSingleItem().ShouldBeOfType<AssemblyResolutionResultEventArgs>();
        result.IsPrimary.ShouldBe(isPrimary);
        result.IsCopyLocal.ShouldBe(privateValue != false);
        result.FullPath.ShouldBe(reference.FullPath);
        result.ResolvedSearchPath.ShouldBe(reference.ResolvedSearchPath);
        result.Importance.ShouldBe(MessageImportance.Low);
        result.FormatMessage(CultureInfo.CurrentUICulture).ShouldBe(
            string.Join(Environment.NewLine, legacy.MessageEvents.Select(e => e.Message)));
        legacy.MessageEvents.Length.ShouldBe(privateValue == false ? 4 : 3);
        grouped.WarningEvents.ShouldBeEmpty();
        grouped.ErrorEvents.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("error")]
    [InlineData("unresolved")]
    [InlineData("bad-image")]
    [InlineData("unification")]
    [InlineData("rejected-search")]
    [InlineData("related")]
    [InlineData("satellite")]
    [InlineData("scatter")]
    [InlineData("runtime")]
    [InlineData("winmd")]
    [InlineData("conflict")]
    [InlineData("dependency")]
    [InlineData("copy-reason")]
    public void ResultFallsBackWithoutChangingDiagnostics(string detail)
    {
        Reference reference = CreateReference(isPrimary: detail != "dependency", privateValue: false);
        switch (detail)
        {
            case "error":
                reference.AddError(new ReferenceResolutionException("Resolution detail", new IOException("Inner detail")));
                break;
            case "unresolved":
                reference.FullPath = string.Empty;
                reference.AddError(new ReferenceResolutionException("Missing reference", new IOException("Inner detail")));
                break;
            case "bad-image":
                reference.AddError(new BadImageReferenceException("Bad image", new BadImageFormatException("Inner detail")));
                break;
            case "unification":
                reference.AddPreUnificationVersion("parent.dll", new Version(1, 0), UnificationReason.FrameworkRetarget);
                break;
            case "rejected-search":
                reference.AddAssembliesConsideredAndRejected(
                    [new() { FileNameAttempted = "missing.dll", SearchPath = "path", Reason = NoMatchReason.FileNotFound }]);
                break;
            case "related":
                reference.AddRelatedFileExtension(".xml");
                break;
            case "satellite":
                reference.AddSatelliteFile("reference.resources.dll");
                break;
            case "scatter":
                reference.AttachScatterFiles(["reference.netmodule"]);
                break;
            case "runtime":
                reference.ImageRuntime = "v2.0.50727";
                break;
            case "winmd":
                reference.IsWinMDFile = true;
                break;
            case "conflict":
                reference.AddConflictVictim(new AssemblyNameExtension("Other"));
                break;
            case "dependency":
                reference.AddSourceItem(new TaskItem("parent"));
                break;
            case "copy-reason":
                reference = CreateReference(isPrimary: true, privateValue: null);
                reference.IsPrerequisite = true;
                SetCopyLocal(reference);
                break;
        }

        MockEngine legacy = LogReference(reference, enabled: false);
        MockEngine grouped = LogReference(reference, enabled: true);

        grouped.MessageEvents.ShouldNotContain(e => e is AssemblyResolutionResultEventArgs);
        grouped.MessageEvents.Select(e => (e.GetType(), e.Message, e.Importance, e.SenderName, e.HelpKeyword))
            .ShouldBe(legacy.MessageEvents.Select(e => (e.GetType(), e.Message, e.Importance, e.SenderName, e.HelpKeyword)));
        grouped.WarningEvents.Select(e => (e.Code, e.Message, e.File, e.LineNumber, e.ColumnNumber, e.HelpKeyword))
            .ShouldBe(legacy.WarningEvents.Select(e => (e.Code, e.Message, e.File, e.LineNumber, e.ColumnNumber, e.HelpKeyword)));
        grouped.ErrorEvents.ShouldBeEmpty();
        if (detail == "error")
        {
            grouped.WarningEvents.ShouldHaveSingleItem().Code.ShouldBe("MSB3245");
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ResultFormattingPreservesInvariantAndLocalizedResources(bool isPrimary)
    {
        var result = new AssemblyResolutionResultEventArgs(
            "Reference", "reference.dll", "{HintPathFromItem}", isPrimary, false,
            nameof(ResolveAssemblyReference), MessageImportance.Low, default);
        var rar = new ResolveAssemblyReference();
        CultureInfo[] cultures = [CultureInfo.InvariantCulture, CultureInfo.GetCultureInfo("fr-FR")];
        foreach (CultureInfo culture in cultures)
        {
            string expected = await Task.Run(() =>
            {
                CultureInfo.CurrentCulture = culture;
                CultureInfo.CurrentUICulture = culture;
                return string.Join(Environment.NewLine,
                    rar.Log.FormatResourceString(isPrimary ? "ResolveAssemblyReference.PrimaryReference" : "ResolveAssemblyReference.Dependency", "Reference"),
                    "    " + rar.Log.FormatResourceString("ResolveAssemblyReference.Resolved", "reference.dll"),
                    "    " + rar.Log.FormatResourceString("ResolveAssemblyReference.ResolvedFrom", "{HintPathFromItem}"),
                    "    " + rar.Log.FormatResourceString("ResolveAssemblyReference.NotCopyLocalBecauseIncomingItemAttributeOverrode"));
            });
            result.FormatMessage(culture).ShouldBe(expected);
        }

        result.Message.ShouldBe(result.FormatMessage(CultureInfo.InvariantCulture));
        Should.Throw<ArgumentNullException>(() => result.FormatMessage(null!));
    }

    [Fact]
    public void ResultRespectsMessageImportanceFiltering()
    {
        var engine = new MockEngine(output) { MinimumMessageImportance = MessageImportance.Normal };
        CreateTask(engine, enabled: true).LogReference(CreateReference(true, false), "Reference");
        engine.MessageEvents.ShouldBeEmpty();
    }

    private MockEngine LogReference(Reference reference, bool enabled)
    {
        var engine = new MockEngine(output);
        CreateTask(engine, enabled).LogReference(reference, "Reference");
        return engine;
    }

    private static ResolveAssemblyReference CreateTask(MockEngine engine, bool enabled)
    {
        Dictionary<string, string> variables = [];
        if (enabled)
        {
            variables.Add("MSBUILDLOGRARRESULTS", "1");
        }

        return new()
        {
            BuildEngine = engine,
            TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(
                Path.GetTempPath(), variables),
        };
    }

    private static Reference CreateReference(bool isPrimary, bool? privateValue)
    {
        var reference = new Reference(
            (string _, GetAssemblyRuntimeVersion _, FileExists _, out string imageRuntimeVersion, out bool isManagedWinmd) =>
            {
                imageRuntimeVersion = RuntimeVersion;
                isManagedWinmd = false;
                return false;
            },
            _ => true,
            _ => RuntimeVersion);
        if (isPrimary)
        {
            var item = new TaskItem("Reference");
            if (privateValue.HasValue)
            {
                item.SetMetadata("Private", privateValue.Value ? "true" : "false");
            }

            reference.MakePrimaryAssemblyReference(item, false, ".dll");
        }

        reference.FullPath = Path.Combine(Path.GetTempPath(), "reference.dll");
        reference.ResolvedSearchPath = "{HintPathFromItem}";
        reference.ImageRuntime = RuntimeVersion;
        SetCopyLocal(reference);
        return reference;
    }

    private static void SetCopyLocal(Reference reference)
        => reference.SetFinalCopyLocalState(
            new AssemblyNameExtension("Reference"), [], default, _ => RuntimeVersion, new Version(4, 0),
            _ => true, getAssemblyPathInGac: null!, copyLocalDependenciesWhenParentReferenceInGac: true,
            doNotCopyLocalIfInGac: false, referenceTable: null!);
}
