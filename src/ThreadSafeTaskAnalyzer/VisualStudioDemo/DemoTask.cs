// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.IO;
using Microsoft.Build.Framework;

namespace VisualStudioDemo
{
    /// <summary>
    /// Demo task showing PROBLEMATIC code that triggers analyzer diagnostics across all 4 categories.
    /// 
    /// Try the code fixers:
    /// 1. Place cursor on MSB9997 or MSB9998 warnings
    /// 2. Press Ctrl+. to open Quick Actions
    /// 3. Apply the suggested fix
    /// 4. Watch the warning disappear!
    /// 
    /// Expected diagnostics:
    /// - 2 MSB9999 errors (Environment.Exit, ThreadPool.SetMaxThreads)
    /// - 3 MSB9998 warnings (Environment.CurrentDirectory, GetEnvironmentVariable, Path.GetFullPath) - Code fixers available!
    /// - 7 MSB9997 warnings (File/Directory APIs) - Code fixers available!
    /// - 1 MSB9996 warning (Console.WriteLine)
    /// </summary>
    public class ProblematicTask : IMultiThreadableTask
    {
        public IBuildEngine BuildEngine { get; set; }
        public ITaskHost HostObject { get; set; }
        public TaskEnvironment TaskEnvironment { get; set; }

        public string InputFile { get; set; }
        public string OutputDirectory { get; set; }

        public bool Execute()
        {
            // ❌ MSB9998: Environment.CurrentDirectory - Code fixer available!
            string currentDir = Environment.CurrentDirectory;

            // ❌ MSB9998: Environment.GetEnvironmentVariable - Code fixer available!
            string pathVar = Environment.GetEnvironmentVariable("PATH");

            // ❌ MSB9997: File.Exists with unwrapped path - Code fixer available!
            if (File.Exists(InputFile))
            {
                // ❌ MSB9997: Directory.Exists with unwrapped path
                if (!Directory.Exists(OutputDirectory))
                {
                    // ❌ MSB9997: Directory.CreateDirectory with unwrapped path
                    Directory.CreateDirectory(OutputDirectory);
                }

                // ❌ MSB9997: File.ReadAllText with unwrapped path
                string content = File.ReadAllText(InputFile);

                // ❌ MSB9998: Path.GetFullPath - Code fixer available!
                string fullPath = Path.GetFullPath(InputFile);

                // ❌ MSB9997: new FileInfo with unwrapped path
                var fileInfo = new FileInfo(InputFile);

                // ❌ MSB9997: new DirectoryInfo with unwrapped path
                var dirInfo = new DirectoryInfo(OutputDirectory);

                // ❌ MSB9997: new StreamReader with unwrapped path
                using (var reader = new StreamReader(InputFile))
                {
                    string line = reader.ReadLine();
                }

                // ❌ MSB9996: Console.WriteLine - warning only
                Console.WriteLine("Processing file: " + InputFile);

                // ❌ MSB9999: ThreadPool.SetMaxThreads - ERROR (no safe alternative)
                System.Threading.ThreadPool.SetMaxThreads(10, 10);

                // ❌ MSB9999: Environment.Exit - ERROR (terminates process)
                if (content.Length == 0)
                {
                    Environment.Exit(1);
                }

                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Example of CORRECT code - no MSB9999 warnings!
    /// All File/Directory operations use absolute paths via TaskEnvironment.GetAbsolutePath().
    /// This is what your code should look like after applying the code fixer.
    /// </summary>
    public class CorrectTask : IMultiThreadableTask
    {
        public IBuildEngine BuildEngine { get; set; }
        public ITaskHost HostObject { get; set; }
        public TaskEnvironment TaskEnvironment { get; set; }

        public string InputFile { get; set; }
        public string OutputDirectory { get; set; }

        public bool Execute()
        {
            // ✅ Wrapped with TaskEnvironment.GetAbsolutePath() - NO MSB9999 warning
            if (File.Exists(TaskEnvironment.GetAbsolutePath(InputFile)))
            {
                // ✅ Wrapped path - NO MSB9999 warning
                if (!Directory.Exists(TaskEnvironment.GetAbsolutePath(OutputDirectory)))
                {
                    // ✅ Wrapped path - NO MSB9999 warning
                    Directory.CreateDirectory(TaskEnvironment.GetAbsolutePath(OutputDirectory));
                }

                // ✅ Wrapped path - NO MSB9999 warning
                string content = File.ReadAllText(TaskEnvironment.GetAbsolutePath(InputFile));
                
                // ✅ Path.Combine result wrapped - NO MSB9999 warning
                var outputFile = TaskEnvironment.GetAbsolutePath(
                    Path.Combine(TaskEnvironment.GetAbsolutePath(OutputDirectory), "output.txt"));
                File.WriteAllText(outputFile, content);

                return true;
            }

            return false;
        }
    }
}
