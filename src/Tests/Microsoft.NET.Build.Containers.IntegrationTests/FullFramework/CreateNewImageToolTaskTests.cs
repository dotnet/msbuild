// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.NET.Build.Containers.Tasks;
using FakeItEasy;

namespace Microsoft.NET.Build.Containers.IntegrationTests.FullFramework;

public class CreateNewImageToolTaskTests
{
    private ITestOutputHelper _testOutput;

    public CreateNewImageToolTaskTests(ITestOutputHelper testOutput)
    {
        _testOutput = testOutput;
    }

    [Fact]
    public void GenerateCommandLineCommands_ThrowsWhenRequiredPropertiesNotSet()
    {
        CreateNewImage task = new();

        Exception e = Assert.Throws<InvalidOperationException>(() => task.GenerateCommandLineCommandsInt());
        Assert.Equal("CONTAINER4001: Required property 'PublishDirectory' was not set or empty.", e.Message);

        DirectoryInfo publishDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), DateTime.Now.ToString("yyyyMMddHHmmssfff")));

        task.PublishDirectory = publishDir.FullName;

        e = Assert.Throws<InvalidOperationException>(() => task.GenerateCommandLineCommandsInt());
        Assert.Equal("CONTAINER4001: Required property 'BaseRegistry' was not set or empty.", e.Message);

        task.BaseRegistry = "MyBaseRegistry";

        e = Assert.Throws<InvalidOperationException>(() => task.GenerateCommandLineCommandsInt());
        Assert.Equal("CONTAINER4001: Required property 'BaseImageName' was not set or empty.", e.Message);

        task.BaseImageName = "MyBaseImageName";

        e = Assert.Throws<InvalidOperationException>(() => task.GenerateCommandLineCommandsInt());
        Assert.Equal("CONTAINER4001: Required property 'Repository' was not set or empty.", e.Message);

        task.Repository = "MyImageName";

        e = Assert.Throws<InvalidOperationException>(() => task.GenerateCommandLineCommandsInt());
        Assert.Equal("CONTAINER4001: Required property 'WorkingDirectory' was not set or empty.", e.Message);

        task.WorkingDirectory = "MyWorkingDirectory";

        string args = task.GenerateCommandLineCommandsInt();
        string workDir = GetPathToContainerize();

        new DotnetCommand(_testOutput, args)
            .WithRawArguments()
            .WithWorkingDirectory(workDir)
            .Execute().Should().Fail()
            .And.NotHaveStdOutContaining("Description:"); //standard help output for parse error

    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ValidTag", true)]
    public void GenerateCommandLineCommands_BaseImageTag(string value, bool optionExpected = false)
    {
        CreateNewImage task = new();
        DirectoryInfo publishDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), DateTime.Now.ToString("yyyyMMddHHmmssfff")));
        task.PublishDirectory = publishDir.FullName;
        task.BaseRegistry = "MyBaseRegistry";
        task.BaseImageName = "MyBaseImageName";
        task.Repository = "MyImageName";
        task.WorkingDirectory = "MyWorkingDirectory";
        task.Entrypoint = new[] { new TaskItem("MyEntryPoint") };

        task.BaseImageTag = value;

        string args = task.GenerateCommandLineCommandsInt();

        if (optionExpected)
        {
            Assert.Contains($"--baseimagetag {value}", args);
        }
        else
        {
            Assert.DoesNotContain("--baseimagetag", args);
        }
    }


    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Valid", true)]
    public void GenerateCommandLineCommands_OutputRegistry(string value, bool optionExpected = false)
    {
        CreateNewImage task = new();
        DirectoryInfo publishDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), DateTime.Now.ToString("yyyyMMddHHmmssfff")));
        task.PublishDirectory = publishDir.FullName;
        task.BaseRegistry = "MyBaseRegistry";
        task.BaseImageName = "MyBaseImageName";
        task.Repository = "MyImageName";
        task.WorkingDirectory = "MyWorkingDirectory";
        task.Entrypoint = new[] { new TaskItem("MyEntryPoint") };

        task.OutputRegistry = value;

        string args = task.GenerateCommandLineCommandsInt();

        if (optionExpected)
        {
            Assert.Contains($"--outputregistry {value}", args);
        }
        else
        {
            Assert.DoesNotContain("--outputregistry", args);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Valid", true)]
    public void GenerateCommandLineCommands_ContainerRuntimeIdentifier(string value, bool optionExpected = false)
    {
        CreateNewImage task = new();
        DirectoryInfo publishDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), DateTime.Now.ToString("yyyyMMddHHmmssfff")));
        task.PublishDirectory = publishDir.FullName;
        task.BaseRegistry = "MyBaseRegistry";
        task.BaseImageName = "MyBaseImageName";
        task.Repository = "MyImageName";
        task.WorkingDirectory = "MyWorkingDirectory";
        task.Entrypoint = new[] { new TaskItem("MyEntryPoint") };

        task.ContainerRuntimeIdentifier = value;

        string args = task.GenerateCommandLineCommandsInt();
        if (optionExpected)
        {
            Assert.Contains($"--rid {value}", args);
        }
        else
        {
            Assert.DoesNotContain("--rid", args);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Valid", true)]
    public void GenerateCommandLineCommands_RuntimeIdentifierGraphPath(string value, bool optionExpected = false)
    {
        CreateNewImage task = new();
        DirectoryInfo publishDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), DateTime.Now.ToString("yyyyMMddHHmmssfff")));
        task.PublishDirectory = publishDir.FullName;
        task.BaseRegistry = "MyBaseRegistry";
        task.BaseImageName = "MyBaseImageName";
        task.Repository = "MyImageName";
        task.WorkingDirectory = "MyWorkingDirectory";
        task.Entrypoint = new[] { new TaskItem("MyEntryPoint") };

        task.RuntimeIdentifierGraphPath = value;

        string args = task.GenerateCommandLineCommandsInt();

        if (optionExpected)
        {
            Assert.Contains($"--ridgraphpath {value}", args);
        }
        else
        {
            Assert.DoesNotContain("--ridgraphpath", args);
        }
    }

    [Fact]
    public void GenerateCommandLineCommands_Labels()
    {
        CreateNewImage task = new();

        List<string?> warnings = new();
        IBuildEngine buildEngine = A.Fake<IBuildEngine>();
        A.CallTo(() => buildEngine.LogWarningEvent(A<BuildWarningEventArgs>.Ignored)).Invokes((BuildWarningEventArgs e) => warnings.Add(e.Message));

        task.BuildEngine = buildEngine;

        DirectoryInfo publishDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), DateTime.Now.ToString("yyyyMMddHHmmssfff")));
        task.PublishDirectory = publishDir.FullName;
        task.BaseRegistry = "MyBaseRegistry";
        task.BaseImageName = "MyBaseImageName";
        task.Repository = "MyImageName";
        task.WorkingDirectory = "MyWorkingDirectory";
        task.Entrypoint = new[] { new TaskItem("MyEntryPoint") };

        task.Labels = new[]
        {
            new TaskItem("NoValue"),
            new TaskItem(" "),
            new TaskItem("Valid1", new Dictionary<string, string>() {{ "Value", "Val1" }}),
            new TaskItem("Valid12", new Dictionary<string, string>() {{ "Value", "Val2" }}),
            new TaskItem("Valid12", new Dictionary<string, string>() {{ "Value", "" }}),
            new TaskItem("Valid3", new Dictionary<string, string>() {{ "Value", "has space" }}),
            new TaskItem("Valid4", new Dictionary<string, string>() {{ "Value", "has\"quotes\"" }})
        };

        string args = task.GenerateCommandLineCommandsInt();

        Assert.Contains("""
                                      --labels NoValue= Valid1=Val1 Valid12=Val2 Valid12= "Valid3=has space" "Valid4=has\"quotes\""
                                      """, args);
        Assert.Equal("Items 'Labels' contain empty item(s) which will be ignored.", Assert.Single(warnings));

        string workDir = GetPathToContainerize();

        new DotnetCommand(_testOutput, args)
            .WithRawArguments()
            .WithWorkingDirectory(workDir)
            .Execute().Should().Fail()
            .And.NotHaveStdOutContaining("Description:"); //standard help output for parse error
    }

    [Fact]
    public void GenerateCommandLineCommands_ContainerEnvironmentVariables()
    {
        CreateNewImage task = new();

        List<string?> warnings = new();
        IBuildEngine buildEngine = A.Fake<IBuildEngine>();
        A.CallTo(() => buildEngine.LogWarningEvent(A<BuildWarningEventArgs>.Ignored)).Invokes((BuildWarningEventArgs e) => warnings.Add(e.Message));

        task.BuildEngine = buildEngine;

        DirectoryInfo publishDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), DateTime.Now.ToString("yyyyMMddHHmmssfff")));
        task.PublishDirectory = publishDir.FullName;
        task.BaseRegistry = "MyBaseRegistry";
        task.BaseImageName = "MyBaseImageName";
        task.Repository = "MyImageName";
        task.WorkingDirectory = "MyWorkingDirectory";
        task.Entrypoint = new[] { new TaskItem("MyEntryPoint") };

        task.ContainerEnvironmentVariables = new[]
        {
            new TaskItem("NoValue"),
            new TaskItem(" "),
            new TaskItem("Valid1", new Dictionary<string, string>() {{ "Value", "Val1" }}),
            new TaskItem("Valid12", new Dictionary<string, string>() {{ "Value", "Val2" }}),
            new TaskItem("Valid12", new Dictionary<string, string>() {{ "Value", "" }}),
            new TaskItem("Valid3", new Dictionary<string, string>() {{ "Value", "has space" }}),
            new TaskItem("Valid4", new Dictionary<string, string>() {{ "Value", "has\"quotes\"" }})
        };

        string args = task.GenerateCommandLineCommandsInt();

        Assert.Contains("""
                                      --environmentvariables NoValue= Valid1=Val1 Valid12=Val2 Valid12= "Valid3=has space" "Valid4=has\"quotes\""
                                      """, args);
        Assert.Equal("Items 'ContainerEnvironmentVariables' contain empty item(s) which will be ignored.", Assert.Single(warnings));

        string workDir = GetPathToContainerize();

        new DotnetCommand(_testOutput, args)
            .WithRawArguments()
            .WithWorkingDirectory(workDir)
            .Execute().Should().Fail()
            .And.NotHaveStdOutContaining("Description:"); //standard help output for parse error
    }

    [InlineData(nameof(CreateNewImage.Entrypoint), "entrypoint")]
    [InlineData(nameof(CreateNewImage.EntrypointArgs), "entrypointargs", true)]
    [InlineData(nameof(CreateNewImage.DefaultArgs), "defaultargs", true)]
    [InlineData(nameof(CreateNewImage.AppCommand), "appcommand", true)]
    [InlineData(nameof(CreateNewImage.AppCommandArgs), "appcommandargs", true)]
    [Theory]
    public void GenerateCommandLineCommands_EntryPointAndCommand(string propertyName, string commandArgName, bool warningExpected = false)
    {
        CreateNewImage task = new();

        List<string?> warnings = new();
        IBuildEngine buildEngine = A.Fake<IBuildEngine>();
        A.CallTo(() => buildEngine.LogWarningEvent(A<BuildWarningEventArgs>.Ignored)).Invokes((BuildWarningEventArgs e) => warnings.Add(e.Message));

        task.BuildEngine = buildEngine;

        DirectoryInfo publishDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), DateTime.Now.ToString("yyyyMMddHHmmssfff")));
        task.PublishDirectory = publishDir.FullName;
        task.BaseRegistry = "MyBaseRegistry";
        task.BaseImageName = "MyBaseImageName";
        task.Repository = "MyImageName";
        task.WorkingDirectory = "MyWorkingDirectory";
        task.Entrypoint = new[] { new TaskItem("MyEntryPoint") };

        switch (propertyName)
        {
            case nameof(CreateNewImage.Entrypoint):
                task.Entrypoint = new[]
                {
                    new TaskItem("Valid1"),
                    new TaskItem("Valid2"),
                    new TaskItem("Quoted item")
                };
                break;
            case nameof(CreateNewImage.EntrypointArgs):
                task.EntrypointArgs = new[]
                {
                    new TaskItem(""),
                    new TaskItem(" "),
                    new TaskItem("Valid1"),
                    new TaskItem("Valid2"),
                    new TaskItem("Quoted item")
                };
                break;
            case nameof(CreateNewImage.DefaultArgs):
                task.DefaultArgs = new[]
                {
                    new TaskItem(""),
                    new TaskItem(" "),
                    new TaskItem("Valid1"),
                    new TaskItem("Valid2"),
                    new TaskItem("Quoted item")
                };
                break;
            case nameof(CreateNewImage.AppCommand):
                task.AppCommand = new[]
                {
                    new TaskItem(""),
                    new TaskItem(" "),
                    new TaskItem("Valid1"),
                    new TaskItem("Valid2"),
                    new TaskItem("Quoted item")
                };
                break;
            case nameof(CreateNewImage.AppCommandArgs):
                task.AppCommandArgs = new[]
                {
                    new TaskItem(""),
                    new TaskItem(" "),
                    new TaskItem("Valid1"),
                    new TaskItem("Valid2"),
                    new TaskItem("Quoted item")
                };
                break;
        }

        string args = task.GenerateCommandLineCommandsInt();

        Assert.Contains($"""
                                      --{commandArgName} Valid1 Valid2 "Quoted item"
                                      """, args);

        if (warningExpected)
        {
            Assert.Equal($"Items '{propertyName}' contain empty item(s) which will be ignored.", Assert.Single(warnings));
        }

        string workDir = GetPathToContainerize();

        new DotnetCommand(_testOutput, args)
            .WithRawArguments()
            .WithWorkingDirectory(workDir)
            .Execute().Should().Fail()
            .And.NotHaveStdOutContaining("Description:"); //standard help output for parse error
    }

    [InlineData("")]
    [InlineData("  ")]
    [Theory]
    public void GenerateCommandLineCommands_EntryPointCanHaveEmptyItems(string itemValue)
    {
        CreateNewImage task = new();
        IBuildEngine buildEngine = A.Fake<IBuildEngine>();

        task.BuildEngine = buildEngine;

        DirectoryInfo publishDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), DateTime.Now.ToString("yyyyMMddHHmmssfff")));
        task.PublishDirectory = publishDir.FullName;
        task.BaseRegistry = "MyBaseRegistry";
        task.BaseImageName = "MyBaseImageName";
        task.Repository = "MyImageName";
        task.WorkingDirectory = "MyWorkingDirectory";
        task.Entrypoint = new[] { new TaskItem(itemValue) };

        task.GenerateCommandLineCommandsInt();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Valid", true)]
    public void GenerateCommandLineCommands_AppCommandInstruction(string value, bool optionExpected = false)
    {
        CreateNewImage task = new();
        DirectoryInfo publishDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), DateTime.Now.ToString("yyyyMMddHHmmssfff")));
        task.PublishDirectory = publishDir.FullName;
        task.BaseRegistry = "MyBaseRegistry";
        task.BaseImageName = "MyBaseImageName";
        task.Repository = "MyImageName";
        task.WorkingDirectory = "MyWorkingDirectory";

        task.AppCommandInstruction = value;

        string args = task.GenerateCommandLineCommandsInt();

        if (optionExpected)
        {
            Assert.Contains($"--appcommandinstruction {value}", args);
        }
        else
        {
            Assert.DoesNotContain("--appcommandinstruction", args);
        }
    }

    [Fact]
    public void GenerateCommandLineCommands_ImageTags()
    {
        CreateNewImage task = new();

        List<string?> warnings = new();
        IBuildEngine buildEngine = A.Fake<IBuildEngine>();
        A.CallTo(() => buildEngine.LogWarningEvent(A<BuildWarningEventArgs>.Ignored)).Invokes((BuildWarningEventArgs e) => warnings.Add(e.Message));

        task.BuildEngine = buildEngine;

        DirectoryInfo publishDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), DateTime.Now.ToString("yyyyMMddHHmmssfff")));
        task.PublishDirectory = publishDir.FullName;
        task.BaseRegistry = "MyBaseRegistry";
        task.BaseImageName = "MyBaseImageName";
        task.Repository = "MyImageName";
        task.WorkingDirectory = "MyWorkingDirectory";
        task.Entrypoint = new[] { new TaskItem("MyEntryPoint") };

        task.ImageTags = new[] { "", " ", "Valid1", "To be quoted" };

        string args = task.GenerateCommandLineCommandsInt();

        Assert.Contains("""
                                      --imagetags Valid1 "To be quoted"
                                      """, actualString: args);
        Assert.Equal("Property 'ImageTags' is empty or contains whitespace and will be ignored.", Assert.Single(warnings));

        string workDir = GetPathToContainerize();

        new DotnetCommand(_testOutput, args)
            .WithRawArguments()
            .WithWorkingDirectory(workDir)
            .Execute().Should().Fail()
            .And.NotHaveStdOutContaining("Description:"); //standard help output for parse error
    }

    [Fact]
    public void GenerateCommandLineCommands_ExposedPorts()
    {
        CreateNewImage task = new();

        List<string?> warnings = new();
        IBuildEngine buildEngine = A.Fake<IBuildEngine>();
        A.CallTo(() => buildEngine.LogWarningEvent(A<BuildWarningEventArgs>.Ignored)).Invokes((BuildWarningEventArgs e) => warnings.Add(e.Message));

        task.BuildEngine = buildEngine;

        DirectoryInfo publishDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), DateTime.Now.ToString("yyyyMMddHHmmssfff")));
        task.PublishDirectory = publishDir.FullName;
        task.BaseRegistry = "MyBaseRegistry";
        task.BaseImageName = "MyBaseImageName";
        task.Repository = "MyImageName";
        task.WorkingDirectory = "MyWorkingDirectory";
        task.Entrypoint = new[] { new TaskItem("MyEntryPoint") };

        task.ExposedPorts = new[]
        {
            new TaskItem("1500"),
            new TaskItem(" "),
            new TaskItem("1501", new Dictionary<string, string>() {{ "Type", "udp" }}),
            new TaskItem("1501", new Dictionary<string, string>() {{ "Type", "tcp" }}),
            new TaskItem("1502", new Dictionary<string, string>() {{ "Type", "tcp" }}),
            new TaskItem("1503", new Dictionary<string, string>() {{ "Type", "" }})
        };

        string args = task.GenerateCommandLineCommandsInt();

        Assert.Contains("""
                                      --ports 1500 1501/udp 1501/tcp 1502/tcp 1503
                                      """, args);
        Assert.Equal("Items 'ExposedPorts' contain empty item(s) which will be ignored.", Assert.Single(warnings));

        string workDir = GetPathToContainerize();

        new DotnetCommand(_testOutput, args)
            .WithRawArguments()
            .WithWorkingDirectory(workDir)
            .Execute().Should().Fail()
            .And.NotHaveStdOutContaining("Description:"); //standard help output for parse error
    }

    [Fact]
    public void Logging_CanEnableTraceLogging()
    {
        CreateNewImage task = new();
        DirectoryInfo publishDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), DateTime.Now.ToString("yyyyMMddHHmmssfff")));

        task.PublishDirectory = publishDir.FullName;
        task.BaseRegistry = "MyBaseRegistry";
        task.BaseImageName = "MyBaseImageName";
        task.Repository = "MyImageName";
        task.WorkingDirectory = "MyWorkingDirectory";
        task.Entrypoint = new[] { new TaskItem("") };
        task.Entrypoint = new[] { new TaskItem("MyEntryPoint") };

        string args = task.GenerateCommandLineCommandsInt();
        string workDir = GetPathToContainerize();

        new DotnetCommand(_testOutput, args)
            .WithRawArguments()
            .WithWorkingDirectory(workDir)
            .WithEnvironmentVariable("CONTAINERIZE_TRACE_LOGGING_ENABLED", "1")
            .Execute().Should().Fail()
            .And.NotHaveStdOutContaining("Description:") //standard help output for parse error
            .And.HaveStdOutContaining("Trace logging: enabled.");
    }

    [Fact]
    public void Logging_TraceLoggingIsDisabledByDefault()
    {
        CreateNewImage task = new();
        DirectoryInfo publishDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), DateTime.Now.ToString("yyyyMMddHHmmssfff")));

        task.PublishDirectory = publishDir.FullName;
        task.BaseRegistry = "MyBaseRegistry";
        task.BaseImageName = "MyBaseImageName";
        task.Repository = "MyImageName";
        task.WorkingDirectory = "MyWorkingDirectory";
        task.Entrypoint = new[] { new TaskItem("") };
        task.Entrypoint = new[] { new TaskItem("MyEntryPoint") };

        string args = task.GenerateCommandLineCommandsInt();
        string workDir = GetPathToContainerize();

        new DotnetCommand(_testOutput, args)
            .WithRawArguments()
            .WithWorkingDirectory(workDir)
            .Execute().Should().Fail()
            .And.NotHaveStdOutContaining("Description:") //standard help output for parse error
            .And.NotHaveStdOutContaining("Trace logging: enabled.");
    }

    private static string GetPathToContainerize()
    {
        return Path.Combine(TestContext.Current.TestExecutionDirectory, "Container", "containerize");
    }
}
