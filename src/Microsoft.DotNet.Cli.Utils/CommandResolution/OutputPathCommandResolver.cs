using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.ProjectModel.Graph;
using Microsoft.Extensions.PlatformAbstractions;
using NuGet.Frameworks;
using NuGet.Packaging;

namespace Microsoft.DotNet.Cli.Utils
{
    public class OutputPathCommandResolver : AbstractPathBasedCommandResolver
    {
        public OutputPathCommandResolver(IEnvironmentProvider environment,
            IPlatformCommandSpecFactory commandSpecFactory) : base(environment, commandSpecFactory)
        { }


        internal override string ResolveCommandPath(CommandResolverArguments commandResolverArguments)
        {
            if (commandResolverArguments.Framework == null
                || commandResolverArguments.ProjectDirectory == null
                || commandResolverArguments.Configuration == null
                || commandResolverArguments.CommandName == null)
            {
                return null;
            }

            return ResolveFromProjectOutput(
                commandResolverArguments.ProjectDirectory,
                commandResolverArguments.Framework,
                commandResolverArguments.Configuration,
                commandResolverArguments.CommandName,
                commandResolverArguments.CommandArguments.OrEmptyIfNull(),
                commandResolverArguments.OutputPath,
                commandResolverArguments.BuildBasePath);
        }

        private string ResolveFromProjectOutput(
            string projectDirectory,
            NuGetFramework framework,
            string configuration,
            string commandName,
            IEnumerable<string> commandArguments,
            string outputPath,
            string buildBasePath)
        {
            var projectContext = GetProjectContextFromDirectory(
                projectDirectory,
                framework);

            if (projectContext == null)
            {
                return null;
            }

            var buildOutputPath = projectContext.GetOutputPaths(configuration, buildBasePath, outputPath).RuntimeFiles.BasePath;

            return _environment.GetCommandPathFromRootPath(buildOutputPath, commandName);
        }

        private ProjectContext GetProjectContextFromDirectory(string directory, NuGetFramework framework)
        {
            if (directory == null || framework == null)
            {
                return null;
            }

            var projectRootPath = directory;

            if (!File.Exists(Path.Combine(projectRootPath, Project.FileName)))
            {
                return null;
            }

            var projectContext = ProjectContext.Create(
                projectRootPath,
                framework,
                PlatformServices.Default.Runtime.GetAllCandidateRuntimeIdentifiers());

            if (projectContext.RuntimeIdentifier == null)
            {
                return null;
            }

            return projectContext;
        }

        internal override CommandResolutionStrategy GetCommandResolutionStrategy()
        {
            return CommandResolutionStrategy.OutputPath;
        }
    }
}
