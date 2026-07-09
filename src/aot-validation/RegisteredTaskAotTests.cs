// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Build.AotValidation;

/// <summary>
/// Validates that MSBuild can actually <em>build</em> - run tasks, not just evaluate - under Native AOT,
/// using host-registered task classes.
///
/// The harness bakes <c>EnableReflectiveTaskExecution=false</c>, so the reflective task-loading path
/// (assembly probing, by-name type resolution) is trimmed away and an <em>un</em>registered task fails
/// observably. A host instead pre-registers its tasks with the host task registry: the common built-in
/// tasks through <see cref="BuiltInTasks.RegisterAll"/>, and its own tasks through
/// <see cref="Utilities.Task.RegisterTask{T}(string)"/>. A registered task is constructed and bound with
/// no assembly loading or by-name type resolution, so it runs under AOT. These tests drive a real
/// in-process build (<see cref="Project.Build(string, System.Collections.Generic.IEnumerable{ILogger})"/>)
/// of a hand-authored project and assert the tasks' real side effects.
/// </summary>
[TestClass]
public sealed class RegisteredTaskAotTests
{
    [TestMethod]
    public void RegisteredBuiltInAndCustomTasks_Build_UnderAot()
    {
        // Pre-register the common built-in tasks and a host custom task before the build. (Idempotent, so
        // calling RegisterAll across tests is safe.)
        BuiltInTasks.RegisterAll();
        Utilities.Task.RegisterTask<HarnessEchoTask>(nameof(HarnessEchoTask));

        using TempDirectory dir = new();
        string outDir = Path.Combine(dir.Path, "out");
        string projectPath = Path.Combine(dir.Path, "Build.proj");

        // A self-contained project (no SDK, no imports) whose single target runs a chain of registered
        // tasks: a built-in directory/file/copy sequence plus a host-registered custom task whose [Output]
        // is bound back to a property and echoed. Exercising the whole FindTask -> construct -> bind ->
        // Execute path for registered tasks under AOT.
        File.WriteAllText(
            projectPath,
            $"""
            <Project>
              <PropertyGroup>
                <OutDir>{outDir}</OutDir>
              </PropertyGroup>
              <ItemGroup>
                <Line Include="alpha" />
                <Line Include="beta" />
              </ItemGroup>
              <Target Name="Build">
                <MakeDir Directories="$(OutDir)" />
                <WriteLinesToFile File="$(OutDir)\lines.txt" Lines="@(Line)" Overwrite="true" />
                <HarnessEchoTask Input="hello">
                  <Output TaskParameter="Result" PropertyName="EchoResult" />
                </HarnessEchoTask>
                <Message Text="Echo result: $(EchoResult)" Importance="high" />
                <Copy SourceFiles="$(OutDir)\lines.txt" DestinationFiles="$(OutDir)\copy.txt" />
              </Target>
            </Project>
            """);

        CapturingLogger logger = new();
        using ProjectCollection collection = new();
        Project project = new(projectPath, globalProperties: null, toolsVersion: null, collection);

        bool success = InProcBuild.Run(project, "Build", logger);

        Assert.IsTrue(
            success,
            "Build failed. Errors:" + Environment.NewLine + string.Join(Environment.NewLine, logger.Errors));

        // The built-in MakeDir/WriteLinesToFile/Copy tasks ran (real file side effects).
        string copiedFile = Path.Combine(outDir, "copy.txt");
        Assert.IsTrue(File.Exists(Path.Combine(outDir, "lines.txt")), "WriteLinesToFile did not produce lines.txt.");
        Assert.IsTrue(File.Exists(copiedFile), "Copy did not produce copy.txt.");
        CollectionAssert.AreEqual(new[] { "alpha", "beta" }, File.ReadAllLines(copiedFile));

        // The host-registered custom task ran, and its [Output] was bound back to a property (reflective
        // parameter binding over the registered, trim-rooted task type).
        Assert.IsTrue(
            logger.Messages.Exists(m => m.Contains("Echo result: hello!", StringComparison.Ordinal)),
            "The custom registered task's output was not bound. Messages:" + Environment.NewLine
                + string.Join(Environment.NewLine, logger.Messages));
    }

