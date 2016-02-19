// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectModel;
using Microsoft.Extensions.PlatformAbstractions;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Tools.Run
{
    public partial class RunCommand
    {
        public string Framework = null;
        public string Configuration = null;
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

            var rids = PlatformServices.Default.Runtime.GetAllCandidateRuntimeIdentifiers();
            if (Framework == null)
            {
                var defaultFrameworks = new[]
                {
                    FrameworkConstants.FrameworkIdentifiers.DnxCore,
                    FrameworkConstants.FrameworkIdentifiers.NetStandardApp,
                };

                var contexts = ProjectContext.CreateContextForEachFramework(Project, null, rids);
                _context = contexts.FirstOrDefault(c =>
                    defaultFrameworks.Contains(c.TargetFramework.Framework) && !string.IsNullOrEmpty(c.RuntimeIdentifier));

                if (_context == null)
                {
                    throw new InvalidOperationException($"Couldn't find default target with framework: {string.Join(",", defaultFrameworks)}");
                }
            }
            else
            {
                _context = ProjectContext.Create(Project, NuGetFramework.Parse(Framework), rids);
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

            // Compile to that directory
            var result = Build.BuildCommand.Run(new[]
            {
                $"--framework",
                $"{_context.TargetFramework}",
                $"--configuration",
                Configuration,
                $"{_context.ProjectFile.ProjectDirectory}"
            });

            if (result != 0)
            {
                return result;
            }

            // Now launch the output and give it the results
            var outputName = _context.GetOutputPaths(Configuration).RuntimeFiles.Executable;

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (_context.TargetFramework.IsDesktop())
                {
                    // Run mono if we're running a desktop target on non windows
                    _args.Insert(0, outputName);

                    if (string.Equals(Configuration, "Debug", StringComparison.OrdinalIgnoreCase))
                    {
                        // If we're compiling for the debug configuration then add the --debug flag
                        // other options may be passed using the MONO_OPTIONS env var
                        _args.Insert(0, "--debug");
                    }

                    outputName = "mono";
                }
            }

            result = Command.Create(outputName, _args)
                .ForwardStdOut()
                .ForwardStdErr()
                .Execute()
                .ExitCode;

            return result;
        }

        private static int RunInteractive(string scriptName)
        {
            var command = Command.CreateDotNet($"repl-csi", new [] {scriptName})
                .ForwardStdOut()
                .ForwardStdErr();
            var result = command.Execute();
            return result.ExitCode;
        }
    }
}
