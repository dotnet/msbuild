// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.CommandFactory
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
