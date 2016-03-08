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
    public class AppBaseCommandResolver : AbstractPathBasedCommandResolver
    {
        public AppBaseCommandResolver(IEnvironmentProvider environment, 
            IPlatformCommandSpecFactory commandSpecFactory) : base(environment, commandSpecFactory) { }

        internal override string ResolveCommandPath(CommandResolverArguments commandResolverArguments)
        {
            return _environment.GetCommandPathFromRootPath(
                PlatformServices.Default.Application.ApplicationBasePath, 
                commandResolverArguments.CommandName);
        }

        internal override CommandResolutionStrategy GetCommandResolutionStrategy()
        {
            return CommandResolutionStrategy.BaseDirectory;
        }
    }
}
