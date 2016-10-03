// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectModel;
using System.Linq;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Tools.Test
{
    public class ProjectJsonTestRunnerDecorator : IDotnetTestRunner
    {
        private readonly Func<ICommandFactory, string, NuGetFramework, IDotnetTestRunner> _nextRunner;
        private readonly TestProjectBuilder _testProjectBuilder;

        public ProjectJsonTestRunnerDecorator(
            Func<ICommandFactory, string, NuGetFramework, IDotnetTestRunner> nextRunner)
        {
            _nextRunner = nextRunner;
            _testProjectBuilder = new TestProjectBuilder();
        }

        public int RunTests(DotnetTestParams dotnetTestParams)
        {
            var projectPath = GetProjectPath(dotnetTestParams.ProjectOrAssemblyPath);
            var runtimeIdentifiers = !string.IsNullOrEmpty(dotnetTestParams.Runtime)
                ? new[] {dotnetTestParams.Runtime}
                : DotnetRuntimeIdentifiers.InferCurrentRuntimeIdentifiers();
            var exitCode = 0;

            // Create a workspace
            var workspace = new BuildWorkspace(ProjectReaderSettings.ReadFromEnvironment());

            if (dotnetTestParams.Framework != null)
            {
                var projectContext = workspace.GetProjectContext(projectPath, dotnetTestParams.Framework);
                if (projectContext == null)
                {
                    Reporter.Error.WriteLine(
                        $"Project '{projectPath}' does not support framework: {dotnetTestParams.UnparsedFramework}");
                    return 1;
                }
                projectContext = workspace.GetRuntimeContext(projectContext, runtimeIdentifiers);

                exitCode = RunTests(projectContext, dotnetTestParams);
            }
            else
            {
                var summary = new Summary();
                var projectContexts = workspace.GetProjectContextCollection(projectPath)
                    .EnsureValid(projectPath)
                    .FrameworkOnlyContexts
                    .Select(c => workspace.GetRuntimeContext(c, runtimeIdentifiers))
                    .ToList();

                // Execute for all TFMs the project targets.
                foreach (var projectContext in projectContexts)
                {
                    var result = RunTests(projectContext, dotnetTestParams);
                    if (result == 0)
                    {
                        summary.Passed++;
                    }
                    else
                    {
                        summary.Failed++;
                        if (exitCode == 0)
                        {
                            // If tests fail in more than one TFM, we'll have it use the result of the first one
                            // as the exit code.
                            exitCode = result;
                        }
                    }
                }

                summary.Print();
            }

            return exitCode;
        }

        private int RunTests(ProjectContext projectContext, DotnetTestParams dotnetTestParams)
        {
            var result = _testProjectBuilder.BuildTestProject(projectContext, dotnetTestParams);

            if (result == 0)
            {
                var commandFactory =
                    new ProjectDependenciesCommandFactory(
                        projectContext.TargetFramework,
                        dotnetTestParams.Config,
                        dotnetTestParams.Output,
                        dotnetTestParams.BuildBasePath,
                        projectContext.ProjectDirectory);

                var assemblyUnderTest = new AssemblyUnderTest(projectContext, dotnetTestParams);

                var framework = projectContext.TargetFramework;

                result = _nextRunner(commandFactory, assemblyUnderTest.Path, framework).RunTests(dotnetTestParams);
            }

            return result;
        }

        private static string GetProjectPath(string projectPath)
        {
            projectPath = projectPath ?? Directory.GetCurrentDirectory();

            if (!projectPath.EndsWith(Project.FileName))
            {
                projectPath = Path.Combine(projectPath, Project.FileName);
            }

            if (!File.Exists(projectPath))
            {
                throw new InvalidOperationException($"{projectPath} does not exist.");
            }

            return projectPath;
        }

        private class Summary
        {
            public int Passed { get; set; }

            public int Failed { get; set; }

            private int Total => Passed + Failed;

            public void Print()
            {
                var summaryMessage = $"SUMMARY: Total: {Total} targets, Passed: {Passed}, Failed: {Failed}.";
                if (Failed > 0)
                {
                    Reporter.Error.WriteLine(summaryMessage.Red());
                }
                else
                {
                    Reporter.Output.WriteLine(summaryMessage);
                }
            }
        }
    }
}