// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using Microsoft.Dotnet.Cli.Compiler.Common;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.ProjectModel.Graph;
using NuGet.Frameworks;
using Microsoft.Extensions.PlatformAbstractions;

namespace Microsoft.DotNet.Tools.Restore
{
    public partial class RestoreCommand
    {
        private static readonly string DefaultRid = PlatformServices.Default.Runtime.GetLegacyRestoreRuntimeIdentifier();

        public static int Run(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            var app = new CommandLineApplication(false)
            {
                Name = "dotnet restore",
                FullName = ".NET project dependency restorer",
                Description = "Restores dependencies listed in project.json"
            };

            // Parse --quiet, because we have to handle that specially since NuGet3 has a different
            // "--verbosity" switch that goes BEFORE the command
            var quiet = args.Any(s => s.Equals("--quiet", StringComparison.OrdinalIgnoreCase));
            args = args.Where(s => !s.Equals("--quiet", StringComparison.OrdinalIgnoreCase)).ToArray();

            app.OnExecute(() =>
            {
                try
                {
                    var projectRestoreResult = NuGet3.Restore(args, quiet);

                    var restoreTasks = GetRestoreTasks(args);

                    foreach (var restoreTask in restoreTasks)
                    {
                        var project = ProjectReader.GetProject(restoreTask.ProjectPath);

                        RestoreTools(project, restoreTask, quiet);
                    }

                    return projectRestoreResult;
                }
                catch (InvalidOperationException e)
                {
                    Console.WriteLine(e.Message);

                    return -1;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);

                    return -2;
                }
            });

            return app.Execute(args);
        }

        private static IEnumerable<RestoreTask> GetRestoreTasks(IEnumerable<string> args)
        {
            var directory = Directory.GetCurrentDirectory();

            if (args.Any())
            {
                var firstArg = args.First();

                if (IsProjectFile(firstArg))
                {
                    return new [] {new RestoreTask { ProjectPath = firstArg, Arguments = args.Skip(1)} };
                }

                if (Directory.Exists(firstArg))
                {
                    directory = firstArg;

                    args = args.Skip(1);
                }
            }

            return GetAllProjectFiles(directory)
                .Select(p => new RestoreTask {ProjectPath = p, Arguments = args});
        }

        private static string[] GetAllProjectFiles(string directory)
        {
            return Directory.GetFiles(directory, Project.FileName, SearchOption.AllDirectories);
        }

        private static bool IsProjectFile(string firstArg)
        {
            return firstArg.EndsWith(Project.FileName) && File.Exists(firstArg);
        }

        private static void RestoreTools(Project project, RestoreTask restoreTask, bool quiet)
        {
            foreach (var tooldep in project.Tools)
            {
                RestoreTool(tooldep, restoreTask, quiet);
            }
        }

        private static void RestoreTool(LibraryRange tooldep, RestoreTask restoreTask, bool quiet)
        {
            var tempRoot = Path.Combine(restoreTask.ProjectDirectory, "obj");
            try
            {
                var tempPath = Path.Combine(tempRoot, Guid.NewGuid().ToString(), "bin");

                var restoreSucceded = RestoreToolToPath(tooldep, restoreTask.Arguments, tempPath, quiet);
                if (restoreSucceded)
                {
                    CreateDepsInPackageCache(tooldep, tempPath);
                    PersistLockFile(tooldep, tempPath, restoreTask.ProjectDirectory);
                }
            }
            finally
            {
                Directory.Delete(tempRoot, true);
            }
        }

        private static void PersistLockFile(LibraryRange tooldep, string tempPath, string projectPath)
        {
            var sourcePath = Path.Combine(tempPath, "project.lock.json");
            var targetDir = Path.Combine(projectPath, "artifacts", "Tools", tooldep.Name);
            var targetPath = Path.Combine(targetDir, "project.lock.json");

            if (Directory.Exists(targetDir)) Directory.Delete(targetDir, true);
            Directory.CreateDirectory(targetDir);

            Console.WriteLine($"Writing '{sourcePath}' to '{targetPath}'");

            File.Move(sourcePath, targetPath);
        }

        private static void CreateDepsInPackageCache(LibraryRange toolLibrary, string projectPath)
        {
            var context = ProjectContext.Create(projectPath,
                FrameworkConstants.CommonFrameworks.NetStandardApp15, new[] { DefaultRid });

            var toolDescription = context.LibraryManager.GetLibraries()
                .Select(l => l as PackageDescription)
                .Where(l => l != null)
                .FirstOrDefault(l => l.Identity.Name == toolLibrary.Name);

            var depsPath = Path.Combine(
                toolDescription.Path,
                Path.GetDirectoryName(toolDescription.RuntimeAssemblies.First().Path),
                toolDescription.Identity.Name + FileNameSuffixes.Deps);

            var depsJsonPath = Path.Combine(
                toolDescription.Path,
                Path.GetDirectoryName(toolDescription.RuntimeAssemblies.First().Path),
                toolDescription.Identity.Name + FileNameSuffixes.DepsJson);

            var calculator = context.GetOutputPaths(Constants.DefaultConfiguration, buidBasePath: null, outputPath: context.ProjectDirectory);
            var executable = new Executable(context, calculator, context.CreateExporter(Constants.DefaultConfiguration), null);

            executable.MakeCompilationOutputRunnable();

            if (File.Exists(depsPath)) File.Delete(depsPath);
            if (File.Exists(depsJsonPath)) File.Delete(depsJsonPath);

            File.Move(Path.Combine(calculator.RuntimeOutputPath, "bin" + FileNameSuffixes.Deps), depsPath);
            File.Move(Path.Combine(calculator.RuntimeOutputPath, "bin" + FileNameSuffixes.DepsJson), depsJsonPath);
        }

        private static bool RestoreToolToPath(LibraryRange tooldep, IEnumerable<string> args, string tempPath, bool quiet)
        {
            Directory.CreateDirectory(tempPath);
            var projectPath = Path.Combine(tempPath, Project.FileName);

            Console.WriteLine($"Restoring Tool '{tooldep.Name}' for '{projectPath}' in '{tempPath}'");

            File.WriteAllText(projectPath, GenerateProjectJsonContents(new[] {"netstandardapp1.5"}, tooldep));
            return NuGet3.Restore(new[] { $"{projectPath}" }.Concat(args), quiet) == 0;
        }

        private static string GenerateProjectJsonContents(IEnumerable<string> frameworks, LibraryRange tooldep)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("    \"dependencies\": {");
            sb.AppendLine($"        \"{tooldep.Name}\": \"{tooldep.VersionRange.OriginalString}\"");
            sb.AppendLine("    },");
            sb.AppendLine("    \"frameworks\": {");
            foreach (var framework in frameworks)
            {
                var importsStatement = "\"imports\": [ \"dnxcore50\", \"portable-net452+win81\" ]";
                sb.AppendLine($"        \"{framework}\": {{ {importsStatement} }}");
            }
            sb.AppendLine("    },");
            sb.AppendLine("    \"runtimes\": { ");
            sb.AppendLine($"        \"{DefaultRid}\": {{}}");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            var pjContents = sb.ToString();
            return pjContents;
        }
    }
}
