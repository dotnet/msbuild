using System.Collections.Generic;
using System.IO;
using Microsoft.DotNet.Cli.Utils;
using NuGet.Frameworks;

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
            var projectFactory = new ProjectFactory(_environment);
			
            var project = projectFactory.GetProject(
                projectDirectory,
                framework,
                configuration,
                buildBasePath,
                outputPath);

            if (project == null)
            {
                return null;
            }

            var buildOutputPath = project.FullOutputPath;

            if (!Directory.Exists(buildOutputPath))
            {
                Reporter.Verbose.WriteLine($"outputpathresolver: {buildOutputPath} does not exist");
                return null;
            }

            return _environment.GetCommandPathFromRootPath(buildOutputPath, commandName);
        }
        
        internal override CommandResolutionStrategy GetCommandResolutionStrategy()
        {
            return CommandResolutionStrategy.OutputPath;
        }
    }
}
