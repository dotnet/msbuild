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
    public abstract class AbstractPathBasedCommandResolver : ICommandResolver
    {
        protected IEnvironmentProvider _environment;
        protected IPlatformCommandSpecFactory _commandSpecFactory;

        public AbstractPathBasedCommandResolver(IEnvironmentProvider environment, 
            IPlatformCommandSpecFactory commandSpecFactory)
        {
            if (environment == null)
            {
                throw new ArgumentNullException("environment");
            }

            if (commandSpecFactory == null)
            {
                throw new ArgumentNullException("commandSpecFactory");
            }

            _environment = environment;
            _commandSpecFactory = commandSpecFactory;
        }

        public CommandSpec Resolve(CommandResolverArguments commandResolverArguments)
        {
            if (commandResolverArguments.CommandName == null)
            {
                return null;
            }

            var commandPath = ResolveCommandPath(commandResolverArguments);

            if (commandPath == null)
            {
                return null;
            }

            return _commandSpecFactory.CreateCommandSpec(
                    commandResolverArguments.CommandName,
                    commandResolverArguments.CommandArguments.OrEmptyIfNull(),
                    commandPath,
                    GetCommandResolutionStrategy(),
                    _environment);
        }

        internal abstract string ResolveCommandPath(CommandResolverArguments commandResolverArguments);
        internal abstract CommandResolutionStrategy GetCommandResolutionStrategy();
    }
}
