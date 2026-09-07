// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;

namespace MSBuild.Benchmarks;

/// <summary>
/// Shared project setup and input capture for the recording and filesystem-validation benchmarks.
/// </summary>
/// <remarks>
/// <para>
/// By default the benchmark evaluates a synthetic project that mirrors an SDK-style project without the SDK:
/// a <c>Directory.Build.props</c> found by upward search, wildcard imports, chained properties, file,
/// environment, and path property functions, and recursive item globs over a source tree with excluded
/// <c>obj</c> directories.
/// </para>
/// <para>
/// To measure real projects, set <c>MSBUILD_EVALUATION_INPUTS_BENCHMARK_PROJECTS</c> to a
/// <see cref="Path.PathSeparator"/>-separated list of restored project files. SDK-style projects also need
/// <c>MSBUILD_EXE_PATH</c> pointing at the bootstrap <c>MSBuild.dll</c>, <c>MSBuildSDKsPath</c> at its
/// <c>Sdks</c> directory (<c>artifacts\bin\bootstrap\core\sdk\&lt;version&gt;</c>), and
/// <c>DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR</c> at <c>artifacts\bin\bootstrap\core</c>. Pass those three to
/// the benchmark process through BenchmarkDotNet, for example
/// <c>Run-Benchmarks.ps1 -BenchmarkDotNetArguments '--envVars', 'MSBUILD_EXE_PATH:...', ...</c>; setting
/// them in the host environment redirects the build of the generated benchmark project instead.
/// A real project that is not cacheable fails setup with the reason, which makes the run double as an
/// admissibility check.
/// </para>
/// </remarks>
internal sealed class EvaluationInputBenchmarkFixture : IDisposable
{
    private const string RecordingVariable = "MSBUILDRECORDEVALUATIONINPUTS";
    private const string ProjectsVariable = "MSBUILD_EVALUATION_INPUTS_BENCHMARK_PROJECTS";
    internal const string SyntheticProject = "synthetic";
    private const int DirectoryCount = 32;
    private const int SourceFilesPerDirectory = 4;
    private const int ImportCount = 8;
    private const int ChainedPropertyCount = 100;

    private readonly TemporaryDirectory? _directory;

    internal string ProjectPath { get; }
    internal EvaluationInputs Inputs { get; private set; } = null!;

    internal static IEnumerable<string> ProjectPaths
    {
        get
        {
            yield return SyntheticProject;

            string? projects = Environment.GetEnvironmentVariable(ProjectsVariable);
            if (!string.IsNullOrEmpty(projects))
            {
                foreach (string project in projects.Split([Path.PathSeparator], StringSplitOptions.RemoveEmptyEntries))
                {
                    yield return Path.GetFullPath(project);
                }
            }
        }
    }

    internal EvaluationInputBenchmarkFixture(string projectPath)
    {
        if (projectPath == SyntheticProject)
        {
            _directory = new TemporaryDirectory(nameof(EvaluationInputBenchmarkFixture));
            CreateTree();
            ProjectPath = _directory.WriteFile("evaluation.proj", CreateProjectXml());
        }
        else
        {
            ProjectPath = projectPath;
        }
    }

    internal void RecordInputs()
    {
        SetRecording(enabled: true);
        Inputs = EvaluateProject().EvaluationInputs
            ?? throw new InvalidOperationException("Recording did not produce inputs.");
        if (!Inputs.IsCacheable)
        {
            throw new InvalidOperationException($"{ProjectPath} is not cacheable: {Inputs.NonCacheable} {Inputs.NonCacheableDetail}");
        }

        Console.WriteLine($"// {ProjectPath}: {Inputs.Files.Count} paths, {Inputs.EnvironmentReads.Count} environment reads, {Inputs.SdkResolutions.Length} SDK resolutions, {Inputs.RegistryReads.Length} registry reads");
    }

    /// <summary>
    /// A cold evaluation: the fresh collection keeps the project XML cache empty so every evaluation reads its files again.
    /// The collection costs the same with and without recording.
    /// </summary>
    internal ProjectInstance EvaluateProject(EvaluationContext? context = null)
    {
        using ProjectCollection collection = new();
        return ProjectInstance.FromFile(ProjectPath, new ProjectOptions { ProjectCollection = collection, EvaluationContext = context });
    }

