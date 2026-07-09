// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Construction;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Build.AotValidation;

// The MSBuild evaluation entry points (new Project(...), new ProjectInstance(...),
// ProjectCollection.LoadProject, Project.CreateProjectInstance) are now trim/AOT-safe: their
// SDK-resolution [RequiresUnreferencedCode] was removed once in-box SDK resolution was made
// reflection-free and plugin-resolver loading was gated behind a feature switch that fails
// observably under trimming (see documentation/aot/sdk-resolution.md). No IL2026 suppression
// is needed here anymore.

/// <summary>
/// Vets the MSBuild object-model scenarios the .NET SDK CLI relies on in-process
/// (see documentation/aot/sdk-msbuild-object-model-audit.md) under Native AOT.
///
/// Scope is the evaluation + construction tiers - the surface that should be trim/AOT-safe and
/// that the SDK uses for project/solution inspection (dotnet new/run/test discovery, publish/pack
/// release detection, reference and solution editing). The execution engine (BuildManager) is
/// intentionally out of scope: per the audit it is the AOT-hard path that a host falls back to a
/// forwarded/JIT MSBuild for.
/// </summary>
[TestClass]
public sealed class ObjectModelAotTests
{
    // ---- Construction tier: dotnet reference / dotnet solution add edit project XML ----

    [TestMethod]
    public void Construction_CreateAndReadProjectRootElement()
    {
        ProjectRootElement root = ProjectRootElement.Create();
        root.AddProperty("Configuration", "Debug");
        ProjectItemElement item = root.AddItem("ProjectReference", @"..\Lib\Lib.csproj");

        Assert.AreEqual("ProjectReference", item.ItemType);
        Assert.AreEqual(@"..\Lib\Lib.csproj", item.Include);
        Assert.IsTrue(root.Properties.Any(p => p.Name == "Configuration" && p.Value == "Debug"));
    }

    // ---- Evaluation tier: dotnet new (capabilities), dotnet run, reference TFM checks ----

    [TestMethod]
    public void Evaluation_InMemoryProject_PropertiesAndItems()
    {
        ProjectRootElement root = ProjectRootElement.Create();
        root.AddProperty("TargetFramework", "net10.0");
        root.AddProperty("OutputType", "Exe");
        root.AddItem("Compile", "A.cs");
        root.AddItem("Compile", "B.cs");

        using var collection = new ProjectCollection();
        var project = new Project(root, globalProperties: null, toolsVersion: null, collection);

        Assert.AreEqual("net10.0", project.GetPropertyValue("TargetFramework"));
        Assert.AreEqual("Exe", project.GetPropertyValue("OutputType"));
        Assert.AreEqual(2, project.GetItems("Compile").Count);
    }

    [TestMethod]
    public void Evaluation_ConditionsEvaluate()
    {
        ProjectRootElement root = ProjectRootElement.Create();
        root.AddProperty("Configuration", "Release");
        ProjectPropertyGroupElement pg = root.AddPropertyGroup();
        pg.Condition = "'$(Configuration)' == 'Release'";
        pg.AddProperty("Optimize", "true");

        using var collection = new ProjectCollection();
        var project = new Project(root, null, null, collection);

        Assert.AreEqual("true", project.GetPropertyValue("Optimize"));
    }

    [TestMethod]
    public void Evaluation_IntrinsicPropertyFunction()
    {
        // Intrinsic MSBuild property functions are not arbitrary-type reflection, so they are the
        // AOT-friendly subset of the property-function surface (see property-functions-reachability.md).
        ProjectRootElement root = ProjectRootElement.Create();
        root.AddProperty("Sum", "$([MSBuild]::Add(2, 3))");

        using var collection = new ProjectCollection();
        var project = new Project(root, null, null, collection);

        Assert.AreEqual("5", project.GetPropertyValue("Sum"));
    }

    // ---- ProjectInstance tier: release locator, dotnet run, test discovery, solution add ----

    [TestMethod]
    public void ProjectInstance_InMemory_PropertiesAndItems()
    {
        ProjectRootElement root = ProjectRootElement.Create();
        root.AddProperty("IsTestProject", "true");
        root.AddItem("ProjectReference", @"..\Lib\Lib.csproj");

        var instance = new ProjectInstance(root);

        Assert.AreEqual("true", instance.GetPropertyValue("IsTestProject"));
        Assert.AreEqual(1, instance.GetItems("ProjectReference").Count());
    }

    [TestMethod]
    public void ProjectInstance_FromFile_MirrorsTestDiscoveryAndReleaseLocator()
    {
        RunInTempDir(dir =>
        {
            string proj = Path.Combine(dir, "App.csproj");
            File.WriteAllText(proj,
                """
                <Project>
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <PublishRelease>true</PublishRelease>
                    <IsTestProject>true</IsTestProject>
                  </PropertyGroup>
                  <ItemGroup>
                    <Compile Include="Program.cs" />
                  </ItemGroup>
                </Project>
                """);

            using var collection = new ProjectCollection();
            var instance = ProjectInstance.FromFile(proj, new ProjectOptions { ProjectCollection = collection });

            Assert.AreEqual("net10.0", instance.GetPropertyValue("TargetFramework"));
            Assert.AreEqual("true", instance.GetPropertyValue("PublishRelease"));
            Assert.AreEqual("true", instance.GetPropertyValue("IsTestProject"));
            Assert.AreEqual(1, instance.GetItems("Compile").Count());
        });
    }

