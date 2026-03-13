// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// ═══════════════════════════════════════════════════════════════════════════════
// DEMO 1: IMultiThreadableTask with ALL categories of problematic APIs
// Should produce diagnostics for MSBuildTask0001, 0002, 0003, 0004
// ═══════════════════════════════════════════════════════════════════════════════

#nullable disable

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace AnalyzerDemo
{
    // ─────────────────────────────────────────────────────────────────────────
    // TASK 1: Kitchen sink of all problematic patterns
    // Expected: 2 errors (0001), 3 warnings (0002), 7+ warnings (0003),
    //           1 warning (0004)
    // ─────────────────────────────────────────────────────────────────────────
    public class KitchenSinkTask : Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; }
        public string InputFile { get; set; }
        public string OutputDir { get; set; }

        public override bool Execute()
        {
            // ── MSBuildTask0001: Critical errors ──────────────────────────────
            Console.WriteLine("hello");              // MSBuildTask0001: Console
            Environment.Exit(1);                     // MSBuildTask0001: Exit

            // ── MSBuildTask0002: TaskEnvironment required ─────────────────────
            string cwd = Environment.CurrentDirectory;             // MSBuildTask0002
            string val = Environment.GetEnvironmentVariable("X");  // MSBuildTask0002
            string fp = Path.GetFullPath(InputFile);               // MSBuildTask0002

            // ── MSBuildTask0003: File path needs absolute ─────────────────────
            File.Exists(InputFile);                          // MSBuildTask0003
            Directory.Exists(OutputDir);                     // MSBuildTask0003
            Directory.CreateDirectory(OutputDir);            // MSBuildTask0003
            File.ReadAllText(InputFile);                     // MSBuildTask0003
            var fi = new FileInfo(InputFile);                // MSBuildTask0003
            var di = new DirectoryInfo(OutputDir);           // MSBuildTask0003
            using var sr = new StreamReader(InputFile);      // MSBuildTask0003

            // ── MSBuildTask0004: Potential issues ─────────────────────────────
            Assembly.LoadFrom("plugin.dll");                 // MSBuildTask0004

            return true;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TASK 2: Correctly migrated task - should produce ZERO diagnostics
    // ─────────────────────────────────────────────────────────────────────────
    public class CorrectlyMigratedTask : Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; }
        public string InputFile { get; set; }
        public string OutputDir { get; set; }

        public override bool Execute()
        {
            // ✅ Using TaskEnvironment for environment
            string projDir = TaskEnvironment.ProjectDirectory;
            string val = TaskEnvironment.GetEnvironmentVariable("X");
            TaskEnvironment.SetEnvironmentVariable("Y", "value");

            // ✅ Using GetAbsolutePath for file operations
            AbsolutePath absInput = TaskEnvironment.GetAbsolutePath(InputFile);
            AbsolutePath absOutput = TaskEnvironment.GetAbsolutePath(OutputDir);

            File.Exists(absInput);                     // ✅ AbsolutePath implicit conversion
            Directory.Exists(absOutput);               // ✅ AbsolutePath implicit conversion
            Directory.CreateDirectory(absOutput);
            File.ReadAllText(absInput);
            var fi = new FileInfo(absInput);            // ✅ AbsolutePath conversion
            var di = new DirectoryInfo(absOutput);

            // ✅ Using TaskEnvironment for process start
            ProcessStartInfo psi = TaskEnvironment.GetProcessStartInfo();
            psi.FileName = "tool.exe";

            // ✅ Logging instead of Console
            Log.LogMessage("Processing file");

            return true;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TASK 3: Regular ITask (NOT IMultiThreadableTask)
    // Should still get MSBuildTask0001 and MSBuildTask0004 but NOT 0002/0003
    // ─────────────────────────────────────────────────────────────────────────
    public class RegularTask : Task
    {
        public string InputFile { get; set; }

        public override bool Execute()
        {
            // ❌ MSBuildTask0001: Console.* always wrong in tasks
            Console.WriteLine("hello from regular task");

            // ❌ MSBuildTask0001: Environment.Exit always wrong
            Environment.Exit(42);

            // ✅ No warning: Environment.GetEnvironmentVariable OK in regular task
            string val = Environment.GetEnvironmentVariable("PATH");

            // ✅ No warning: File.Exists OK in regular task (not multithreaded)
            File.Exists(InputFile);

            // ❌ MSBuildTask0004: Assembly.LoadFrom still flagged
            Assembly.LoadFrom("plugin.dll");

            return true;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TASK 4: Safe patterns - ITaskItem.GetMetadata("FullPath")
    // Should produce ZERO diagnostics
    // ─────────────────────────────────────────────────────────────────────────
    public class MetadataFullPathTask : Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; }

        [Required]
        public ITaskItem[] SourceFiles { get; set; }

        public override bool Execute()
        {
            foreach (ITaskItem item in SourceFiles)
            {
                // ✅ GetMetadata("FullPath") is safe - MSBuild resolves it
                string fullPath = item.GetMetadata("FullPath");
                File.Exists(fullPath);  // ✅ No warning - comes from GetMetadata("FullPath")
                                        // Note: this is still flagged because we check the argument
                                        // at the call site, not data flow. This is a known limitation.

                // ✅ Using TaskEnvironment.GetAbsolutePath for ItemSpec
                AbsolutePath abs = TaskEnvironment.GetAbsolutePath(item.ItemSpec);
                File.ReadAllText(abs);  // ✅ No warning
            }

            return true;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TASK 5: FileInfo.FullName is safe
    // ─────────────────────────────────────────────────────────────────────────
    public class FileInfoFullNameTask : Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; }
        public string InputFile { get; set; }

        public override bool Execute()
        {
            AbsolutePath abs = TaskEnvironment.GetAbsolutePath(InputFile);
            var fi = new FileInfo(abs);

            // ✅ Using FileInfo.FullName is safe
            File.ReadAllText(fi.FullName);

            return true;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TASK 6: Multiple Console overloads
    // All Console methods should be flagged as MSBuildTask0001
    // ─────────────────────────────────────────────────────────────────────────
    public class ConsoleOverloadsTask : Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; }

        public override bool Execute()
        {
            Console.Write("a");                    // MSBuildTask0001
            Console.Write(42);                     // MSBuildTask0001
            Console.Write(true);                   // MSBuildTask0001
            Console.Write('x');                    // MSBuildTask0001
            Console.Write("{0}", "a");             // MSBuildTask0001
            Console.WriteLine();                   // MSBuildTask0001
            Console.WriteLine("b");                // MSBuildTask0001
            Console.WriteLine(42);                 // MSBuildTask0001
            Console.WriteLine("{0}", "b");         // MSBuildTask0001
            Console.ReadLine();                    // MSBuildTask0001
            var _ = Console.In;                    // MSBuildTask0001
            var __ = Console.Out;                  // MSBuildTask0001
            var ___ = Console.Error;               // MSBuildTask0001

            return true;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TASK 7: ProcessStartInfo and Process.Start
    // ─────────────────────────────────────────────────────────────────────────
    public class ProcessStartTask : Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; }

        public override bool Execute()
        {
            // ❌ MSBuildTask0002: ProcessStartInfo constructors
            var psi1 = new ProcessStartInfo();              // MSBuildTask0002
            var psi2 = new ProcessStartInfo("cmd.exe");     // MSBuildTask0002
            var psi3 = new ProcessStartInfo("cmd", "/c");   // MSBuildTask0002

            // ❌ MSBuildTask0002: Process.Start overloads
            Process.Start("notepad.exe");                   // MSBuildTask0002
            Process.Start("cmd.exe", "/c dir");             // MSBuildTask0002

            return true;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TASK 8: ThreadPool and CultureInfo
    // ─────────────────────────────────────────────────────────────────────────
    public class ThreadPoolCultureTask : Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; }

        public override bool Execute()
        {
            // ❌ MSBuildTask0001: ThreadPool settings
            ThreadPool.SetMinThreads(4, 4);                // MSBuildTask0001
            ThreadPool.SetMaxThreads(16, 16);              // MSBuildTask0001

            // ❌ MSBuildTask0001: CultureInfo defaults
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;    // MSBuildTask0001
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;  // MSBuildTask0001

            return true;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TASK 9: Environment.FailFast overloads
    // ─────────────────────────────────────────────────────────────────────────
    public class EnvironmentFailFastTask : Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; }

        public override bool Execute()
        {
            if (false)
            {
                Environment.FailFast("fatal");                            // MSBuildTask0001
                Environment.FailFast("fatal", new Exception());           // MSBuildTask0001
            }

            return true;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TASK 10: Environment variable edge cases
    // ─────────────────────────────────────────────────────────────────────────
    public class EnvVarEdgeCasesTask : Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; }

        public override bool Execute()
        {
            // ❌ MSBuildTask0002: 3-parameter SetEnvironmentVariable
            Environment.SetEnvironmentVariable("X", "Y", EnvironmentVariableTarget.Process);  // MSBuildTask0002

            // ❌ MSBuildTask0002: ExpandEnvironmentVariables
            string expanded = Environment.ExpandEnvironmentVariables("%PATH%");  // MSBuildTask0002

            // ❌ MSBuildTask0002: GetEnvironmentVariables (no parameter)
            var envVars = Environment.GetEnvironmentVariables();  // MSBuildTask0002

            return true;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TASK 11: File API edge cases - StreamWriter, multiple parameters
    // ─────────────────────────────────────────────────────────────────────────
    public class FileApiEdgeCasesTask : Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; }
        public string Path1 { get; set; }
        public string Path2 { get; set; }

        public override bool Execute()
        {
            // ❌ MSBuildTask0003: StreamWriter constructor
            using var sw = new StreamWriter(Path1);          // MSBuildTask0003

            // ❌ MSBuildTask0003: FileStream constructor
            using var fs = new FileStream(Path1, FileMode.Open);  // MSBuildTask0003

            // ❌ MSBuildTask0003: File.Copy (both paths unwrapped)
            File.Copy(Path1, Path2);                         // MSBuildTask0003 (first param)

            // ❌ MSBuildTask0003: File.Move
            File.Move(Path1, Path2);                         // MSBuildTask0003 (first param)

            // ❌ MSBuildTask0003: File.WriteAllText
            File.WriteAllText(Path1, "content");             // MSBuildTask0003

            // ❌ MSBuildTask0003: Directory.GetFiles
            Directory.GetFiles(Path1);                       // MSBuildTask0003

            // ❌ MSBuildTask0003: Directory.EnumerateFiles
            Directory.EnumerateFiles(Path1);                 // MSBuildTask0003

            // ❌ MSBuildTask0003: Directory.Delete
            Directory.Delete(Path1);                         // MSBuildTask0003

            return true;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TASK 12: Assembly loading edge cases
    // ─────────────────────────────────────────────────────────────────────────
    public class AssemblyLoadEdgeCasesTask : Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; }

        public override bool Execute()
        {
            // ❌ MSBuildTask0004: Assembly.Load overloads
            Assembly.Load("MyAssembly");                     // MSBuildTask0004
            Assembly.Load(new byte[] { });                   // MSBuildTask0004
            Assembly.LoadFrom("MyAssembly.dll");             // MSBuildTask0004
            Assembly.LoadFile("MyAssembly.dll");             // MSBuildTask0004

            // ❌ MSBuildTask0004: Activator
            Activator.CreateInstance("asm", "type");         // MSBuildTask0004

            return true;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TASK 13: Mixed safe and unsafe in same method
    // Tests that the analyzer correctly differentiates
    // ─────────────────────────────────────────────────────────────────────────
    public class MixedSafeUnsafeTask : Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; }
        public string InputFile { get; set; }

        public override bool Execute()
        {
            AbsolutePath absPath = TaskEnvironment.GetAbsolutePath(InputFile);

            // ✅ Safe: using AbsolutePath
            if (File.Exists(absPath))
            {
                string content = File.ReadAllText(absPath);

                // ❌ MSBuildTask0003: Raw string passed to File.WriteAllText
                File.WriteAllText(InputFile + ".bak", content);
            }

            // ✅ Safe: TaskEnvironment property
            string projDir = TaskEnvironment.ProjectDirectory;

            // ❌ MSBuildTask0002: Environment.CurrentDirectory
            string cwd = Environment.CurrentDirectory;

            return true;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TASK 14: Path.GetFullPath with two parameters
    // ─────────────────────────────────────────────────────────────────────────
    public class PathGetFullPathTwoParamsTask : Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; }
        public string InputFile { get; set; }

        public override bool Execute()
        {
            // ❌ MSBuildTask0002: Path.GetFullPath(string, string) overload
            string full = Path.GetFullPath(InputFile, "C:\\base");  // MSBuildTask0002

            return true;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TASK 15: Process.Kill edge case
    // ─────────────────────────────────────────────────────────────────────────
    public class ProcessKillTask : Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; }

        public override bool Execute()
        {
            var p = Process.GetCurrentProcess();
            p.Kill();            // MSBuildTask0001
            p.Kill(true);       // MSBuildTask0001

            return true;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TASK 16: Helper method that delegates file operations
    // Tests that analysis works within methods called from Execute
    // ─────────────────────────────────────────────────────────────────────────
    public class HelperMethodTask : Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; }
        public string InputFile { get; set; }

        public override bool Execute()
        {
            ProcessFile(InputFile);
            return true;
        }

        private void ProcessFile(string path)
        {
            // ❌ MSBuildTask0003: Helper method still flagged (it's in the task class)
            File.ReadAllText(path);

            // ❌ MSBuildTask0001: Console in helper method too
            Console.WriteLine($"Processed: {path}");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TASK 17: Non-task class should NOT be analyzed
    // ─────────────────────────────────────────────────────────────────────────
    public class NotATask
    {
        public void DoWork()
        {
            // ✅ No diagnostics - this is not a task
            Console.WriteLine("This is fine");
            File.Exists("anything");
            Environment.CurrentDirectory = "whatever";
            Environment.Exit(0);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TASK 18: Task inheriting from another task
    // ─────────────────────────────────────────────────────────────────────────
    public class DerivedTask : CorrectlyMigratedTask
    {
        public override bool Execute()
        {
            // ❌ This task inherits IMultiThreadableTask
            File.Exists("relative/path.txt");   // MSBuildTask0003
            return true;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TASK 19: Task using ToolTask (still implements ITask)
    // ─────────────────────────────────────────────────────────────────────────
    public class ToolTaskDerivedTask : Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; }
        public string SomePath { get; set; }
        public IBuildEngine BuildEngine { get; set; }
        public ITaskHost HostObject { get; set; }

        public override bool Execute()
        {
            // ❌ MSBuildTask0003
            Directory.CreateDirectory(SomePath);

            // ❌ MSBuildTask0001
            Console.Error.WriteLine("error!");

            return true;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TASK 20: Batch processing with exception handling pattern
    // Tests that analysis works correctly with try/catch
    // ─────────────────────────────────────────────────────────────────────────
    public class BatchProcessingTask : Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; }
        public ITaskItem[] SourceFiles { get; set; }
        public ITaskItem[] DestinationFiles { get; set; }

        public override bool Execute()
        {
            bool success = true;

            for (int i = 0; i < SourceFiles.Length; i++)
            {
                string sourceSpec = SourceFiles[i].ItemSpec;
                string destSpec = DestinationFiles[i].ItemSpec;

                try
                {
                    AbsolutePath source = TaskEnvironment.GetAbsolutePath(sourceSpec);
                    AbsolutePath dest = TaskEnvironment.GetAbsolutePath(destSpec);

                    File.Copy(source, dest);  // ✅ Safe - AbsolutePath
                }
                catch (Exception ex)
                {
                    Log.LogError($"Failed to copy {sourceSpec}: {ex.Message}");
                    success = false;
                }
            }

            return success;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TASK 21: Chained method calls
    // ─────────────────────────────────────────────────────────────────────────
    public class ChainedCallTask : Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; }
        public string Dir { get; set; }

        public override bool Execute()
        {
            // ❌ MSBuildTask0003: Path.Combine result is not absolute
            string combined = Path.Combine(Dir, "subdir", "file.txt");
            File.Exists(combined);

            // ✅ Safe: GetAbsolutePath of combined path
            File.Exists(TaskEnvironment.GetAbsolutePath(combined));

            return true;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TASK 22: Fully qualified API names (System.IO.File instead of File)
    // ─────────────────────────────────────────────────────────────────────────
    public class FullyQualifiedTask : Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; }
        public string InputFile { get; set; }

        public override bool Execute()
        {
            // ❌ Should still be caught even with fully qualified names
            System.IO.File.Exists(InputFile);                    // MSBuildTask0003
            System.IO.Directory.CreateDirectory(InputFile);      // MSBuildTask0003
            System.Environment.Exit(1);                          // MSBuildTask0001
            System.Console.WriteLine("bad");                     // MSBuildTask0001

            return true;
        }
    }
}