    internal static void SetRecording(bool enabled)
    {
        Environment.SetEnvironmentVariable(RecordingVariable, enabled ? "1" : null);
        Traits.UpdateFromEnvironment();
    }

    public void Dispose() => _directory?.Dispose();

    private void CreateTree()
    {
        _directory!.WriteFile("Directory.Build.props", "<Project><PropertyGroup><FromDirectoryBuildProps>true</FromDirectoryBuildProps></PropertyGroup></Project>");
        _directory.WriteFile("version.txt", "1.2.3");

        for (int importIndex = 0; importIndex < ImportCount; importIndex++)
        {
            StringBuilder import = new("<Project><PropertyGroup>");
            for (int propertyIndex = 0; propertyIndex < 10; propertyIndex++)
            {
                import.Append($"<Import{importIndex}Property{propertyIndex}>{propertyIndex}</Import{importIndex}Property{propertyIndex}>");
            }

            import.Append("</PropertyGroup></Project>");
            _directory.WriteFile(Path.Combine("imports", $"import{importIndex:D2}.props"), import.ToString());
        }

        for (int directoryIndex = 0; directoryIndex < DirectoryCount; directoryIndex++)
        {
            string directory = Path.Combine("src", $"group{directoryIndex:D3}");
            for (int fileIndex = 0; fileIndex < SourceFilesPerDirectory; fileIndex++)
            {
                _directory.WriteFile(Path.Combine(directory, $"file{fileIndex}.cs"), "class C {}");
            }

            _directory.WriteFile(Path.Combine(directory, "readme.txt"), "readme");
            _directory.WriteFile(Path.Combine(directory, "obj", "generated.cs"), "class G {}");
        }
    }

    private static string CreateProjectXml()
    {
        StringBuilder xml = new();
        xml.AppendLine("<Project>");
        xml.AppendLine("  <PropertyGroup>");
        xml.AppendLine("    <DirectoryBuildPropsPath>$([MSBuild]::GetPathOfFileAbove('Directory.Build.props'))</DirectoryBuildPropsPath>");
        xml.AppendLine("  </PropertyGroup>");
        xml.AppendLine("  <Import Project=\"$(DirectoryBuildPropsPath)\" Condition=\"'$(DirectoryBuildPropsPath)' != ''\" />");
        xml.AppendLine("  <PropertyGroup>");
        xml.AppendLine("    <Configuration Condition=\"'$(Configuration)' == ''\">Debug</Configuration>");
        xml.AppendLine("    <OutputPath>bin/$(Configuration)/</OutputPath>");
        xml.AppendLine("    <Version>$([System.IO.File]::ReadAllText('$(MSBuildThisFileDirectory)version.txt').Trim())</Version>");
        xml.AppendLine("    <SourceRoot>$([MSBuild]::NormalizeDirectory('$(MSBuildThisFileDirectory)', 'src'))</SourceRoot>");
        xml.AppendLine("    <Stamp>$([System.Environment]::GetEnvironmentVariable('MSBUILD_BENCHMARK_STAMP'))</Stamp>");
        xml.AppendLine("    <HasGenerated>$([System.IO.Directory]::Exists('$(SourceRoot)generated'))</HasGenerated>");
        xml.AppendLine("    <Chain0>value</Chain0>");
        for (int i = 1; i < ChainedPropertyCount; i++)
        {
            xml.AppendLine($"    <Chain{i}>$(Chain{i - 1}).{i}</Chain{i}>");
        }

        xml.AppendLine("  </PropertyGroup>");
        xml.AppendLine("  <Import Project=\"imports/*.props\" />");
        xml.AppendLine("  <ItemGroup>");
        xml.AppendLine("    <Compile Include=\"src/**/*.cs\" Exclude=\"src/**/obj/**\" />");
        xml.AppendLine("    <None Include=\"**/*.txt\" />");
        xml.AppendLine("  </ItemGroup>");
        xml.AppendLine("</Project>");
        return xml.ToString();
    }
}
