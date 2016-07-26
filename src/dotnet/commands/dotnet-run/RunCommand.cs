// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.InternalAbstractions;
using Microsoft.DotNet.ProjectModel;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Tools.Run
{
    public partial class RunCommand
    {
        public string Framework = null;
        public string Configuration = null;
        public string Project = null;
        public IReadOnlyList<string> Args = null;

        private readonly ICommandFactory _commandFactory;
        private ProjectContext _context;
        private List<string> _args;
        private BuildWorkspace _workspace;

        public static readonly string[] DefaultFrameworks = new[]
        {
            FrameworkConstants.FrameworkIdentifiers.NetCoreApp,
            FrameworkConstants.FrameworkIdentifiers.NetStandardApp,
        };

        public RunCommand()
            : this(new RunCommandFactory())
        {
        }

        public RunCommand(ICommandFactory commandFactory)
        {
            _commandFactory = commandFactory;
        }

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

            var frameworkContexts = _workspace.GetProjectContextCollection(Project)
                .EnsureValid(Project)
                .FrameworkOnlyContexts;

            var rids = RuntimeEnvironmentRidExtensions.GetAllCandidateRuntimeIdentifiers();

            ProjectContext frameworkContext;
            if (Framework == null)
            {
                if (frameworkContexts.Count() == 1)
                {
                    frameworkContext = frameworkContexts.Single();
                }
                else
                {
                    frameworkContext = frameworkContexts.FirstOrDefault(c => DefaultFrameworks.Contains(c.TargetFramework.Framework));
                }
            }
            else
            {
                frameworkContext = frameworkContexts.FirstOrDefault(c => Equals(c.TargetFramework, NuGetFramework.Parse(Framework)));
            }

            if (frameworkContext == null)
            {
                throw new InvalidOperationException($"Couldn't find target to run. Possible causes:" + Environment.NewLine +
                    "1. No project.lock.json file or restore failed - run `dotnet restore`" + Environment.NewLine +
                    Framework == null ?
                        $"2. project.lock.json has multiple targets none of which is in default list ({string.Join(", ", DefaultFrameworks)})" :
                        $"2. The project does not support the desired framework: {Framework}");
            }

            _context = _workspace.GetRuntimeContext(frameworkContext, rids);

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
            // Set up the workspace
            _workspace = new BuildWorkspace(ProjectReaderSettings.ReadFromEnvironment());

            CalculateDefaultsForNonAssigned();

            // Compile to that directory
            var result = Build.BuildCommand.Run(new[]
            {
                $"--framework",
                $"{_context.TargetFramework}",
                $"--configuration",
                Configuration,
                $"{_context.ProjectFile.ProjectDirectory}"
            }, _workspace);

            if (result != 0)
            {
                return result;
            }

            List<string> hostArgs = new List<string>();
            if (!_context.TargetFramework.IsDesktop() && _context.LockFile != null)
            {
                // Add Nuget Packages Probing Paths
                const string probingPathArg = "--additionalprobingpath";

                foreach (var packageFolder in _context.LockFile.PackageFolders)
                {
                    hostArgs.Insert(0, packageFolder.Path);
                    hostArgs.Insert(0, probingPathArg);
                }
            }

            // Now launch the output and give it the results
            var outputPaths = _context.GetOutputPaths(Configuration);
            var outputName = outputPaths.RuntimeFiles.Executable;

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

            ICommand command;
            if (outputName.EndsWith(FileNameSuffixes.DotNet.DynamicLib, StringComparison.OrdinalIgnoreCase))
            {
                // The executable is a ".dll", we need to call it through dotnet.exe
                var muxer = new Muxer();

                command = _commandFactory.Create(muxer.MuxerPath, Enumerable.Concat(
                            Enumerable.Concat(new string[] { "exec" }, hostArgs),
                            Enumerable.Concat(new string[] { outputName }, _args)));
            }
            else
            {
                command = _commandFactory.Create(outputName, Enumerable.Concat(hostArgs, _args));
            }

            result = command
                .Execute()
                .ExitCode;

            return result;
        }

        private static int RunInteractive(string scriptName)
        {
            var command = Command.CreateDotNet($"repl-csi", new[] { scriptName })
                .ForwardStdOut()
                .ForwardStdErr();
            var result = command.Execute();
            return result.ExitCode;
        }

        private class RunCommandFactory : ICommandFactory
        {
            public ICommand Create(string commandName, IEnumerable<string> args, NuGetFramework framework = null, string configuration = "Debug")
            {
                return Command.Create(commandName, args, framework, configuration);
            }
        }
    }
}
