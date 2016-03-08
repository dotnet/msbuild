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
    public class ProjectPathCommandResolver : AbstractPathBasedCommandResolver
    {
        public ProjectPathCommandResolver(IEnvironmentProvider environment, 
            IPlatformCommandSpecFactory commandSpecFactory) : base(environment, commandSpecFactory) { }

        internal override string ResolveCommandPath(CommandResolverArguments commandResolverArguments)
        {
            if (commandResolverArguments.ProjectDirectory == null)
            {
                return null;
            }

            return _environment.GetCommandPathFromRootPath(
                commandResolverArguments.ProjectDirectory, 
                commandResolverArguments.CommandName,
                commandResolverArguments.InferredExtensions.OrEmptyIfNull());
        }

        internal override CommandResolutionStrategy GetCommandResolutionStrategy()
        {
            return CommandResolutionStrategy.ProjectLocal;
        }
    }
}
