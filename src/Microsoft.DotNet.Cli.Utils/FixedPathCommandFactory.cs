// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Cli.Utils
{
    public class FixedPathCommandFactory : ICommandFactory
    {
        private readonly NuGetFramework _nugetFramework;
        private readonly string _configuration;
        private readonly string _outputPath;

        public FixedPathCommandFactory(NuGetFramework nugetFramework, string configuration, string outputPath)
        {
            _nugetFramework = nugetFramework;
            _configuration = configuration;
            _outputPath = outputPath;
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

            return Command.Create(commandName, args, framework, configuration, _outputPath);
        }
    }
}
