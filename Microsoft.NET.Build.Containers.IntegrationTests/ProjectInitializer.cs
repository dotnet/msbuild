// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.NET.Build.Containers.IntegrationTests;

public static class ProjectInitializer {
    private static string? CombinedTargetsLocation;

    private static string CombineFiles(string propsFile, string targetsFile)
    {
        var propsContent = File.ReadAllLines(propsFile);
        var targetsContent = File.ReadAllLines(targetsFile);
        var combinedContent = new List<string>();
        combinedContent.AddRange(propsContent[..^1]);
        combinedContent.AddRange(targetsContent[1..]);
        var tempTargetLocation = Path.Combine(Path.GetTempPath(), "Containers", "Microsoft.NET.Build.Containers.targets");
        string? directoryName = Path.GetDirectoryName(tempTargetLocation);
        Assert.NotNull(directoryName);
        Directory.CreateDirectory(directoryName);
        File.WriteAllLines(tempTargetLocation, combinedContent);
        return tempTargetLocation;
    }

    public static void LocateMSBuild()
    {
        var relativePath = Path.Combine("..", "packaging", "build", "Microsoft.NET.Build.Containers.targets");
        var targetsFile = CurrentFile.Relative(relativePath);
        var propsFile = Path.ChangeExtension(targetsFile, ".props");
        CombinedTargetsLocation = CombineFiles(propsFile, targetsFile);
    }

    public static void Cleanup()
    {
        if (CombinedTargetsLocation != null) File.Delete(CombinedTargetsLocation);
    }

    public static (Project, CapturingLogger) InitProject(Dictionary<string, string> bonusProps, [CallerMemberName]string projectName = "")
    {
        var props = new Dictionary<string, string>();
        // required parameters
        props["TargetFileName"] = "foo.dll";
        props["AssemblyName"] = "foo";
        props["_TargetFrameworkVersionWithoutV"] = "7.0";
        props["_NativeExecutableExtension"] = ".exe"; //TODO: windows/unix split here
        props["Version"] = "1.0.0"; // TODO: need to test non-compliant version strings here
        props["NetCoreSdkVersion"] = "7.0.100"; // TODO: float this to current SDK?
        // test setup parameters so that we can load the props/targets/tasks 
        props["ContainerCustomTasksAssembly"] = Path.GetFullPath(Path.Combine(".", "Microsoft.NET.Build.Containers.dll"));
        props["_IsTest"] = "true";

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
        return (collection.LoadProject(CombinedTargetsLocation, props, null), logs);
    }
}
