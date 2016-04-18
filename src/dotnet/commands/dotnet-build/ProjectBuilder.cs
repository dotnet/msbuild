using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.ProjectModel;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Tools.Build
{
    internal enum CompilationResult
    {
        IncrementalSkip, Success, Failure
    }

    internal abstract class ProjectBuilder
    {
        private readonly bool _skipDependencies;

        public ProjectBuilder(bool skipDependencies)
        {
            _skipDependencies = skipDependencies;
        }

        private Dictionary<ProjectContextIdentity, CompilationResult> _compilationResults = new Dictionary<ProjectContextIdentity, CompilationResult>();

        public IEnumerable<CompilationResult> Build(IEnumerable<ProjectGraphNode> roots)
        {
            foreach (var projectNode in roots)
            {
                Console.WriteLine(projectNode.ProjectContext.Identity.TargetFramework);
                yield return Build(projectNode);
            }
        }

        protected CompilationResult? GetCompilationResult(ProjectGraphNode projectNode)
        {
            CompilationResult result;
            if (_compilationResults.TryGetValue(projectNode.ProjectContext.Identity, out result))
            {
                return result;
            }
            return null;
        }

        protected virtual bool NeedsRebuilding(ProjectGraphNode projectNode)
        {
            return true;
        }

        protected virtual void ProjectSkiped(ProjectGraphNode projectNode)
        {
        }
        protected abstract CompilationResult RunCompile(ProjectGraphNode projectNode);

        private CompilationResult Build(ProjectGraphNode projectNode)
        {
            CompilationResult result;
            if (_compilationResults.TryGetValue(projectNode.ProjectContext.Identity, out result))
            {
                return result;
            }
            result = CompileWithDependencies(projectNode);

            _compilationResults[projectNode.ProjectContext.Identity] = result;

            return result;
        }

        private CompilationResult CompileWithDependencies(ProjectGraphNode projectNode)
        {
            if (!_skipDependencies)
            {
                foreach (var dependency in projectNode.Dependencies)
                {
                    var context = dependency.ProjectContext;
                    if (!context.ProjectFile.Files.SourceFiles.Any())
                    {
                        continue;
                    }
                    var result = Build(dependency);
                    if (result == CompilationResult.Failure)
                    {
                        return CompilationResult.Failure;
                    }
                }
            }
            if (NeedsRebuilding(projectNode))
            {
                return RunCompile(projectNode);
            }
            else
            {
                ProjectSkiped(projectNode);
                return CompilationResult.IncrementalSkip;
            }
        }
    }
}