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
    public class CompositeCommandResolver : ICommandResolver
    {
        private IList<ICommandResolver> _orderedCommandResolvers;

        public IEnumerable<ICommandResolver> OrderedCommandResolvers 
        {
            get 
            {
                return _orderedCommandResolvers;
            }
        }

        public CompositeCommandResolver()
        {
            _orderedCommandResolvers = new List<ICommandResolver>();
        }

        public void AddCommandResolver(ICommandResolver commandResolver)
        {
            _orderedCommandResolvers.Add(commandResolver);
        }
        
        public CommandSpec Resolve(CommandResolverArguments commandResolverArguments)
        {
            foreach (var commandResolver in _orderedCommandResolvers)
            {
                var commandSpec = commandResolver.Resolve(commandResolverArguments);

                if (commandSpec != null)
                {
                    return commandSpec;
                }
            }

            return null;
        }
    }
}