    [TestMethod]
    public void Evaluation_LoadProjectFromDisk_MirrorsRunCommand()
    {
        RunInTempDir(dir =>
        {
            string proj = Path.Combine(dir, "Lib.csproj");
            File.WriteAllText(proj,
                """
                <Project>
                  <PropertyGroup>
                    <RunCommand>dotnet</RunCommand>
                    <OutputType>Library</OutputType>
                  </PropertyGroup>
                </Project>
                """);

            using var collection = new ProjectCollection();
            Project project = collection.LoadProject(proj);
            ProjectInstance instance = project.CreateProjectInstance();

            Assert.AreEqual("dotnet", instance.GetPropertyValue("RunCommand"));
            Assert.AreEqual("Library", instance.GetPropertyValue("OutputType"));
        });
    }

    // ---- SDK resolution tier: <Project Sdk="..."> in-box (reflection-free) resolution ----

    [TestMethod]
    public void Evaluation_InBoxSdkResolvesReflectionFree()
    {
        // The in-box SDK resolver is a reflection-free directory probe
        // (BuildEnvironmentHelper.MSBuildSDKsPath\<name>\Sdk), so a <Project Sdk="..."> whose SDK lives
        // in the SDK folder resolves and its implicit Sdk.props/Sdk.targets imports load end to end under
        // Native AOT - no resolver assembly is loaded. Plugin resolvers (NuGet/workload/custom) are the
        // AOT-hard path and fail observably (MSB4282) instead; that branch is covered by the engine unit tests.
        RunInTempDir(dir =>
        {
            const string sdkName = "Harness.InBox.Sdk";
            string sdkRoot = Path.Combine(dir, "Sdks");
            string sdkDir = Path.Combine(sdkRoot, sdkName, "Sdk");
            Directory.CreateDirectory(sdkDir);
            File.WriteAllText(
                Path.Combine(sdkDir, "Sdk.props"),
                "<Project><PropertyGroup><FromSdkProps>props-value</FromSdkProps></PropertyGroup></Project>");
            File.WriteAllText(
                Path.Combine(sdkDir, "Sdk.targets"),
                "<Project><PropertyGroup><FromSdkTargets>targets-value</FromSdkTargets></PropertyGroup></Project>");

            string proj = Path.Combine(dir, "App.csproj");
            File.WriteAllText(proj, $"<Project Sdk=\"{sdkName}\"><PropertyGroup><Configuration>Debug</Configuration></PropertyGroup></Project>");

            // The in-box resolver probes MSBuildSDKsPath, which honors this environment override on every read.
            string? previousSdksPath = Environment.GetEnvironmentVariable("MSBuildSDKsPath");
            Environment.SetEnvironmentVariable("MSBuildSDKsPath", sdkRoot);
            try
            {
                using var collection = new ProjectCollection();
                var project = new Project(proj, globalProperties: null, toolsVersion: null, collection);

                // The implicit Sdk.props (top) and Sdk.targets (bottom) both resolved and were imported.
                Assert.AreEqual("props-value", project.GetPropertyValue("FromSdkProps"));
                Assert.AreEqual("targets-value", project.GetPropertyValue("FromSdkTargets"));
                Assert.AreEqual(2, project.Imports.Count);
            }
            finally
            {
                Environment.SetEnvironmentVariable("MSBuildSDKsPath", previousSdksPath);
            }
        });
    }

    // ---- SolutionFile tier: dotnet test discovery / publish-pack release locator ----

    [TestMethod]
    public void Construction_ParseSolutionFile()
    {
        RunInTempDir(dir =>
        {
            string sln = Path.Combine(dir, "App.sln");
            File.WriteAllText(sln, MinimalSolution);

            SolutionFile solution = SolutionFile.Parse(sln);

            Assert.AreEqual(1, solution.ProjectsInOrder.Count);
            Assert.AreEqual("App", solution.ProjectsInOrder[0].ProjectName);
            Assert.AreEqual("App.csproj", solution.ProjectsInOrder[0].RelativePath);
        });
    }

    private static void RunInTempDir(Action<string> body)
    {
        string dir = Path.Combine(Path.GetTempPath(), "msb-aot-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            body(dir);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static readonly string MinimalSolution = string.Join("\r\n",
    [
        "Microsoft Visual Studio Solution File, Format Version 12.00",
        "# Visual Studio Version 17",
        "Project(\"{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}\") = \"App\", \"App.csproj\", \"{11111111-1111-1111-1111-111111111111}\"",
        "EndProject",
        "Global",
        "\tGlobalSection(SolutionConfigurationPlatforms) = preSolution",
        "\t\tDebug|Any CPU = Debug|Any CPU",
        "\t\tRelease|Any CPU = Release|Any CPU",
        "\tEndGlobalSection",
        "\tGlobalSection(ProjectConfigurationPlatforms) = postSolution",
        "\t\t{11111111-1111-1111-1111-111111111111}.Debug|Any CPU.ActiveCfg = Debug|Any CPU",
        "\t\t{11111111-1111-1111-1111-111111111111}.Debug|Any CPU.Build.0 = Debug|Any CPU",
        "\t\t{11111111-1111-1111-1111-111111111111}.Release|Any CPU.ActiveCfg = Release|Any CPU",
        "\t\t{11111111-1111-1111-1111-111111111111}.Release|Any CPU.Build.0 = Release|Any CPU",
        "\tEndGlobalSection",
        "EndGlobal",
    ]);
}
