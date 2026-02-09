// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.Build.UnitTests;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.Engine.UnitTests;

public class MSBuildTaskHostTests(ITestOutputHelper testOutput) : IDisposable
{
    private static string AssemblyLocation
        => field ??= Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppContext.BaseDirectory);

    private static string TestAssetsRootPath
        => field ??= Path.Combine(AssemblyLocation, "TestAssets");

    private readonly TestEnvironment _environment = TestEnvironment.Create(testOutput);

    public void Dispose()
        => _environment.Dispose();

    [WindowsNet35OnlyFact]
    public void CompileNet35WinFormsApp()
    {
        TransientTestFolder testFolder = _environment.CreateFolder(createFolder: true);

        CopyFilesRecursively(Path.Combine(TestAssetsRootPath, "Net35WinFormsApp"), testFolder.Path);
        string projectFilePath = Path.Combine(testFolder.Path, "TestNet35WinForms.csproj");

        string output = RunnerUtilities.ExecBootstrapedMSBuild(projectFilePath, out bool success, outputHelper: testOutput);
        success.ShouldBeTrue();

        output.ShouldContain("Build succeeded.");
    }

    private static void CopyFilesRecursively(string sourcePath, string targetPath)
    {
        // First Create all directories
        foreach (string dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(dirPath.Replace(sourcePath, targetPath));
        }

        // Then copy all the files & Replaces any files with the same name
        foreach (string newPath in Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories))
        {
            File.Copy(newPath, newPath.Replace(sourcePath, targetPath), overwrite: true);
        }
    }
}
