// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Build.AotValidation;

/// <summary>
/// End-to-end validation that the MSBuild object model can evaluate <em>real</em> SDK-style projects -
/// the ones the .NET SDK CLI produces - under Native AOT, not just synthetic in-memory projects.
///
/// Each test shells out to the bootstrap <c>dotnet new</c> to lay down a stock template (a library and
/// an executable) into a temporary directory, then opens the generated <c>.csproj</c> with
/// <see cref="Project"/>. That exercises the full evaluation path a host like the SDK relies on:
/// resolving the <c>Microsoft.NET.Sdk</c> SDK (the reflection-free in-box resolver), importing its
/// implicit <c>Sdk.props</c>/<c>Sdk.targets</c> and the whole common-targets graph, and computing the
/// derived properties (output directory, target framework, output type) the SDK reads back. The
/// bootstrap toolset the harness points at via <c>MSBUILD_EXE_PATH</c> is the same SDK
/// <c>dotnet new</c> uses, so the SDK reference resolves to a real on-disk SDK.
/// </summary>
[TestClass]
public sealed class DotnetTemplateAotTests
{
    [TestMethod]
    public void DotnetNew_Console_EvaluatesAsExecutableProject()
    {
        using var temp = new TempDirectory();
        CreateFromTemplate("console", "ConsoleApp", temp.Path);

        using var collection = new ProjectCollection();
        Project project = EvaluateSingleProject(temp.Path, collection);

        // The console template declares an executable.
        Assert.AreEqual("Exe", project.GetPropertyValue("OutputType"));
        AssertEvaluatedSdkProject(project, "ConsoleApp");
    }

    [TestMethod]
    public void DotnetNew_Classlib_EvaluatesAsLibraryProject()
    {
        using var temp = new TempDirectory();
        CreateFromTemplate("classlib", "ClassLib", temp.Path);

        using var collection = new ProjectCollection();
        Project project = EvaluateSingleProject(temp.Path, collection);

        // The class-library template sets no OutputType, so it evaluates to the SDK default of "Library".
        Assert.AreEqual("Library", project.GetPropertyValue("OutputType"));
        AssertEvaluatedSdkProject(project, "ClassLib");
    }

    [TestMethod]
    public void DotnetNew_Console_BuildUnderAot_RunsRegisteredTasksThenFailsObservably()
        => AssertTemplateBuildEngagesTasksThenFailsObservably("console", "ConsoleApp");

    [TestMethod]
    public void DotnetNew_Classlib_BuildUnderAot_RunsRegisteredTasksThenFailsObservably()
        => AssertTemplateBuildEngagesTasksThenFailsObservably("classlib", "ClassLib");

    /// <summary>
    /// Builds a real SDK template in-process under the AOT configuration and asserts the build engages task
    /// execution and fails observably, rather than crashing in reflection.
    /// </summary>
    /// <remarks>
    /// A full SDK build also runs tasks from <c>Microsoft.NET.Build.Tasks</c> (the SDK's own task
    /// assembly - <c>ProcessFrameworkReferences</c>, <c>ResolvePackageAssets</c>, and so on), which are not
    /// part of <c>Microsoft.Build.Tasks.Core</c> and so cannot be registered from this harness. With the
    /// reflective task-loading path disabled (the trimmed/AOT host), evaluation and the registered built-in
    /// tasks still work, but reaching the first unregistered SDK task fails observably with a reported error.
    /// This pins the exact AOT boundary for a real SDK build: it degrades to a reported error, never a crash.
    /// </remarks>
    private static void AssertTemplateBuildEngagesTasksThenFailsObservably(string template, string name)
    {
        // Pre-register the common built-in tasks so the failure is isolated to the SDK's own task assembly,
        // not the core tasks a build also uses.
        BuiltInTasks.RegisterAll();

        using var temp = new TempDirectory();
        CreateFromTemplate(template, name, temp.Path);

        string projectPath = Directory
            .GetFiles(temp.Path, "*.csproj", SearchOption.AllDirectories)
            .Single();

        Dictionary<string, string> globalProperties = new()
        {
            ["MSBuildEnableWorkloadResolver"] = "false",
        };

        var logger = new CapturingLogger();
        using var collection = new ProjectCollection(globalProperties);
        Project project = new(projectPath, globalProperties, toolsVersion: null, collection);

        // The build must not throw (no AOT/reflection crash); it returns a result.
        bool success = InProcBuild.Run(project, "Build", logger);

        Assert.IsFalse(
            success,
            "A full SDK build cannot complete under AOT without the SDK's own task assembly; it should fail observably.");

        // The build evaluated the real SDK project, then executed targets until it reached the first task
        // from the SDK's own task assembly (for example AllowEmptyTelemetry, ProcessFrameworkReferences),
        // which is not registered. With reflective task loading disabled, that fails with the observable
        // "reflective task execution not supported" error - proving the build reached task execution and
        // degraded to a reported error rather than crashing in reflection.
        Assert.IsTrue(
            logger.Errors.Exists(e => e.Contains("trimmed or Native AOT host", StringComparison.Ordinal)),
            "Expected an observable reflective-task-execution-not-supported error. Errors:" + Environment.NewLine
                + string.Join(Environment.NewLine, logger.Errors));
    }

