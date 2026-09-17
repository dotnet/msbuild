// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests;

public class AssemblyConflictLogger_Tests
{
    [Fact]
    public void ConsoleLoggerLocalizesStructuredConflictMessages()
    {
        (AssemblyConflictDependencyDetailsMessageEventArgs details, AssemblyConflictWarningEventArgs warning) = CreateEvents();
        var output = new StringBuilder();
        var eventSource = new EventSourceSink();
        var logger = new ConsoleLogger(LoggerVerbosity.Diagnostic, message => output.Append(message), null, null);
        logger.Initialize(eventSource);

        WithFrenchUICulture(() =>
        {
            eventSource.Consume(details);
            AssertFrenchDetailsOutput(output.ToString(), details);
            output.Clear();
            eventSource.Consume(warning);
        });

        AssertFrenchWarningOutput(output.ToString(), warning);
    }

    [Fact]
    public void TerminalLoggerLocalizesStructuredConflictMessages()
    {
        (AssemblyConflictDependencyDetailsMessageEventArgs details, AssemblyConflictWarningEventArgs warning) = CreateEvents();
        using var output = new StringWriter();
        using var terminal = new Terminal(output);
        var eventSource = new EventSourceSink();
        var logger = new TerminalLogger(terminal)
        {
            Verbosity = LoggerVerbosity.Diagnostic,
        };
        logger.Initialize(eventSource, nodeCount: 1);

        WithFrenchUICulture(() =>
        {
            eventSource.Consume(details);
            eventSource.Consume(warning);
        });

        AssertFrenchWarningOutput(output.ToString(), warning);
        details.IsMessageMaterialized.ShouldBeFalse();
    }

    private static void AssertFrenchDetailsOutput(
        string output,
        AssemblyConflictDependencyDetailsMessageEventArgs details)
    {
        output.ShouldContain("Références qui dépendent de");
        output.ShouldContain("Item Include d'un fichier projet");
        output.ShouldNotContain("References which depend on");
        output.ShouldNotContain("Project file item includes");
        details.IsMessageMaterialized.ShouldBeFalse();
    }

    private static void AssertFrenchWarningOutput(
        string output,
        AssemblyConflictWarningEventArgs warning)
    {
        output.ShouldContain("Conflit existant entre");
        output.ShouldContain("détection de conflits non résolus");
        output.ShouldContain("Références qui dépendent de");
        output.ShouldContain("Item Include d'un fichier projet");
        output.ShouldNotContain("There was a conflict between");
        output.ShouldNotContain("Found conflicts between different versions");
        warning.IsMessageMaterialized.ShouldBeFalse();
    }

    private static void WithFrenchUICulture(Action action)
    {
        CultureInfo originalUICulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");
            action();
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalUICulture;
        }
    }

    internal static (AssemblyConflictDependencyDetailsMessageEventArgs, AssemblyConflictWarningEventArgs) CreateEvents()
    {
        var victor = new AssemblyConflictReferenceDetails(
            "D, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null",
            @"C:\libs\v2\D.dll",
            isPrimary: true,
            isResolved: true,
            unresolvedPrimaryItemSpec: null,
            primarySourceItemSpecs: ["D"],
            dependees: []);

        var victim = new AssemblyConflictReferenceDetails(
            "D, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
            @"C:\libs\v1\D.dll",
            isPrimary: false,
            isResolved: true,
            unresolvedPrimaryItemSpec: null,
            primarySourceItemSpecs: [],
            dependees: [new AssemblyConflictDependee(@"C:\libs\B.dll", ["B"])]);

        var details = new AssemblyConflictDependencyDetailsMessageEventArgs(
            victor,
            victim,
            "ResolveAssemblyReference",
            MessageImportance.Low,
            DateTime.UtcNow);

        var warning = new AssemblyConflictWarningEventArgs(
            "D",
            AssemblyConflictLossReason.WasNotPrimary,
            victor,
            victim,
            "MSB3277",
            file: null,
            lineNumber: 0,
            columnNumber: 0,
            helpKeyword: "MSBuild.ResolveAssemblyReference.FoundConflicts",
            senderName: "ResolveAssemblyReference",
            eventTimestamp: DateTime.UtcNow);

        var context = new BuildEventContext(1, 2, 3, 4);
        details.BuildEventContext = context;
        warning.BuildEventContext = context;

        return (details, warning);
    }
}
