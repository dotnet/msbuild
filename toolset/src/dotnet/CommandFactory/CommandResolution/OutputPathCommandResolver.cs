// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using Microsoft.DotNet.Cli.Utils;
using NuGet.Frameworks;

namespace Microsoft.DotNet.CommandFactory
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
                Reporter.Verbose.WriteLine(
                    string.Format(LocalizableStrings.BuildOutputPathDoesNotExist, buildOutputPath));
                return null;
            }

            return _environment.GetCommandPathFromRootPath(buildOutputPath, commandName);
        }
    }
}