    /// <summary>
    /// Opens the single generated project under <paramref name="directory"/> with the MSBuild object
    /// model and returns the evaluated <see cref="Project"/>. A successful return is itself the core
    /// assertion: a real SDK project resolved its SDK and imported its entire targets graph under AOT.
    /// </summary>
    private static Project EvaluateSingleProject(string directory, ProjectCollection collection)
    {
        string projectPath = Directory
            .GetFiles(directory, "*.csproj", SearchOption.AllDirectories)
            .Single();

        // Disable workload resolution. Microsoft.NET.Sdk otherwise imports the workload-locator SDKs
        // (Microsoft.NET.SDK.WorkloadAutoImportPropsLocator / ...WorkloadManifestTargetsLocator), which are
        // resolved by the dynamically-loaded NuGet/workload plugin resolver - the AOT-hard path this harness
        // bakes off, so reaching it fails observably with MSB4282. The SDK gates that whole import behind the
        // MSBuildEnableWorkloadResolver flag (Microsoft.NET.Sdk.props), so an AOT host turns it off and the
        // project then evaluates entirely through the reflection-free in-box SDK path.
        Dictionary<string, string> globalProperties = new()
        {
            ["MSBuildEnableWorkloadResolver"] = "false",
        };

        return new Project(projectPath, globalProperties, toolsVersion: null, collection);
    }

    /// <summary>
    /// Validates the properties a host commonly reads back off an evaluated SDK project, including the
    /// "output directory" the SDK computes during evaluation.
    /// </summary>
    private static void AssertEvaluatedSdkProject(Project project, string expectedName)
    {
        // The project name is a reserved property derived from the file name.
        Assert.AreEqual(expectedName, project.GetPropertyValue("MSBuildProjectName"));

        // By default the assembly name follows the project name.
        Assert.AreEqual(expectedName, project.GetPropertyValue("AssemblyName"));

        // The SDK reference resolved and its whole import graph (Sdk.props/.targets plus the common
        // targets) loaded - a synthetic project would have a couple of imports; a real SDK project has many.
        Assert.IsTrue(
            project.Imports.Count > 10,
            $"Expected the full SDK import graph to load; only {project.Imports.Count} imports were evaluated.");

        // The template established a target framework.
        string targetFramework = project.GetPropertyValue("TargetFramework");
        Assert.IsTrue(
            targetFramework.StartsWith("net", StringComparison.Ordinal),
            $"Unexpected TargetFramework '{targetFramework}'.");

        // The default build configuration.
        Assert.AreEqual("Debug", project.GetPropertyValue("Configuration"));

        // The output directory: the SDK derives OutputPath (e.g. bin\Debug\<tfm>\) during evaluation.
        string outputPath = project.GetPropertyValue("OutputPath");
        Assert.IsFalse(string.IsNullOrEmpty(outputPath), "OutputPath should be set by the SDK after evaluation.");
        StringAssert.Contains(outputPath, "bin", $"OutputPath '{outputPath}' should live under 'bin'.");

        // The intermediate (obj) directory is likewise derived during evaluation.
        string intermediateOutputPath = project.GetPropertyValue("IntermediateOutputPath");
        Assert.IsFalse(
            string.IsNullOrEmpty(intermediateOutputPath),
            "IntermediateOutputPath should be set by the SDK after evaluation.");
        StringAssert.Contains(
            intermediateOutputPath,
            "obj",
            $"IntermediateOutputPath '{intermediateOutputPath}' should live under 'obj'.");
    }

    /// <summary>
    /// Runs <c>dotnet new &lt;template&gt; --name &lt;name&gt; --output &lt;dir&gt;</c> with the bootstrap
    /// SDK and asserts it succeeded.
    /// </summary>
    private static void CreateFromTemplate(string template, string name, string outputDirectory)
    {
        var startInfo = new ProcessStartInfo(FindBootstrapDotnet())
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = outputDirectory,
        };
        startInfo.ArgumentList.Add("new");
        startInfo.ArgumentList.Add(template);
        startInfo.ArgumentList.Add("--name");
        startInfo.ArgumentList.Add(name);
        startInfo.ArgumentList.Add("--output");
        startInfo.ArgumentList.Add(outputDirectory);

        // Keep the invocation quiet, offline-friendly, and deterministic.
        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        startInfo.Environment["DOTNET_NOLOGO"] = "1";
        startInfo.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        // Read both streams asynchronously to avoid a pipe-buffer deadlock.
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit(milliseconds: 120_000))
        {
            process.Kill(entireProcessTree: true);
            Assert.Fail($"`dotnet new {template}` did not complete within the timeout.");
        }

        // Ensure the redirected streams are fully flushed before reading their results.
        process.WaitForExit();

        Assert.AreEqual(
            0,
            process.ExitCode,
            $"`dotnet new {template}` failed (exit {process.ExitCode}).{Environment.NewLine}"
                + $"stdout:{Environment.NewLine}{standardOutput.GetAwaiter().GetResult()}{Environment.NewLine}"
                + $"stderr:{Environment.NewLine}{standardError.GetAwaiter().GetResult()}");
    }

    /// <summary>
    /// Locates the bootstrap <c>dotnet</c> host (the same complete SDK the harness evaluates against,
    /// produced by <c>build.cmd</c>) by walking up from the executable directory to the repository root.
    /// </summary>
    private static string FindBootstrapDotnet()
    {
        string dotnetFileName = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";

        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, "artifacts", "bin", "bootstrap", "core", dotnetFileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException(
            "Could not locate the bootstrap dotnet host under artifacts/bin/bootstrap/core. Run build.cmd first.");
    }
}