    [TestMethod]
    public void IntrinsicCallTargetAndMSBuildTasks_Build_UnderAot()
    {
        // The intrinsic MSBuild and CallTarget tasks are engine-internal types resolved without reflecting
        // over a runtime-discovered assembly, so they must stay available with reflective task execution
        // disabled (the AOT path) even though they are not host-registered - virtually every real build uses
        // them. The inner leaf tasks (MakeDir/WriteLinesToFile) are host-registered as usual.
        BuiltInTasks.RegisterAll();

        using TempDirectory dir = new();
        string outDir = Path.Combine(dir.Path, "out");
        string childMarker = Path.Combine(outDir, "child.txt");
        string callTargetMarker = Path.Combine(outDir, "calltarget.txt");
        string childProjectPath = Path.Combine(dir.Path, "Child.proj");
        string projectPath = Path.Combine(dir.Path, "Build.proj");

        File.WriteAllText(
            childProjectPath,
            $"""
            <Project>
              <Target Name="ChildTarget">
                <MakeDir Directories="{outDir}" />
                <WriteLinesToFile File="{childMarker}" Lines="from-child" Overwrite="true" />
              </Target>
            </Project>
            """);

        File.WriteAllText(
            projectPath,
            $"""
            <Project>
              <PropertyGroup>
                <OutDir>{outDir}</OutDir>
              </PropertyGroup>
              <Target Name="Build">
                <!-- Intrinsic CallTarget: dispatch to another target in this project. -->
                <CallTarget Targets="ViaCallTarget" />
                <!-- Intrinsic MSBuild: build a separate project in-process. -->
                <MSBuild Projects="{childProjectPath}" Targets="ChildTarget" />
              </Target>
              <Target Name="ViaCallTarget">
                <MakeDir Directories="$(OutDir)" />
                <WriteLinesToFile File="{callTargetMarker}" Lines="via-call-target" Overwrite="true" />
              </Target>
            </Project>
            """);

        CapturingLogger logger = new();
        using ProjectCollection collection = new();
        Project project = new(projectPath, globalProperties: null, toolsVersion: null, collection);

        bool success = InProcBuild.Run(project, "Build", logger);

        Assert.IsTrue(
            success,
            "Build failed. Errors:" + Environment.NewLine + string.Join(Environment.NewLine, logger.Errors));

        // CallTarget (intrinsic, unregistered) dispatched to the ViaCallTarget target.
        Assert.IsTrue(File.Exists(callTargetMarker), "CallTarget did not run the ViaCallTarget target under AOT.");

        // The MSBuild task (intrinsic, unregistered) built the child project in-process.
        Assert.IsTrue(File.Exists(childMarker), "The MSBuild task did not build the child project under AOT.");
    }

    [TestMethod]
    public void UnregisteredTask_WithReflectionOff_FailsObservably()
    {
        // A task that is neither pre-registered nor host-registered. With the reflective task-loading path
        // trimmed away, the build fails with a reported error rather than crashing in reflection.
        using TempDirectory dir = new();
        string projectPath = Path.Combine(dir.Path, "Unregistered.proj");
        File.WriteAllText(
            projectPath,
            """
            <Project>
              <Target Name="Build">
                <ThisTaskIsNotRegistered />
              </Target>
            </Project>
            """);

        CapturingLogger logger = new();
        using ProjectCollection collection = new();
        Project project = new(projectPath, globalProperties: null, toolsVersion: null, collection);

        bool success = InProcBuild.Run(project, "Build", logger);

        Assert.IsFalse(success, "An unregistered task must not build under AOT.");
        Assert.IsTrue(logger.Errors.Count > 0, "An unregistered task must fail observably with a reported error.");
    }
}

/// <summary>
/// A host task registered through the public registration API. Its <see cref="Result"/> is an
/// <c>[Output]</c> bound back to a property, exercising reflective parameter binding over a registered,
/// trim-rooted task type under AOT.
/// </summary>
public sealed class HarnessEchoTask : Utilities.Task
{
    public string? Input { get; set; }

    [Output]
    public string? Result { get; set; }

    public override bool Execute()
    {
        Result = Input + "!";
        Log.LogMessage(MessageImportance.High, "Echo result: " + Result);
        return true;
    }
}

/// <summary>
/// A minimal, reflection-free <see cref="ILogger"/> that captures messages and errors for assertions.
/// </summary>
internal sealed class CapturingLogger : ILogger
{
    public List<string> Messages { get; } = [];

    public List<string> Errors { get; } = [];

    public LoggerVerbosity Verbosity { get; set; } = LoggerVerbosity.Normal;

    public string? Parameters { get; set; }

    public void Initialize(IEventSource eventSource)
    {
        eventSource.MessageRaised += (_, e) => Messages.Add(e.Message ?? string.Empty);
        eventSource.ErrorRaised += (_, e) => Errors.Add(e.Message ?? string.Empty);
    }

    public void Shutdown()
    {
    }
}
