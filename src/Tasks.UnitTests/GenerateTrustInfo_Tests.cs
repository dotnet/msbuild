// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETFRAMEWORK
using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
#endif
using Microsoft.Build.Tasks;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests;

public sealed class GenerateTrustInfo_Tests
{
    private readonly ITestOutputHelper _output;

    public GenerateTrustInfo_Tests(ITestOutputHelper output) => _output = output;

#if NETFRAMEWORK
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ManifestPathsAreResolvedPerTaskInstance(bool useAbsolutePaths)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string firstDirectory = env.CreateFolder().Path;
        string secondDirectory = env.CreateFolder().Path;
        firstDirectory.ShouldNotBe(Directory.GetCurrentDirectory());
        secondDirectory.ShouldNotBe(Directory.GetCurrentDirectory());

        GenerateTrustInfo first = CreateTask(firstDirectory, "requireAdministrator");
        GenerateTrustInfo second = CreateTask(secondDirectory, "highestAvailable");

        Parallel.Invoke(
            () => first.Execute().ShouldBeTrue(),
            () => second.Execute().ShouldBeTrue());

        VerifyOutput(first, firstDirectory, "requireAdministrator");
        VerifyOutput(second, secondDirectory, "highestAvailable");

        GenerateTrustInfo CreateTask(string directory, string executionLevel)
        {
            string manifestPath = Path.Combine(directory, "app.manifest");
            File.WriteAllText(manifestPath, $$"""
                <assembly xmlns="urn:schemas-microsoft-com:asm.v1">
                  <trustInfo xmlns="urn:schemas-microsoft-com:asm.v2">
                    <security>
                      <applicationRequestMinimum>
                        <PermissionSet class="System.Security.PermissionSet" version="1" Unrestricted="true" ID="Custom" />
                      </applicationRequestMinimum>
                      <requestedPrivileges xmlns="urn:schemas-microsoft-com:asm.v3">
                        <requestedExecutionLevel level="{{executionLevel}}" uiAccess="false" />
                      </requestedPrivileges>
                    </security>
                  </trustInfo>
                </assembly>
                """);

            Directory.CreateDirectory(Path.Combine(directory, "trust"));
            return new GenerateTrustInfo
            {
                BuildEngine = new MockEngine(_output),
                TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(directory),
                BaseManifest = new TaskItem(useAbsolutePaths ? manifestPath : "app.manifest"),
                TrustInfoFile = new TaskItem(useAbsolutePaths ? Path.Combine(directory, "trust", "app.trustinfo") : Path.Combine("trust", "app.trustinfo")),
                TargetZone = "Custom",
            };
        }

        void VerifyOutput(GenerateTrustInfo task, string directory, string executionLevel)
        {
            string outputPath = Path.Combine(directory, "trust", "app.trustinfo");
            File.Exists(outputPath).ShouldBeTrue();
            XDocument document = XDocument.Load(outputPath);
            XNamespace manifestNamespace = "urn:schemas-microsoft-com:asm.v2";
            XNamespace privilegesNamespace = "urn:schemas-microsoft-com:asm.v3";
            XElement security = document.Root.ShouldNotBeNull().Element(manifestNamespace + "security").ShouldNotBeNull();
            security.Element(privilegesNamespace + "requestedPrivileges").ShouldNotBeNull()
                .Element(privilegesNamespace + "requestedExecutionLevel").ShouldNotBeNull()
                .Attribute("level").ShouldNotBeNull().Value.ShouldBe(executionLevel);
            security.Element(manifestNamespace + "applicationRequestMinimum").ShouldNotBeNull()
                .Element(manifestNamespace + "PermissionSet").ShouldNotBeNull()
                .Attribute("Unrestricted").ShouldNotBeNull().Value.ShouldBe("true");
            task.BaseManifest.ItemSpec.ShouldBe(useAbsolutePaths ? Path.Combine(directory, "app.manifest") : "app.manifest");
            task.TrustInfoFile.ItemSpec.ShouldBe(useAbsolutePaths ? outputPath : Path.Combine("trust", "app.trustinfo"));
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("missing.manifest")]
    public void MissingBaseManifestIsIgnored(string? baseManifest)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string projectDirectory = env.CreateFolder().Path;
        string outputPath = Path.Combine(projectDirectory, "app.trustinfo");
        var engine = new MockEngine(_output);
        var task = new GenerateTrustInfo
        {
            BuildEngine = engine,
            TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDirectory),
            BaseManifest = baseManifest is null ? null : new TaskItem(baseManifest),
            TrustInfoFile = new TaskItem(outputPath),
            TargetZone = "Custom",
        };

        task.Execute().ShouldBeTrue();

        engine.Errors.ShouldBe(0);
        File.Exists(outputPath).ShouldBeTrue();
    }

    [Fact]
    public void InvalidManifestReportsOriginalPath()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string projectDirectory = env.CreateFolder().Path;
        File.WriteAllText(Path.Combine(projectDirectory, "app.manifest"), "<assembly>");
        var engine = new MockEngine(_output);
        var task = new GenerateTrustInfo
        {
            BuildEngine = engine,
            TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDirectory),
            BaseManifest = new TaskItem("app.manifest"),
            TrustInfoFile = new TaskItem("app.trustinfo"),
            TargetZone = "Custom",
        };

        task.Execute().ShouldBeFalse();

        BuildErrorEventArgs error = engine.ErrorEvents.ShouldHaveSingleItem();
        error.Code.ShouldBe("MSB3172");
        string message = error.Message.ShouldNotBeNull();
        message.ShouldContain("app.manifest");
        message.ShouldNotContain(projectDirectory);
        File.Exists(Path.Combine(projectDirectory, "app.trustinfo")).ShouldBeFalse();
    }
#else
    [Fact]
    public void UnsupportedRuntimeReportsFrameworkRequirement()
    {
        var engine = new MockEngine(_output);
        var task = new GenerateTrustInfo { BuildEngine = engine };

        task.Execute().ShouldBeFalse();

        engine.ErrorEvents.ShouldHaveSingleItem().Code.ShouldBe("MSB4803");
    }
#endif
}
