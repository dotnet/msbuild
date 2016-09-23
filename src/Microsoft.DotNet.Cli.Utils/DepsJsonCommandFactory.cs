// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Cli.Utils
{
    public class DepsJsonCommandFactory : ICommandFactory
    {
        private DepsJsonCommandResolver _depsJsonCommandResolver;
        private string _temporaryDirectory;
        private string _depsJsonFile;
        private string _runtimeConfigFile;

        public DepsJsonCommandFactory(
            string depsJsonFile, 
            string runtimeConfigFile,
            string nugetPackagesRoot,
            string temporaryDirectory)
        {
            _depsJsonCommandResolver = new DepsJsonCommandResolver(nugetPackagesRoot);

            _temporaryDirectory = temporaryDirectory;
            _depsJsonFile = depsJsonFile;
            _runtimeConfigFile = runtimeConfigFile;
        }

        public ICommand Create(
            string commandName,
            IEnumerable<string> args,
            NuGetFramework framework = null,
            string configuration = Constants.DefaultConfiguration)
        {
            var commandResolverArgs = new CommandResolverArguments()
            {
                CommandName = commandName,
                CommandArguments = args,
                DepsJsonFile = _depsJsonFile
            };

            var commandSpec = _depsJsonCommandResolver.Resolve(commandResolverArgs);

            return Command.Create(commandSpec);
        }
    }
}
