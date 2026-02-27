// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// ═══════════════════════════════════════════════════════════════════════════════
// ADVANCED EDGE CASES - Part 1: Partial Classes, Generics, Async, Lambdas
// ═══════════════════════════════════════════════════════════════════════════════

#nullable disable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Task = Microsoft.Build.Utilities.Task;

namespace AnalyzerDemo
{
    // ─────────────────────────────────────────────────────────────────────────
    // TASK 23: Partial class - Part A (see AdvancedEdgeCases2.cs for Part B)
    // Both parts should be analyzed since the combined type implements IMultiThreadableTask
    // ─────────────────────────────────────────────────────────────────────────
    public partial class PartialTask : Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; }
        public string InputFile { get; set; }

        public override bool Execute()
        {
            // ❌ MSBuildTask0001: Console in partial class Part A
            Console.WriteLine("Part A");

            // Call helper in Part B
            DoPartBWork();
            return true;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TASK 24: Generic task class
    // Should be analyzed since it implements IMultiThreadableTask
    // ─────────────────────────────────────────────────────────────────────────
    public class GenericTask<T> : Task, IMultiThreadableTask where T : class
    {
        public TaskEnvironment TaskEnvironment { get; set; }
        public T Data { get; set; }
        public string OutputPath { get; set; }

        public override bool Execute()
        {
            // ❌ MSBuildTask0003: File API with non-absolute path
            File.WriteAllText(OutputPath, Data?.ToString());

            // ❌ MSBuildTask0002: Environment.CurrentDirectory
            string cwd = Environment.CurrentDirectory;

            return true;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TASK 25: Lambda and delegate patterns
    // Banned APIs used inside lambdas within task class should be caught
    // ─────────────────────────────────────────────────────────────────────────
    public class LambdaTask : Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; }
        public ITaskItem[] Items { get; set; }

        public override bool Execute()
        {
            // ❌ MSBuildTask0003: File.Exists inside lambda
            var exists = Items.Select(i => File.Exists(i.ItemSpec)).ToArray();

            // ❌ MSBuildTask0001: Console inside lambda
            Items.ToList().ForEach(i => Console.WriteLine(i.ItemSpec));

            // ❌ MSBuildTask0002: Environment in lambda
            Func<string> getVar = () => Environment.GetEnvironmentVariable("HOME");

            // ❌ MSBuildTask0003: File.ReadAllText in local function
            string ReadFile(string path) => File.ReadAllText(path);

            return true;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TASK 26: LINQ with file operations
    // ─────────────────────────────────────────────────────────────────────────
    public class LinqFileTask : Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; }
        public ITaskItem[] SourceFiles { get; set; }

        public override bool Execute()
        {
            // ❌ MSBuildTask0003: File.Exists in LINQ Where
            var validFiles = SourceFiles
                .Where(f => File.Exists(f.ItemSpec))
                .ToArray();

            // ❌ MSBuildTask0003: File.ReadAllText in LINQ Select
            var contents = SourceFiles
                .Select(f => File.ReadAllText(f.ItemSpec))
                .ToArray();

            // ❌ MSBuildTask0003: Directory.GetFiles in LINQ SelectMany
            var allFiles = SourceFiles
                .SelectMany(f => Directory.GetFiles(f.ItemSpec))
                .ToArray();

            return true;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TASK 27: Property getters/setters with unsafe APIs
    // ─────────────────────────────────────────────────────────────────────────
    public class PropertyUnsafeTask : Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; }

        // ❌ MSBuildTask0002: Environment.CurrentDirectory in property getter
        public string WorkDir => Environment.CurrentDirectory;

        // ❌ MSBuildTask0002: Environment.GetEnvironmentVariable in property getter
        public string HomePath => Environment.GetEnvironmentVariable("HOME");

        public override bool Execute()
        {
            string wd = WorkDir;
            string home = HomePath;
            return true;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TASK 28: Nested class implementing ITask
    // The nested class should be analyzed independently
    // ─────────────────────────────────────────────────────────────────────────
    public class OuterTask : Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; }

        public override bool Execute() => true; // ✅ No issues

        // This nested class is also a task - should be analyzed
        public class InnerTask : Task, IMultiThreadableTask
        {
            public TaskEnvironment TaskEnvironment { get; set; }
            public string Path { get; set; }

            public override bool Execute()
            {
                // ❌ MSBuildTask0003: File API in nested task
                File.Exists(Path);

                // ❌ MSBuildTask0001: Console in nested task
                Console.WriteLine("inner task");

                return true;
            }
        }

        // This nested class is NOT a task - should NOT be analyzed
        public class HelperNotTask
        {
            public void DoWork()
            {
                // ✅ No diagnostics - not a task
                Console.WriteLine("not a task");
                File.Exists("whatever");
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TASK 29: Explicit interface implementation
    // ─────────────────────────────────────────────────────────────────────────
    public class ExplicitInterfaceTask : ITask, IMultiThreadableTask
    {
        public IBuildEngine BuildEngine { get; set; }
        public ITaskHost HostObject { get; set; }
        public TaskEnvironment TaskEnvironment { get; set; }

        bool ITask.Execute()
        {
            // ❌ MSBuildTask0001: Console in explicit impl
            Console.WriteLine("explicit");

            // ❌ MSBuildTask0003: File API
            File.Exists("test.txt");

            return true;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TASK 30: Conditional compilation
    // Diagnostics should fire in both branches
    // ─────────────────────────────────────────────────────────────────────────
    public class ConditionalCompilationTask : Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; }
        public string InputFile { get; set; }

        public override bool Execute()
        {
#if DEBUG
            // ❌ MSBuildTask0001: Console even in DEBUG
            Console.WriteLine("debug mode");
#else
            // ❌ MSBuildTask0001: Console in release too
            Console.WriteLine("release mode");
#endif

            // ❌ MSBuildTask0003: File API
            File.Exists(InputFile);

            return true;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TASK 31: DirectoryInfo.FullName safe pattern
    // ─────────────────────────────────────────────────────────────────────────
    public class DirectoryInfoFullNameTask : Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; }
        public string Dir { get; set; }

        public override bool Execute()
        {
            AbsolutePath absDir = TaskEnvironment.GetAbsolutePath(Dir);
            var di = new DirectoryInfo(absDir);

            // ✅ Safe: DirectoryInfo.FullName (declared on FileSystemInfo)
            Directory.Exists(di.FullName);

            // ✅ Safe: FileInfo.FullName
            var fi = new FileInfo(absDir);
            File.Exists(fi.FullName);

            return true;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TASK 32: String interpolation with Environment variables
    // ─────────────────────────────────────────────────────────────────────────
    public class StringInterpolationTask : Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; }

        public override bool Execute()
        {
            // ❌ MSBuildTask0002: Environment.GetEnvironmentVariable in interpolation
            string msg = $"Home is {Environment.GetEnvironmentVariable("HOME")}";

            // ❌ MSBuildTask0002: Environment.CurrentDirectory in interpolation
            Log.LogMessage($"CWD: {Environment.CurrentDirectory}");

            return true;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TASK 33: Task with async pattern (Task-returning Execute override)
    // Note: MSBuild ITask.Execute() returns bool, but tasks may have async helpers
    // ─────────────────────────────────────────────────────────────────────────
    public class AsyncHelperTask : Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; }
        public string InputFile { get; set; }

        public override bool Execute()
        {
            return DoWorkAsync().GetAwaiter().GetResult();
        }

        private async System.Threading.Tasks.Task<bool> DoWorkAsync()
        {
            // ❌ MSBuildTask0003: File API in async method
            if (File.Exists(InputFile))
            {
                // ❌ MSBuildTask0003: StreamReader in async method
                using var reader = new StreamReader(InputFile);
                string content = await reader.ReadToEndAsync();
            }

            // ❌ MSBuildTask0001: Console in async method
            Console.WriteLine("async work done");

            return true;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TASK 34: Event handler patterns
    // ─────────────────────────────────────────────────────────────────────────
    public class EventHandlerTask : Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; }
        public string WatchDir { get; set; }

        public override bool Execute()
        {
            // ❌ MSBuildTask0003: FileSystemWatcher path
            using var watcher = new FileSystemWatcher(WatchDir);

            // Event handler with banned API
            watcher.Changed += (s, e) =>
            {
                // ❌ MSBuildTask0001: Console in event handler
                Console.WriteLine($"Changed: {e.FullPath}");
            };

            return true;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TASK 35: ITaskItem.GetMetadataValue("FullPath") safe pattern
    // ─────────────────────────────────────────────────────────────────────────
    public class GetMetadataValueTask : Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; }

        [Required]
        public ITaskItem[] SourceFiles { get; set; }

        public override bool Execute()
        {
            foreach (ITaskItem item in SourceFiles)
            {
                // ✅ GetMetadata("FullPath") is safe - just like GetMetadata
                string fullPath = item.GetMetadata("FullPath");

                // But this assignment loses the safe origin (no data flow tracking):
                // ❌ MSBuildTask0003: known limitation - variable lost safe origin
                File.Exists(fullPath);
            }

            return true;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TASK 36: Method group / delegate reference
    // ─────────────────────────────────────────────────────────────────────────
    public class MethodGroupTask : Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; }
        public ITaskItem[] Items { get; set; }

        public override bool Execute()
        {
            // ❌ MSBuildTask0003: File.Exists used as method group
            var results = Array.ConvertAll(
                Array.ConvertAll(Items, i => i.ItemSpec),
                File.Exists);

            return true;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TASK 37: Multiple IMultiThreadableTask derivation chains
    // Base class has TaskEnvironment, derived should still be analyzed
    // ─────────────────────────────────────────────────────────────────────────
    public abstract class BaseMultiThreadableTask : Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; }

        protected void LogSafe(string message) => Log.LogMessage(message);
    }

    public class DerivedMultiThreadableTask : BaseMultiThreadableTask
    {
        public string FilePath { get; set; }

        public override bool Execute()
        {
            // ❌ MSBuildTask0003: derived class still analyzed
            File.ReadAllText(FilePath);

            // ❌ MSBuildTask0001: Console in derived class
            Console.WriteLine("derived");

            // ✅ Safe: using base class helper
            LogSafe("All good");

            return true;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TASK 38: Static method with file operations (inside task class)
    // Static methods in task class should be analyzed
    // ─────────────────────────────────────────────────────────────────────────
    public class StaticMethodTask : Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; }
        public string InputFile { get; set; }

        public override bool Execute()
        {
            string content = ReadFileUnsafely(InputFile);
            return content != null;
        }

        // Static method - still within task class scope
        private static string ReadFileUnsafely(string path)
        {
            // ❌ MSBuildTask0003: static method in task class
            return File.ReadAllText(path);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TASK 39: Multiple unsafe APIs on same line
    // ─────────────────────────────────────────────────────────────────────────
    public class MultipleOnSameLineTask : Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; }
        public string Dir { get; set; }

        public override bool Execute()
        {
            // ❌ MSBuildTask0003 + MSBuildTask0003: Two file ops on same expression
            File.Copy(Dir, Path.Combine(Dir, "backup"));

            // ❌ MSBuildTask0002: Environment.GetEnvironmentVariable nested in another call
            Log.LogMessage(Environment.GetEnvironmentVariable("BUILD_ID"));

            return true;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TASK 40: Ternary / conditional expression with unsafe APIs
    // ─────────────────────────────────────────────────────────────────────────
    public class TernaryTask : Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; }
        public string InputFile { get; set; }
        public bool UseDefault { get; set; }

        public override bool Execute()
        {
            // ❌ MSBuildTask0003: File.Exists in condition
            string content = File.Exists(InputFile) ? File.ReadAllText(InputFile) : "";

            // ❌ MSBuildTask0002: Environment.GetEnvironmentVariable in ternary
            string val = UseDefault ? "default" : Environment.GetEnvironmentVariable("X");

            return true;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TASK 41: try/catch/finally with unsafe APIs in each block
    // ─────────────────────────────────────────────────────────────────────────
    public class TryCatchFinallyTask : Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; }
        public string InputFile { get; set; }

        public override bool Execute()
        {
            try
            {
                // ❌ MSBuildTask0003: in try block
                File.ReadAllText(InputFile);
            }
            catch (Exception)
            {
                // ❌ MSBuildTask0001: Console in catch block
                Console.Error.WriteLine("error occurred");
            }
            finally
            {
                // ❌ MSBuildTask0003: in finally block
                File.Delete(InputFile);
            }

            return true;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TASK 42: Switch expression and switch statement with unsafe APIs
    // ─────────────────────────────────────────────────────────────────────────
    public class SwitchTask : Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; }
        public string InputFile { get; set; }
        public string Mode { get; set; }

        public override bool Execute()
        {
            // ❌ MSBuildTask0003: File API in switch arms
            switch (Mode)
            {
                case "read":
                    File.ReadAllText(InputFile);
                    break;
                case "write":
                    File.WriteAllText(InputFile, "data");
                    break;
                case "delete":
                    File.Delete(InputFile);
                    break;
            }

            return true;
        }
    }
}
