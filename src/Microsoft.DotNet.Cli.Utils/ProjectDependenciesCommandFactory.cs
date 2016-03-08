// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Cli.Utils
{
    public class ProjectDependenciesCommandFactory : ICommandFactory
    {
        private readonly NuGetFramework _nugetFramework;
        private readonly string _configuration;
        private readonly string _outputPath;
        private readonly string _projectDirectory;

        public ProjectDependenciesCommandFactory(
            NuGetFramework nugetFramework, 
            string configuration, 
            string outputPath,
            string projectDirectory)
        {
            _nugetFramework = nugetFramework;
            _configuration = configuration;
            _outputPath = outputPath;
            _projectDirectory = projectDirectory;
        }

        public ICommand Create(
            string commandName,
            IEnumerable<string> args,
            NuGetFramework framework = null,
            string configuration = Constants.DefaultConfiguration)
        {
            if (string.IsNullOrEmpty(configuration))
            {
                configuration = _configuration;
            }

            if (framework == null)
            {
                framework = _nugetFramework;
            }

            var commandSpec = FindProjectDependencyCommands(
                commandName, 
                args, 
                configuration, 
                framework, 
                _outputPath,
                _projectDirectory);

            return Command.Create(commandSpec);
        }

        private CommandSpec FindProjectDependencyCommands(
            string commandName,
            IEnumerable<string> commandArgs,
            string configuration,
            NuGetFramework framework,
            string outputPath,
            string projectDirectory)
        {
            var commandResolverArguments = new CommandResolverArguments
            {
                CommandName = commandName,
                CommandArguments = commandArgs,
                Framework = framework,
                Configuration = configuration,
                OutputPath = outputPath,
                ProjectDirectory = projectDirectory
            };

            var commandResolver = GetProjectDependenciesCommandResolver();

            var commandSpec = commandResolver.Resolve(commandResolverArguments);
            if (commandSpec == null)
            {
                throw new CommandUnknownException(commandName);
            }

            return commandSpec;
        }

        private ICommandResolver GetProjectDependenciesCommandResolver()
        {
            var environment = new EnvironmentProvider();
            var packagedCommandSpecFactory = new PackagedCommandSpecFactory();

            return new ProjectDependenciesCommandResolver(environment, packagedCommandSpecFactory);
        }
    }
}
