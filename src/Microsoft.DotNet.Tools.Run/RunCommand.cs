// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectModel;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Tools.Run
{
    public class RunCommand
    {
        public string Framework = null;
        public string Configuration = null;
        public bool PreserveTemporary = false;
        public string Project = null;
        public IReadOnlyList<string> Args = null;

        ProjectContext _context;
        List<string> _args;

        public int Start()
        {
            if (IsInteractive())
            {
                return RunInteractive(Project);
            }
            else
            {
                return RunExecutable();
            }
        }

        private bool IsInteractive()
        {
            if (!string.IsNullOrEmpty(Project))
            {
                if (File.Exists(Project) && (Path.GetExtension(Project).ToLowerInvariant() == ".csx"))
                {
                    return true;
                }
            }

            return false;
        }

        private void CalculateDefaultsForNonAssigned()
        {
            if (string.IsNullOrWhiteSpace(Project))
            {
                Project = Directory.GetCurrentDirectory();
            }

            if (string.IsNullOrWhiteSpace(Configuration))
            {
                Configuration = Constants.DefaultConfiguration;
            }

            var contexts = ProjectContext.CreateContextForEachFramework(Project);
            if (Framework == null)
            {
                _context = contexts.First();
            }
            else
            {
                var fx = NuGetFramework.Parse(Framework);
                _context = contexts.FirstOrDefault(c => c.TargetFramework.Equals(fx));
            }

            if (Args == null)
            {
                _args = new List<string>();
            }
            else
            {
                _args = new List<string>(Args);
            }
        }

        private int RunExecutable()
        {
            CalculateDefaultsForNonAssigned();

            // Create a temporary directory
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

            // Compile to that directory
            var result = Command.Create($"dotnet-compile", $"--output \"{tempDir}\" --temp-output \"{tempDir}\" --framework \"{_context.TargetFramework}\" --configuration \"{Configuration}\" {_context.ProjectFile.ProjectDirectory}")
                .ForwardStdOut(onlyIfVerbose: true)
                .ForwardStdErr()
                .Execute();

            if (result.ExitCode != 0)
            {
                return result.ExitCode;
            }

            // Now launch the output and give it the results
            var outputName = Path.Combine(tempDir, _context.ProjectFile.Name + Constants.ExeSuffix);
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (_context.TargetFramework.IsDesktop())
                {
                    // Run mono if we're running a desktop target on non windows
                    _args.Insert(0, outputName + ".exe");

                    if (string.Equals(Configuration, "Debug", StringComparison.OrdinalIgnoreCase))
                    {
                        // If we're compiling for the debug configuration then add the --debug flag
                        // other options may be passed using the MONO_OPTIONS env var
                        _args.Insert(0, "--debug");
                    }

                    outputName = "mono";
                }
            }

            // Locate the runtime
            string runtime = Environment.GetEnvironmentVariable("DOTNET_HOME");
            if (string.IsNullOrEmpty(runtime))
            {
                // Use the runtime deployed with the tools, if present
                var candidate = Path.Combine(AppContext.BaseDirectory, "..", "runtime");
                if (File.Exists(Path.Combine(candidate, Constants.LibCoreClrName)))
                {
                    runtime = Path.GetFullPath(candidate);
                }
            }

            result = Command.Create(outputName, string.Join(" ", _args))
                .ForwardStdOut()
                .ForwardStdErr()
                .EnvironmentVariable("DOTNET_HOME", runtime)
                .Execute();

            // Clean up
            if (!PreserveTemporary)
            {
                Directory.Delete(tempDir, recursive: true);
            }

            return result.ExitCode;
        }

        private static int RunInteractive(string scriptName)
        {
            var command = Command.Create($"dotnet-repl-csi", scriptName)
                .ForwardStdOut()
                .ForwardStdErr();
            var result = command.Execute();
            return result.ExitCode;
        }
    }
}
