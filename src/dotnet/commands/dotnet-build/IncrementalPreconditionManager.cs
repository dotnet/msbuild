using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.Tools.Compiler;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Compiler.Common;
using Microsoft.DotNet.ProjectModel.Utilities;

namespace Microsoft.DotNet.Tools.Build
{
    class IncrementalPreconditionManager
    {
        private readonly bool _printPreconditions;
        private readonly bool _forceNonIncremental;
        private readonly bool _skipDependencies;
        private Dictionary<ProjectContextIdentity, IncrementalPreconditions> _preconditions;

        public IncrementalPreconditionManager(bool printPreconditions, bool forceNonIncremental, bool skipDependencies)
        {
            _printPreconditions = printPreconditions;
            _forceNonIncremental = forceNonIncremental;
            _skipDependencies = skipDependencies;
            _preconditions = new Dictionary<ProjectContextIdentity, IncrementalPreconditions>();
        }

        public static readonly string[] KnownCompilers = { "csc", "vbc", "fsc" };

        public IncrementalPreconditions GetIncrementalPreconditions(ProjectGraphNode projectNode)
        {
            IncrementalPreconditions preconditions;
            if (_preconditions.TryGetValue(projectNode.ProjectContext.Identity, out preconditions))
            {
                return preconditions;
            }

            preconditions = new IncrementalPreconditions(_printPreconditions);

            if (_forceNonIncremental)
            {
                preconditions.AddForceUnsafePrecondition();
            }

            var projectsToCheck = GetProjectsToCheck(projectNode);

            foreach (var project in projectsToCheck)
            {
                CollectScriptPreconditions(project, preconditions);
                CollectCompilerNamePreconditions(project, preconditions);
                CollectCheckPathProbingPreconditions(project, preconditions);
            }
            _preconditions[projectNode.ProjectContext.Identity] = preconditions;
            return preconditions;
        }

        private List<ProjectContext> GetProjectsToCheck(ProjectGraphNode projectNode)
        {
            if (_skipDependencies)
            {
                return new List<ProjectContext>(1) { projectNode.ProjectContext };
            }

            // include initial root project
            var contextsToCheck = new List<ProjectContext>(1 + projectNode.Dependencies.Count) { projectNode.ProjectContext };

            // TODO: not traversing deeper than 1 level of dependencies
            contextsToCheck.AddRange(projectNode.Dependencies.Select(n => n.ProjectContext));

            return contextsToCheck;
        }

        private void CollectCheckPathProbingPreconditions(ProjectContext project, IncrementalPreconditions preconditions)
        {
            var pathCommands = CompilerUtil.GetCommandsInvokedByCompile(project)
                .Select(commandName => Command.CreateDotNet(commandName, Enumerable.Empty<string>(), project.TargetFramework))
                .Where(c => c.ResolutionStrategy.Equals(CommandResolutionStrategy.Path));

            foreach (var pathCommand in pathCommands)
            {
                preconditions.AddPathProbingPrecondition(project.ProjectName(), pathCommand.CommandName);
            }
        }

        private void CollectCompilerNamePreconditions(ProjectContext project, IncrementalPreconditions preconditions)
        {
            if (project.ProjectFile != null)
            {
                var projectCompiler = project.ProjectFile.CompilerName;

                if (!KnownCompilers.Any(knownCompiler => knownCompiler.Equals(projectCompiler, StringComparison.Ordinal)))
                {
                    preconditions.AddUnknownCompilerPrecondition(project.ProjectName(), projectCompiler);
                }
            }
        }

        private void CollectScriptPreconditions(ProjectContext project, IncrementalPreconditions preconditions)
        {
            if (project.ProjectFile != null)
            {
                var preCompileScripts = project.ProjectFile.Scripts.GetOrEmpty(ScriptNames.PreCompile);
                var postCompileScripts = project.ProjectFile.Scripts.GetOrEmpty(ScriptNames.PostCompile);

                if (preCompileScripts.Any())
                {
                    preconditions.AddPrePostScriptPrecondition(project.ProjectName(), ScriptNames.PreCompile);
                }

                if (postCompileScripts.Any())
                {
                    preconditions.AddPrePostScriptPrecondition(project.ProjectName(), ScriptNames.PostCompile);
                }
            }
        }

    }
}