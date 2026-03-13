// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// ═══════════════════════════════════════════════════════════════════════════════
// ADVANCED EDGE CASES - Part 2: Partial class Part B, Cross-file analysis
// ═══════════════════════════════════════════════════════════════════════════════

#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Task = Microsoft.Build.Utilities.Task;

namespace AnalyzerDemo
{
    // ─────────────────────────────────────────────────────────────────────────
    // TASK 23 (Part B): Partial class - second file
    // This part of the partial class should also be analyzed because
    // the combined type implements IMultiThreadableTask (declared in Part A)
    // ─────────────────────────────────────────────────────────────────────────
    public partial class PartialTask
    {
        private void DoPartBWork()
        {
            // ❌ MSBuildTask0003: File API in second file of partial class
            File.ReadAllText(InputFile);

            // ❌ MSBuildTask0002: Environment.GetEnvironmentVariable in Part B
            string val = Environment.GetEnvironmentVariable("TEST_VAR");

            // ❌ MSBuildTask0001: Console in Part B
            Console.WriteLine("Part B");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Non-task helper class - should NOT be analyzed
    // (unless [MSBuildMultiThreadableTaskAnalyzed] opt-in is added - future)
    // ─────────────────────────────────────────────────────────────────────────
    public static class UnsafeHelper
    {
        // ✅ No diagnostics - not a task class
        public static string ReadFile(string path)
        {
            Console.WriteLine("helper reading");  // No diagnostic
            return File.ReadAllText(path);         // No diagnostic
        }

        public static string GetEnvVar(string name)
        {
            return Environment.GetEnvironmentVariable(name);  // No diagnostic
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TASK 43: Task that calls the unsafe helper
    // The call TO the helper is fine, but the helper's internal usage
    // is not tracked (no cross-method data flow). This is a known limitation.
    // ─────────────────────────────────────────────────────────────────────────
    public class CallsHelperTask : Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; }
        public string InputFile { get; set; }

        public override bool Execute()
        {
            // ✅ No diagnostic on this line - the analyzer doesn't track
            // into external method bodies. Known limitation.
            string content = UnsafeHelper.ReadFile(InputFile);

            // ❌ MSBuildTask0003: Direct file API usage is still caught
            File.Exists(InputFile);

            return content != null;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TASK 44: Task with IDisposable pattern and using statements
    // ─────────────────────────────────────────────────────────────────────────
    public class DisposablePatternTask : Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; }
        public string InputFile { get; set; }
        public string OutputFile { get; set; }

        public override bool Execute()
        {
            // ❌ MSBuildTask0003: using statement with FileStream
            using (var input = new FileStream(InputFile, FileMode.Open))
            using (var output = new FileStream(OutputFile, FileMode.Create))
            {
                input.CopyTo(output);
            }

            // ❌ MSBuildTask0003: using declaration with StreamReader
            using var reader = new StreamReader(InputFile);
            string line = reader.ReadLine();

            return true;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TASK 45: Object initializer and collection initializer patterns
    // ─────────────────────────────────────────────────────────────────────────
    public class ObjectInitializerTask : Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; }

        public override bool Execute()
        {
            // ❌ MSBuildTask0002: ProcessStartInfo in object initializer
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "tool.exe",
                UseShellExecute = false,
            };

            // ❌ MSBuildTask0002: Environment variable in initializer value
            var config = new Dictionary<string, string>
            {
                ["home"] = Environment.GetEnvironmentVariable("HOME"),
                ["path"] = Environment.GetEnvironmentVariable("PATH"),
            };

            return true;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TASK 46: Null-conditional and null-coalescing with unsafe APIs
    // ─────────────────────────────────────────────────────────────────────────
    public class NullConditionalTask : Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; }
        public string InputFile { get; set; }

        public override bool Execute()
        {
            // ❌ MSBuildTask0002: Environment.GetEnvironmentVariable in null coalescing
            string home = Environment.GetEnvironmentVariable("HOME") ?? "/tmp";

            // ❌ MSBuildTask0003: File operations with null-conditional
            string content = File.Exists(InputFile) ? File.ReadAllText(InputFile) : null;

            return true;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TASK 47: Pattern matching with unsafe APIs
    // ─────────────────────────────────────────────────────────────────────────
    public class PatternMatchTask : Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; }
        public ITaskItem Input { get; set; }

        public override bool Execute()
        {
            // ❌ MSBuildTask0003: File API in pattern matching expression
            if (File.Exists(Input.ItemSpec) is true)
            {
                // ❌ MSBuildTask0003
                var attrs = File.GetAttributes(Input.ItemSpec);
                if (attrs is FileAttributes.Directory)
                {
                    // ❌ MSBuildTask0003
                    Directory.Delete(Input.ItemSpec, true);
                }
            }

            return true;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TASK 48: Correctly migrated complex task - ZERO diagnostics expected
    // Demonstrates proper migration patterns for all scenarios
    // ─────────────────────────────────────────────────────────────────────────
    public class CorrectlyMigratedComplexTask : Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; }
        public ITaskItem[] SourceFiles { get; set; }
        public ITaskItem DestinationFolder { get; set; }

        public override bool Execute()
        {
            bool success = true;

            // ✅ Using TaskEnvironment for environment
            string projDir = TaskEnvironment.ProjectDirectory;
            string buildId = TaskEnvironment.GetEnvironmentVariable("BUILD_ID");

            // ✅ Using GetAbsolutePath for destination
            AbsolutePath destDir = TaskEnvironment.GetAbsolutePath(DestinationFolder.ItemSpec);
            Directory.CreateDirectory(destDir);

            foreach (ITaskItem item in SourceFiles)
            {
                try
                {
                    // ✅ Using GetAbsolutePath for source
                    AbsolutePath source = TaskEnvironment.GetAbsolutePath(item.ItemSpec);

                    // ✅ GetMetadata("FullPath") is safe
                    string fullPath = item.GetMetadata("FullPath");

                    // ✅ AbsolutePath combination
                    AbsolutePath dest = TaskEnvironment.GetAbsolutePath(
                        Path.Combine(destDir, Path.GetFileName(source)));

                    if (File.Exists(source))
                    {
                        File.Copy(source, dest, overwrite: true);
                        Log.LogMessage($"Copied {item.ItemSpec} to {dest}");
                    }
                }
                catch (Exception ex)
                {
                    Log.LogError($"Failed to process {item.ItemSpec}: {ex.Message}");
                    success = false;
                }
            }

            // ✅ Using TaskEnvironment for process start
            var psi = TaskEnvironment.GetProcessStartInfo();
            psi.FileName = "post-process.exe";

            return success;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TASK 49: Task with field initialization containing unsafe APIs
    // ─────────────────────────────────────────────────────────────────────────
    public class FieldInitTask : Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; }

        // ❌ MSBuildTask0002: Field initializer
        private string _defaultDir = Environment.CurrentDirectory;

        // ❌ MSBuildTask0002: Field initializer
        private string _home = Environment.GetEnvironmentVariable("HOME");

        public override bool Execute()
        {
            Log.LogMessage($"Dir: {_defaultDir}, Home: {_home}");
            return true;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TASK 50: Task with constructor containing unsafe APIs
    // ─────────────────────────────────────────────────────────────────────────
    public class ConstructorUnsafeTask : Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; }
        private readonly string _cachedDir;

        public ConstructorUnsafeTask()
        {
            // ❌ MSBuildTask0002: Environment.CurrentDirectory in constructor
            _cachedDir = Environment.CurrentDirectory;

            // ❌ MSBuildTask0001: Console in constructor
            Console.WriteLine("task created");
        }

        public override bool Execute()
        {
            Log.LogMessage(_cachedDir);
            return true;
        }
    }
}
