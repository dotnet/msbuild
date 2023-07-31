// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Containers.IntegrationTests;

public sealed class ProjectInitializer
{
    private readonly static string _combinedTargetsLocation;

    static ProjectInitializer()
    {
        var artifactPackagingDirectory = Path.Combine(TestContext.Current.TestExecutionDirectory, "Container", "packaging");
        var targetsFile = Path.Combine(artifactPackagingDirectory, "Microsoft.NET.Build.Containers.targets");
        var propsFile = Path.ChangeExtension(targetsFile, ".props");
        _combinedTargetsLocation = CombineFiles(propsFile, targetsFile);
    }

    private static string CombineFiles(string propsFile, string targetsFile)
    {
        var propsContent = File.ReadAllLines(propsFile);
        var targetsContent = File.ReadAllLines(targetsFile);
        var combinedContent = new List<string>();
        combinedContent.AddRange(propsContent[..^1]);
        combinedContent.AddRange(targetsContent[1..]);
        var tempTargetLocation = Path.Combine(TestSettings.TestArtifactsDirectory, "Containers", "Microsoft.NET.Build.Containers.targets");
        string? directoryName = Path.GetDirectoryName(tempTargetLocation);
        Assert.NotNull(directoryName);
        Directory.CreateDirectory(directoryName);
        File.WriteAllLines(tempTargetLocation, combinedContent);
        return tempTargetLocation;
    }

    public static (Project, CapturingLogger, IDisposable) InitProject(Dictionary<string, string> bonusProps, [CallerMemberName]string projectName = "")
    {
        var props = new Dictionary<string, string>();
        // required parameters
        props["TargetFileName"] = "foo.dll";
        props["AssemblyName"] = "foo";
        props["TargetFrameworkVersion"] = "v7.0";
        props["TargetFrameworkIdentifier"] = ".NETCoreApp";
        props["TargetFramework"] = "net7.0";
        props["_NativeExecutableExtension"] = ".exe"; //TODO: windows/unix split here
        props["Version"] = "1.0.0"; // TODO: need to test non-compliant version strings here
        props["NetCoreSdkVersion"] = "7.0.100"; // TODO: float this to current SDK?
        // test setup parameters so that we can load the props/targets/tasks
        props["ContainerCustomTasksAssembly"] = Path.GetFullPath(Path.Combine(".", "Microsoft.NET.Build.Containers.dll"));
        props["_IsTest"] = "true";
        // default here, can be overridden by tests if needed
        props["NETCoreSdkPortableRuntimeIdentifier"] = "linux-x64";

        var safeBinlogFileName = projectName.Replace(" ", "_").Replace(":", "_").Replace("/", "_").Replace("\\", "_").Replace("*", "_");
        var loggers = new List<ILogger>
        {
            new global::Microsoft.Build.Logging.BinaryLogger() {CollectProjectImports = global::Microsoft.Build.Logging.BinaryLogger.ProjectImportsCollectionMode.Embed, Verbosity = LoggerVerbosity.Diagnostic, Parameters = $"LogFile={safeBinlogFileName}.binlog" },
            new global::Microsoft.Build.Logging.ConsoleLogger(LoggerVerbosity.Detailed)
        };
        CapturingLogger logs = new CapturingLogger();
        loggers.Add(logs);

        var collection = new ProjectCollection(null, loggers, ToolsetDefinitionLocations.Default);
        foreach (var kvp in bonusProps)
        {
            props[kvp.Key] = kvp.Value;
        }
        // derived properties, since these might be set by bonusProps
        props["_TargetFrameworkVersionWithoutV"] = props["TargetFrameworkVersion"].TrimStart('v');
        return (collection.LoadProject(_combinedTargetsLocation, props, null), logs, collection);
    }
}
