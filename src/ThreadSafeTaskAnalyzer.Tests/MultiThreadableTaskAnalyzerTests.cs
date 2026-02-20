// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Xunit;
using static Microsoft.Build.TaskAuthoring.Analyzer.Tests.TestHelpers;

namespace Microsoft.Build.TaskAuthoring.Analyzer.Tests;

/// <summary>
/// Tests for <see cref="MultiThreadableTaskAnalyzer"/> covering all 4 diagnostic rules.
/// Uses manual compilation to avoid fragile message argument matching.
/// </summary>
public class MultiThreadableTaskAnalyzerTests
{
    // ═══════════════════════════════════════════════════════════════════════
    // MSBuildTask0001: Critical errors
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ConsoleWriteLine_InAnyTask_ProducesError()
    {
        var diags = await GetDiagnosticsAsync("""
            using System;
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    Console.WriteLine("hello");
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.CriticalError);
        diags.Length.ShouldBe(1);
    }

    [Fact]
    public async Task ConsoleWrite_MultipleOverloads_AllDetected()
    {
        var diags = await GetDiagnosticsAsync("""
            using System;
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    Console.Write("a");
                    Console.Write(42);
                    Console.Write(true);
                    Console.Write('c');
                    return true;
                }
            }
            """);

        diags.Where(d => d.Id == DiagnosticIds.CriticalError).Count().ShouldBe(4);
    }

    [Fact]
    public async Task ConsoleOut_PropertyAccess_ProducesError()
    {
        var diags = await GetDiagnosticsAsync("""
            using System;
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    var writer = Console.Out;
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.CriticalError);
    }

    [Fact]
    public async Task EnvironmentExit_ProducesError()
    {
        var diags = await GetDiagnosticsAsync("""
            using System;
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    Environment.Exit(1);
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.CriticalError);
        diags.Length.ShouldBe(1);
    }

    [Fact]
    public async Task EnvironmentFailFast_ProducesError()
    {
        var diags = await GetDiagnosticsAsync("""
            using System;
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    Environment.FailFast("crash");
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.CriticalError);
    }

    [Fact]
    public async Task ThreadPoolSetMinMaxThreads_ProducesError()
    {
        var diags = await GetDiagnosticsAsync("""
            using System.Threading;
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    ThreadPool.SetMinThreads(4, 4);
                    ThreadPool.SetMaxThreads(16, 16);
                    return true;
                }
            }
            """);

        diags.Where(d => d.Id == DiagnosticIds.CriticalError).Count().ShouldBe(2);
    }

    [Fact]
    public async Task CultureInfoDefaults_ProducesError()
    {
        var diags = await GetDiagnosticsAsync("""
            using System.Globalization;
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
                    CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
                    return true;
                }
            }
            """);

        diags.Where(d => d.Id == DiagnosticIds.CriticalError).Count().ShouldBe(2);
    }

    [Fact]
    public async Task ConsoleReadLine_ProducesError()
    {
        var diags = await GetDiagnosticsAsync("""
            using System;
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    var input = Console.ReadLine();
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.CriticalError);
    }

    [Fact]
    public async Task ProcessKill_InAnyTask_ProducesError()
    {
        var diags = await GetDiagnosticsAsync("""
            using System.Diagnostics;
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    var p = Process.GetCurrentProcess();
                    p.Kill();
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.CriticalError);
    }

    [Fact]
    public async Task MSBuildTask0001_FiresForRegularTask()
    {
        var diags = await GetDiagnosticsAsync("""
            using System;
            public class RegularTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    Console.WriteLine("hello");
                    Environment.Exit(1);
                    return true;
                }
            }
            """);

        diags.Where(d => d.Id == DiagnosticIds.CriticalError).Count().ShouldBe(2);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // MSBuildTask0002: TaskEnvironment required (only for IMultiThreadableTask)
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task EnvironmentGetEnvVar_InMultiThreadableTask_ProducesWarning()
    {
        var diags = await GetDiagnosticsAsync("""
            using System;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public override bool Execute()
                {
                    var val = Environment.GetEnvironmentVariable("PATH");
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.TaskEnvironmentRequired);
        diags.Length.ShouldBe(1);
    }

    [Fact]
    public async Task EnvironmentSetEnvVar_InMultiThreadableTask_ProducesWarning()
    {
        var diags = await GetDiagnosticsAsync("""
            using System;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public override bool Execute()
                {
                    Environment.SetEnvironmentVariable("KEY", "VALUE");
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.TaskEnvironmentRequired);
    }

    [Fact]
    public async Task EnvironmentCurrentDirectory_InMultiThreadableTask_ProducesWarning()
    {
        var diags = await GetDiagnosticsAsync("""
            using System;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public override bool Execute()
                {
                    var dir = Environment.CurrentDirectory;
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.TaskEnvironmentRequired);
    }

    [Fact]
    public async Task PathGetFullPath_InMultiThreadableTask_ProducesWarning()
    {
        var diags = await GetDiagnosticsAsync("""
            using System.IO;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public override bool Execute()
                {
                    var p = Path.GetFullPath("relative");
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.TaskEnvironmentRequired);
    }

    [Fact]
    public async Task ProcessStart_InMultiThreadableTask_ProducesWarning()
    {
        var diags = await GetDiagnosticsAsync("""
            using System.Diagnostics;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public override bool Execute()
                {
                    Process.Start("cmd.exe");
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.TaskEnvironmentRequired);
    }

    [Fact]
    public async Task ProcessStartInfo_Constructor_ProducesWarning()
    {
        var diags = await GetDiagnosticsAsync("""
            using System.Diagnostics;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public override bool Execute()
                {
                    var psi = new ProcessStartInfo("cmd.exe");
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.TaskEnvironmentRequired);
    }

    [Fact]
    public async Task EnvironmentGetEnvVar_InRegularTask_ProducesDiagnostic()
    {
        var diags = await GetDiagnosticsAsync("""
            using System;
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    var val = Environment.GetEnvironmentVariable("PATH");
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.TaskEnvironmentRequired);
    }

    [Fact]
    public async Task ExpandEnvironmentVariables_InMultiThreadableTask_ProducesWarning()
    {
        var diags = await GetDiagnosticsAsync("""
            using System;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public override bool Execute()
                {
                    var result = Environment.ExpandEnvironmentVariables("%PATH%");
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.TaskEnvironmentRequired);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // MSBuildTask0003: File path requires absolute (only for IMultiThreadableTask)
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task FileExists_WithStringArg_ProducesWarning()
    {
        var diags = await GetDiagnosticsAsync("""
            using System.IO;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public override bool Execute()
                {
                    File.Exists("foo.txt");
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.FilePathRequiresAbsolute);
    }

    [Fact]
    public async Task FileReadAllText_WithStringArg_ProducesWarning()
    {
        var diags = await GetDiagnosticsAsync("""
            using System.IO;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public override bool Execute()
                {
                    var content = File.ReadAllText("file.txt");
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.FilePathRequiresAbsolute);
    }

    [Fact]
    public async Task DirectoryExists_WithStringArg_ProducesWarning()
    {
        var diags = await GetDiagnosticsAsync("""
            using System.IO;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public override bool Execute()
                {
                    Directory.Exists("mydir");
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.FilePathRequiresAbsolute);
    }

    [Fact]
    public async Task NewFileInfo_WithStringArg_ProducesWarning()
    {
        var diags = await GetDiagnosticsAsync("""
            using System.IO;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public override bool Execute()
                {
                    var fi = new FileInfo("file.txt");
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.FilePathRequiresAbsolute);
    }

    [Fact]
    public async Task NewStreamReader_WithStringArg_ProducesWarning()
    {
        var diags = await GetDiagnosticsAsync("""
            using System.IO;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public override bool Execute()
                {
                    using var sr = new StreamReader("file.txt");
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.FilePathRequiresAbsolute);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // MSBuildTask0003 Safe Patterns: No diagnostic expected
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task FileExists_WithGetAbsolutePath_NoDiagnostic()
    {
        var diags = await GetDiagnosticsAsync("""
            using System.IO;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public override bool Execute()
                {
                    File.Exists(TaskEnvironment.GetAbsolutePath("foo.txt"));
                    return true;
                }
            }
            """);

        diags.ShouldNotContain(d => d.Id == DiagnosticIds.FilePathRequiresAbsolute);
    }

    [Fact]
    public async Task FileExists_WithAbsolutePathVariable_NoDiagnostic()
    {
        var diags = await GetDiagnosticsAsync("""
            using System.IO;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public override bool Execute()
                {
                    AbsolutePath p = TaskEnvironment.GetAbsolutePath("foo.txt");
                    File.Exists(p);
                    return true;
                }
            }
            """);

        diags.ShouldNotContain(d => d.Id == DiagnosticIds.FilePathRequiresAbsolute);
    }

    [Fact]
    public async Task FileDelete_WithNullableAbsolutePathVariable_NoDiagnostic()
    {
        var diags = await GetDiagnosticsAsync("""
            using System.IO;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public override bool Execute()
                {
                    AbsolutePath? filePath = TaskEnvironment.GetAbsolutePath("foo.txt");
                    if (filePath.HasValue)
                    {
                        File.Delete(filePath.Value);
                    }
                    return true;
                }
            }
            """);

        diags.ShouldNotContain(d => d.Id == DiagnosticIds.FilePathRequiresAbsolute);
    }

    [Fact]
    public async Task FileExists_WithGetMetadataFullPath_NoDiagnostic()
    {
        var diags = await GetDiagnosticsAsync("""
            using System.IO;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public ITaskItem[] Items { get; set; }
                public override bool Execute()
                {
                    foreach (var item in Items)
                    {
                        File.Exists(item.GetMetadata("FullPath"));
                    }
                    return true;
                }
            }
            """);

        diags.ShouldNotContain(d => d.Id == DiagnosticIds.FilePathRequiresAbsolute);
    }

    [Fact]
    public async Task FileExists_WithFullNameProperty_NoDiagnostic()
    {
        var diags = await GetDiagnosticsAsync("""
            using System.IO;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public override bool Execute()
                {
                    var fi = new FileInfo(TaskEnvironment.GetAbsolutePath("path.txt"));
                    File.Exists(fi.FullName);
                    return true;
                }
            }
            """);

        diags.ShouldNotContain(d => d.Id == DiagnosticIds.FilePathRequiresAbsolute);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Safe wrapper recognition: Path.GetDirectoryName, Path.Combine, Path.GetFullPath
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task DirectoryCreate_WithGetDirectoryNameOfAbsolutePath_NoDiagnostic()
    {
        var diags = await GetDiagnosticsAsync("""
            using System.IO;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public override bool Execute()
                {
                    AbsolutePath filePath = TaskEnvironment.GetAbsolutePath("file.txt");
                    string dir = Path.GetDirectoryName(filePath);
                    Directory.CreateDirectory(dir);
                    return true;
                }
            }
            """);

        diags.ShouldNotContain(d => d.Id == DiagnosticIds.FilePathRequiresAbsolute);
    }

    [Fact]
    public async Task DirectoryCreate_WithGetDirectoryNameOfString_ProducesDiagnostic()
    {
        var diags = await GetDiagnosticsAsync("""
            using System.IO;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public override bool Execute()
                {
                    string dir = Path.GetDirectoryName("relative/file.txt");
                    Directory.CreateDirectory(dir);
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.FilePathRequiresAbsolute);
    }

    [Fact]
    public async Task FileInfo_WithPathCombineSafeBase_NoDiagnostic()
    {
        var diags = await GetDiagnosticsAsync("""
            using System.IO;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public override bool Execute()
                {
                    AbsolutePath baseDir = TaskEnvironment.GetAbsolutePath("dir");
                    string combined = Path.Combine(baseDir, "sub", "file.txt");
                    var fi = new FileInfo(combined);
                    return true;
                }
            }
            """);

        diags.ShouldNotContain(d => d.Id == DiagnosticIds.FilePathRequiresAbsolute);
    }

    [Fact]
    public async Task FileInfo_WithPathCombineUnsafeBase_ProducesDiagnostic()
    {
        var diags = await GetDiagnosticsAsync("""
            using System.IO;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public override bool Execute()
                {
                    string combined = Path.Combine("relative", "file.txt");
                    var fi = new FileInfo(combined);
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.FilePathRequiresAbsolute);
    }

    [Fact]
    public async Task FileInfo_WithGetFullPathOfAbsolutePath_NoDiagnostic()
    {
        var diags = await GetDiagnosticsAsync("""
            using System.IO;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public override bool Execute()
                {
                    AbsolutePath basePath = TaskEnvironment.GetAbsolutePath("dir");
                    string fullPath = Path.GetFullPath(basePath);
                    var fi = new FileInfo(fullPath);
                    return true;
                }
            }
            """);

        diags.ShouldNotContain(d => d.Id == DiagnosticIds.FilePathRequiresAbsolute);
    }

    [Fact]
    public async Task FileInfo_WithGetFullPathOfRelative_ProducesDiagnostic()
    {
        var diags = await GetDiagnosticsAsync("""
            using System.IO;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public override bool Execute()
                {
                    string fullPath = Path.GetFullPath("relative.txt");
                    var fi = new FileInfo(fullPath);
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.FilePathRequiresAbsolute);
    }

    [Fact]
    public async Task FileWriteAllText_WithNestedPathCombineGetDirectoryName_NoDiagnostic()
    {
        // Simulates WriteLinesToFile pattern: Path.Combine(Path.GetDirectoryName(AbsolutePath), random)
        var diags = await GetDiagnosticsAsync("""
            using System.IO;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public override bool Execute()
                {
                    AbsolutePath filePath = TaskEnvironment.GetAbsolutePath("file.txt");
                    string dir = Path.GetDirectoryName(filePath);
                    string tempPath = Path.Combine(dir, Path.GetRandomFileName() + "~");
                    File.WriteAllText(tempPath, "contents");
                    return true;
                }
            }
            """);

        diags.ShouldNotContain(d => d.Id == DiagnosticIds.FilePathRequiresAbsolute);
    }

    [Fact]
    public async Task FileInfo_WithPathCombineOfFullName_NoDiagnostic()
    {
        // Simulates DownloadFile pattern: Path.Combine(DirectoryInfo.FullName, filename)
        var diags = await GetDiagnosticsAsync("""
            using System.IO;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public override bool Execute()
                {
                    AbsolutePath dirPath = TaskEnvironment.GetAbsolutePath("dir");
                    DirectoryInfo di = new DirectoryInfo(dirPath);
                    string combined = Path.Combine(di.FullName, "file.txt");
                    var fi = new FileInfo(combined);
                    return true;
                }
            }
            """);

        diags.ShouldNotContain(d => d.Id == DiagnosticIds.FilePathRequiresAbsolute);
    }

    [Fact]
    public async Task FileInfo_WithGetFullPathOfPathCombineSafe_NoDiagnostic()
    {
        // Simulates Unzip pattern: Path.GetFullPath(Path.Combine(DirectoryInfo.FullName, entry))
        var diags = await GetDiagnosticsAsync("""
            using System.IO;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public override bool Execute()
                {
                    AbsolutePath dirPath = TaskEnvironment.GetAbsolutePath("dir");
                    DirectoryInfo di = new DirectoryInfo(dirPath);
                    string fullPath = Path.GetFullPath(Path.Combine(di.FullName, "sub/entry"));
                    var fi = new FileInfo(fullPath);
                    return true;
                }
            }
            """);

        diags.ShouldNotContain(d => d.Id == DiagnosticIds.FilePathRequiresAbsolute);
    }

    [Fact]
    public async Task FileApi_InRegularTask_ProducesDiagnostic()
    {
        var diags = await GetDiagnosticsAsync("""
            using System.IO;
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    File.Exists("foo.txt");
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.FilePathRequiresAbsolute);
    }

    [Fact]
    public async Task DirectoryInfoFullName_SafePattern_NoDiagnostic()
    {
        var diags = await GetDiagnosticsAsync("""
            using System.IO;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public override bool Execute()
                {
                    var di = new DirectoryInfo(TaskEnvironment.GetAbsolutePath("mydir"));
                    Directory.Exists(di.FullName);
                    return true;
                }
            }
            """);

        diags.ShouldNotContain(d => d.Id == DiagnosticIds.FilePathRequiresAbsolute);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // MSBuildTask0004: Potential issues (review required)
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AssemblyLoadFrom_ProducesWarning()
    {
        var diags = await GetDiagnosticsAsync("""
            using System.Reflection;
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    Assembly.LoadFrom("mylib.dll");
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.PotentialIssue);
    }

    [Fact]
    public async Task AssemblyLoad_ByteArray_ProducesWarning()
    {
        var diags = await GetDiagnosticsAsync("""
            using System.Reflection;
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    Assembly.Load(new byte[0]);
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.PotentialIssue);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Non-task classes: No diagnostics
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task NonTaskClass_NoDiagnostic()
    {
        var diags = await GetDiagnosticsAsync("""
            using System;
            using System.IO;
            public class NotATask
            {
                public void DoStuff()
                {
                    Console.WriteLine("hello");
                    File.Exists("foo.txt");
                    Environment.Exit(1);
                    var psi = new System.Diagnostics.ProcessStartInfo("cmd.exe");
                }
            }
            """);

        diags.ShouldBeEmpty();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Edge Cases
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Lambda_InsideTask_DetectsUnsafeApi()
    {
        var diags = await GetDiagnosticsAsync("""
            using System;
            using System.Collections.Generic;
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    var items = new List<string> { "a", "b" };
                    items.ForEach(x => Console.WriteLine(x));
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.CriticalError);
    }

    [Fact]
    public async Task DerivedTask_InheritsITask_DetectsUnsafeApi()
    {
        var diags = await GetDiagnosticsAsync("""
            using System;
            public class BaseTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute() => true;
            }
            public class DerivedTask : BaseTask
            {
                public void DoWork()
                {
                    Console.WriteLine("derived");
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.CriticalError);
    }

    [Fact]
    public async Task GenericTask_DetectsUnsafeApi()
    {
        var diags = await GetDiagnosticsAsync("""
            using System;
            using Microsoft.Build.Framework;
            public class GenericTask<T> : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public override bool Execute()
                {
                    Console.WriteLine(typeof(T));
                    var val = Environment.GetEnvironmentVariable("X");
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.CriticalError);
        diags.ShouldContain(d => d.Id == DiagnosticIds.TaskEnvironmentRequired);
    }

    [Fact]
    public async Task PropertyGetter_WithUnsafeApi_DetectsIt()
    {
        var diags = await GetDiagnosticsAsync("""
            using System;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public string Dir => Environment.CurrentDirectory;
                public override bool Execute() => true;
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.TaskEnvironmentRequired);
    }

    [Fact]
    public async Task MethodReference_AsDelegate_DetectsIt()
    {
        var diags = await GetDiagnosticsAsync("""
            using System;
            using System.Collections.Generic;
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    var items = new List<string> { "a", "b" };
                    items.ForEach(Console.WriteLine);
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.CriticalError);
    }

    [Fact]
    public async Task MultipleUnsafeApis_AllDetected()
    {
        var diags = await GetDiagnosticsAsync("""
            using System;
            using System.IO;
            using System.Reflection;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public override bool Execute()
                {
                    Console.WriteLine("hello");
                    var val = Environment.GetEnvironmentVariable("X");
                    File.Exists("foo.txt");
                    Assembly.LoadFrom("lib.dll");
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.CriticalError);
        diags.ShouldContain(d => d.Id == DiagnosticIds.TaskEnvironmentRequired);
        diags.ShouldContain(d => d.Id == DiagnosticIds.FilePathRequiresAbsolute);
        diags.ShouldContain(d => d.Id == DiagnosticIds.PotentialIssue);
        diags.Length.ShouldBe(4);
    }

    [Fact]
    public async Task CorrectlyMigratedTask_NoDiagnostics()
    {
        var diags = await GetDiagnosticsAsync("""
            using System;
            using System.IO;
            using Microsoft.Build.Framework;
            public class CorrectTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public ITaskItem[] Items { get; set; }
                public override bool Execute()
                {
                    var dir = TaskEnvironment.ProjectDirectory;
                    var env = TaskEnvironment.GetEnvironmentVariable("X");
                    TaskEnvironment.SetEnvironmentVariable("X", "val");
                    AbsolutePath abs = TaskEnvironment.GetAbsolutePath("file.txt");
                    File.Exists(abs);
                    File.ReadAllText(TaskEnvironment.GetAbsolutePath("other.txt"));

                    foreach (var item in Items)
                    {
                        File.Exists(item.GetMetadata("FullPath"));
                    }
                    return true;
                }
            }
            """);

        diags.ShouldBeEmpty();
    }

    [Fact]
    public async Task FullyQualifiedConsole_DetectsIt()
    {
        var diags = await GetDiagnosticsAsync("""
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    System.Console.WriteLine("hello");
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.CriticalError);
    }

    [Fact]
    public async Task TryCatchFinally_DetectsAllUnsafeApis()
    {
        var diags = await GetDiagnosticsAsync("""
            using System;
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    try { Console.WriteLine("try"); }
                    catch { Console.WriteLine("catch"); }
                    finally { Console.WriteLine("finally"); }
                    return true;
                }
            }
            """);

        diags.Where(d => d.Id == DiagnosticIds.CriticalError).Count().ShouldBe(3);
    }

    [Fact]
    public async Task AsyncMethod_InsideTask_DetectsUnsafeApi()
    {
        var diags = await GetDiagnosticsAsync("""
            using System;
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    ExecuteAsync().Wait();
                    return true;
                }
                private async System.Threading.Tasks.Task ExecuteAsync()
                {
                    await System.Threading.Tasks.Task.Delay(1);
                    Console.WriteLine("async");
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.CriticalError);
    }

    [Fact]
    public async Task StringInterpolation_WithConsole_DetectsIt()
    {
        var diags = await GetDiagnosticsAsync("""
            using System;
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    var name = "world";
                    Console.WriteLine($"Hello {name}");
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.CriticalError);
    }

    [Fact]
    public async Task EmptyTask_NoDiagnostics()
    {
        var diags = await GetDiagnosticsAsync("""
            public class EmptyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute() => true;
            }
            """);

        diags.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetMetadata_NonFullPath_StillTriggersWarning()
    {
        var diags = await GetDiagnosticsAsync("""
            using System.IO;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public ITaskItem[] Items { get; set; }
                public override bool Execute()
                {
                    foreach (var item in Items)
                    {
                        File.Exists(item.GetMetadata("Identity"));
                    }
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.FilePathRequiresAbsolute);
    }

    [Fact]
    public async Task NestedClass_NotTask_NoFalsePositive()
    {
        var diags = await GetDiagnosticsAsync("""
            using System;
            public class OuterTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    new Inner().DoWork();
                    return true;
                }
                private class Inner
                {
                    public void DoWork() { Console.WriteLine("nested"); }
                }
            }
            """);

        // Inner class is not an ITask - should NOT get diagnostics
        diags.ShouldBeEmpty();
    }

    [Fact]
    public async Task MSBuildTask0002_FiredForRegularTask()
    {
        var diags = await GetDiagnosticsAsync("""
            using System;
            using System.IO;
            using System.Diagnostics;
            public class RegularTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    Environment.GetEnvironmentVariable("X");
                    Path.GetFullPath("foo");
                    Process.Start("cmd.exe");
                    var psi = new ProcessStartInfo("cmd.exe");
                    return true;
                }
            }
            """);

        // MSBuildTask0002 now fires for all ITask implementations
        diags.ShouldContain(d => d.Id == DiagnosticIds.TaskEnvironmentRequired);
    }

    [Fact]
    public async Task MSBuildTask0003_FiredForRegularTask()
    {
        var diags = await GetDiagnosticsAsync("""
            using System.IO;
            public class RegularTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    File.Exists("foo.txt");
                    new FileInfo("bar.txt");
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.FilePathRequiresAbsolute);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Iteration 9-13: New APIs and features
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task DirectorySetCurrentDirectory_ProducesCriticalError()
    {
        var diags = await GetDiagnosticsAsync("""
            using System.IO;
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    Directory.SetCurrentDirectory("/tmp");
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.CriticalError);
    }

    [Fact]
    public async Task DirectoryGetCurrentDirectory_InMultiThreadable_ProducesWarning()
    {
        var diags = await GetDiagnosticsAsync("""
            using System.IO;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public override bool Execute()
                {
                    var dir = Directory.GetCurrentDirectory();
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.TaskEnvironmentRequired);
    }

    [Fact]
    public async Task PathGetTempPath_InMultiThreadable_ProducesWarning()
    {
        var diags = await GetDiagnosticsAsync("""
            using System.IO;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public override bool Execute()
                {
                    var tmp = Path.GetTempPath();
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.TaskEnvironmentRequired);
    }

    [Fact]
    public async Task PathGetTempFileName_InMultiThreadable_ProducesWarning()
    {
        var diags = await GetDiagnosticsAsync("""
            using System.IO;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public override bool Execute()
                {
                    var f = Path.GetTempFileName();
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.TaskEnvironmentRequired);
    }

    [Fact]
    public async Task EnvironmentGetFolderPath_InMultiThreadable_ProducesWarning()
    {
        var diags = await GetDiagnosticsAsync("""
            using System;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public override bool Execute()
                {
                    var p = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.TaskEnvironmentRequired);
    }

    [Fact]
    public async Task ConsoleSetOut_TypeLevelBan_ProducesError()
    {
        var diags = await GetDiagnosticsAsync("""
            using System;
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    Console.ResetColor();
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.CriticalError);
    }

    [Fact]
    public async Task ConsoleForegroundColor_TypeLevelBan_ProducesError()
    {
        var diags = await GetDiagnosticsAsync("""
            using System;
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.CriticalError);
    }

    [Fact]
    public async Task ConsoleTitle_TypeLevelBan_ProducesError()
    {
        var diags = await GetDiagnosticsAsync("""
            using System;
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    var t = Console.Title;
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.CriticalError);
    }

    [Fact]
    public async Task ProcessStartWithPSI_InMultiThreadable_ProducesWarning()
    {
        var diags = await GetDiagnosticsAsync("""
            using System.Diagnostics;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public override bool Execute()
                {
                    var psi = new ProcessStartInfo("cmd");
                    Process.Start(psi);
                    return true;
                }
            }
            """);

        // Should flag both: new ProcessStartInfo and Process.Start(ProcessStartInfo)
        diags.Where(d => d.Id == DiagnosticIds.TaskEnvironmentRequired).Count().ShouldBeGreaterThanOrEqualTo(2);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // [MSBuildMultiThreadableTaskAnalyzed] attribute on helper classes
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task HelperClass_WithAttribute_AnalyzedLikeMultiThreadableTask()
    {
        var diags = await GetDiagnosticsAsync("""
            using System;
            using System.IO;
            using Microsoft.Build.Framework;

            [MSBuildMultiThreadableTaskAnalyzed]
            public class FileHelper
            {
                public void ProcessFile(string path)
                {
                    File.Exists(path);
                    var env = Environment.GetEnvironmentVariable("PATH");
                }
            }
            """);

        // Should detect File.Exists with unwrapped path (MSBuildTask0003)
        diags.ShouldContain(d => d.Id == DiagnosticIds.FilePathRequiresAbsolute);
        // Should detect Environment.GetEnvironmentVariable (MSBuildTask0002)
        diags.ShouldContain(d => d.Id == DiagnosticIds.TaskEnvironmentRequired);
    }

    [Fact]
    public async Task HelperClass_WithoutAttribute_NotAnalyzed()
    {
        var diags = await GetDiagnosticsAsync("""
            using System;
            using System.IO;

            public class RegularHelper
            {
                public void ProcessFile(string path)
                {
                    File.Exists(path);
                    var env = Environment.GetEnvironmentVariable("PATH");
                    Console.WriteLine("hello");
                }
            }
            """);

        diags.ShouldBeEmpty();
    }

    [Fact]
    public async Task HelperClass_WithAttribute_ConsoleDetected()
    {
        var diags = await GetDiagnosticsAsync("""
            using System;
            using Microsoft.Build.Framework;

            [MSBuildMultiThreadableTaskAnalyzed]
            public class LogHelper
            {
                public void Log(string message)
                {
                    Console.WriteLine(message);
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.CriticalError);
    }

    [Fact]
    public async Task HelperClass_WithAttribute_SafePatterns_NoDiagnostic()
    {
        var diags = await GetDiagnosticsAsync("""
            using System.IO;
            using Microsoft.Build.Framework;

            [MSBuildMultiThreadableTaskAnalyzed]
            public class SafeHelper
            {
                public void Process(TaskEnvironment env, ITaskItem item)
                {
                    AbsolutePath abs = env.GetAbsolutePath("file.txt");
                    File.Exists(abs);
                    File.ReadAllText(item.GetMetadata("FullPath"));
                }
            }
            """);

        diags.Where(d => d.Id == DiagnosticIds.FilePathRequiresAbsolute).ShouldBeEmpty();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Multi-path parameter correctness (File.Copy has 2 path args)
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task FileCopy_SecondArgUnwrapped_ProducesDiagnostic()
    {
        var diags = await GetDiagnosticsAsync("""
            using System.IO;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public override bool Execute()
                {
                    AbsolutePath src = TaskEnvironment.GetAbsolutePath("src.txt");
                    File.Copy(src, "dest.txt");
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.FilePathRequiresAbsolute);
    }

    [Fact]
    public async Task FileCopy_BothArgsWrapped_NoDiagnostic()
    {
        var diags = await GetDiagnosticsAsync("""
            using System.IO;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public override bool Execute()
                {
                    AbsolutePath src = TaskEnvironment.GetAbsolutePath("src.txt");
                    AbsolutePath dst = TaskEnvironment.GetAbsolutePath("dst.txt");
                    File.Copy(src, dst);
                    return true;
                }
            }
            """);

        diags.Where(d => d.Id == DiagnosticIds.FilePathRequiresAbsolute).ShouldBeEmpty();
    }

    [Fact]
    public async Task FileCopy_FirstArgUnwrapped_ProducesDiagnostic()
    {
        var diags = await GetDiagnosticsAsync("""
            using System.IO;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public override bool Execute()
                {
                    AbsolutePath dst = TaskEnvironment.GetAbsolutePath("dst.txt");
                    File.Copy("src.txt", dst);
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.FilePathRequiresAbsolute);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Edge cases: LINQ lambdas, nested types, string literals
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Lambda_FileExistsInsideLambda_ProducesDiagnostic()
    {
        var diags = await GetDiagnosticsAsync("""
            using System;
            using System.IO;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public override bool Execute()
                {
                    Func<string, bool> check = path => File.Exists(path);
                    return check("test.txt");
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.FilePathRequiresAbsolute);
    }

    [Fact]
    public async Task NestedClass_InsideTask_NotAnalyzedSeparately()
    {
        // Nested class within a task should NOT independently trigger analysis
        // (it's not a task itself and doesn't have the attribute)
        var diags = await GetDiagnosticsAsync("""
            using System;
            using System.IO;
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    var helper = new Helper();
                    return true;
                }

                private class Helper
                {
                    public void DoWork()
                    {
                        File.Exists("foo.txt");
                        Console.WriteLine("nested");
                    }
                }
            }
            """);

        // The outer task has Console.* in its scope but NOT in nested class
        // Nested class operations are NOT in the outer type's symbol scope
        diags.Where(d => d.Id == DiagnosticIds.CriticalError).ShouldBeEmpty();
    }

    [Fact]
    public async Task StringInterpolation_ConsoleWithInterpolation_ProducesDiagnostic()
    {
        var diags = await GetDiagnosticsAsync("""
            using System;
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    int x = 42;
                    Console.WriteLine($"value = {x}");
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.CriticalError);
    }

    [Fact]
    public async Task StaticMethod_InTask_DetectsUnsafeApis()
    {
        var diags = await GetDiagnosticsAsync("""
            using System;
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    LogMessage("hello");
                    return true;
                }

                private static void LogMessage(string msg)
                {
                    Console.WriteLine(msg);
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.CriticalError);
    }

    [Fact]
    public async Task PropertyGetter_WithBannedApi_ProducesDiagnostic()
    {
        var diags = await GetDiagnosticsAsync("""
            using System;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public string CurrentDir => Environment.CurrentDirectory;
                public override bool Execute() => true;
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.TaskEnvironmentRequired);
    }

    [Fact]
    public async Task AsyncHelper_WithBannedApi_ProducesDiagnostic()
    {
        var diags = await GetDiagnosticsAsync("""
            using System;
            using System.IO;
            using System.Threading.Tasks;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public override bool Execute()
                {
                    ProcessAsync().Wait();
                    return true;
                }

                private async Task ProcessAsync()
                {
                    File.Exists("file.txt");
                    await Task.Delay(1);
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.FilePathRequiresAbsolute);
    }

    [Fact]
    public async Task ConditionalAccess_WithBannedApi_ProducesDiagnostic()
    {
        var diags = await GetDiagnosticsAsync("""
            using System;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public string Path { get; set; }
                public override bool Execute()
                {
                    var dir = System.IO.Path.GetFullPath(Path ?? ".");
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.TaskEnvironmentRequired);
    }

    [Fact]
    public async Task FileApi_WithConstantPath_StillProducesDiagnostic()
    {
        // Even constant paths need absolutization - the task might be invoked from different directories
        var diags = await GetDiagnosticsAsync("""
            using System.IO;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                private const string LogFile = "build.log";
                public override bool Execute()
                {
                    File.WriteAllText(LogFile, "done");
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.FilePathRequiresAbsolute);
    }

    [Fact]
    public async Task MultipleViolationsInSingleMethod_AllDetected()
    {
        var diags = await GetDiagnosticsAsync("""
            using System;
            using System.IO;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public override bool Execute()
                {
                    Console.WriteLine("a");
                    var env = Environment.GetEnvironmentVariable("X");
                    File.Exists("foo");
                    Console.ReadLine();
                    return true;
                }
            }
            """);

        diags.Where(d => d.Id == DiagnosticIds.CriticalError).Count().ShouldBeGreaterThanOrEqualTo(2);
        diags.ShouldContain(d => d.Id == DiagnosticIds.TaskEnvironmentRequired);
        diags.ShouldContain(d => d.Id == DiagnosticIds.FilePathRequiresAbsolute);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // [MSBuildMultiThreadableTask] attribute on task classes
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Task_WithMultiThreadableAttribute_AnalyzedForAllRules()
    {
        // A task with [MSBuildMultiThreadableTask] but NOT IMultiThreadableTask
        // should still get MSBuildTask0002 and MSBuildTask0003
        var diags = await GetDiagnosticsAsync("""
            using System;
            using System.IO;
            using Microsoft.Build.Framework;

            [MSBuildMultiThreadableTask]
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    File.Exists("foo.txt");
                    var env = Environment.GetEnvironmentVariable("PATH");
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.FilePathRequiresAbsolute);
        diags.ShouldContain(d => d.Id == DiagnosticIds.TaskEnvironmentRequired);
    }

    [Fact]
    public async Task Task_WithoutMultiThreadableAttribute_GetsAllRules()
    {
        // All rules now fire on all ITask implementations
        var diags = await GetDiagnosticsAsync("""
            using System;
            using System.IO;
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    File.Exists("foo.txt");
                    var env = Environment.GetEnvironmentVariable("PATH");
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.FilePathRequiresAbsolute);
        diags.ShouldContain(d => d.Id == DiagnosticIds.TaskEnvironmentRequired);
    }

    [Fact]
    public async Task UsingStaticConsole_DetectedByTypeLevelBan()
    {
        var diags = await GetDiagnosticsAsync("""
            using static System.Console;
            public class MyTask : Microsoft.Build.Utilities.Task
            {
                public override bool Execute()
                {
                    WriteLine("hello from using static");
                    return true;
                }
            }
            """);

        diags.ShouldContain(d => d.Id == DiagnosticIds.CriticalError);
    }

    [Fact]
    public async Task FileWriteAllText_NonPathStringParam_NoDiagnosticForContents()
    {
        // File.WriteAllText(string path, string contents) - "contents" should NOT be flagged
        var diags = await GetDiagnosticsAsync("""
            using System.IO;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public override bool Execute()
                {
                    AbsolutePath p = TaskEnvironment.GetAbsolutePath("file.txt");
                    File.WriteAllText(p, "contents here");
                    return true;
                }
            }
            """);

        diags.Where(d => d.Id == DiagnosticIds.FilePathRequiresAbsolute).ShouldBeEmpty();
    }

    [Fact]
    public async Task FileAppendAllText_PathUnwrapped_FlagsPath()
    {
        var diags = await GetDiagnosticsAsync("""
            using System.IO;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public override bool Execute()
                {
                    File.AppendAllText("file.txt", "contents");
                    return true;
                }
            }
            """);

        // Should flag the path parameter but NOT the contents parameter
        diags.Where(d => d.Id == DiagnosticIds.FilePathRequiresAbsolute).Count().ShouldBe(1);
    }

    [Fact]
    public async Task FileWriteAllText_NamedArguments_HandledCorrectly()
    {
        // Named arguments can change the order of arguments in source vs parameters
        var diags = await GetDiagnosticsAsync("""
            using System.IO;
            using Microsoft.Build.Framework;
            public class MyTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
            {
                public TaskEnvironment TaskEnvironment { get; set; }
                public override bool Execute()
                {
                    // Named argument puts "contents" first in source, but "path" is still the path param
                    File.WriteAllText(contents: "some text", path: "file.txt");
                    return true;
                }
            }
            """);

        // Should still flag the path parameter
        diags.Where(d => d.Id == DiagnosticIds.FilePathRequiresAbsolute).Count().ShouldBe(1);
    }
}
