// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using Microsoft.DotNet.PlatformAbstractions;

namespace Microsoft.DotNet.Cli.Utils
{
    public class DotnetToolsCommandResolver : ICommandResolver
    {
        private string _dotnetToolPath;

        public DotnetToolsCommandResolver(string dotnetToolPath = null)
        {
            if (dotnetToolPath == null)
            {
                _dotnetToolPath = Path.Combine(ApplicationEnvironment.ApplicationBasePath,
                    "DotnetTools");
            }
            else
            {
                _dotnetToolPath = dotnetToolPath;
            }
        }

        public CommandSpec Resolve(CommandResolverArguments arguments)
        {
            if (string.IsNullOrEmpty(arguments.CommandName))
            {
                return null;
            }

            var packageId = new DirectoryInfo(Path.Combine(_dotnetToolPath, arguments.CommandName));
            if (!packageId.Exists)
            {
                return null;
            }

            var version = packageId.GetDirectories()[0];
            var dll = version.GetDirectories("tools")[0]
                .GetDirectories()[0] // TFM
                .GetDirectories()[0] // RID
                .GetFiles($"{arguments.CommandName}.dll")[0];

            return CreatePackageCommandSpecUsingMuxer(
                    dll.FullName,
                    arguments.CommandArguments,
                    CommandResolutionStrategy.DotnetToolsPackage);
        }

        private CommandSpec CreatePackageCommandSpecUsingMuxer(
            string commandPath,
            IEnumerable<string> commandArguments,
            CommandResolutionStrategy commandResolutionStrategy)
        {
            var arguments = new List<string>();

            var muxer = new Muxer();

            var host = muxer.MuxerPath;
            if (host == null)
            {
                throw new Exception(LocalizableStrings.UnableToLocateDotnetMultiplexer);
            }

            arguments.Add(commandPath);

            if (commandArguments != null)
            {
                arguments.AddRange(commandArguments);
            }

            return CreateCommandSpec(host, arguments, commandResolutionStrategy);
        }

        private CommandSpec CreateCommandSpec(
            string commandPath,
            IEnumerable<string> commandArguments,
            CommandResolutionStrategy commandResolutionStrategy)
        {
            var escapedArgs = ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(commandArguments);

            return new CommandSpec(commandPath, escapedArgs, commandResolutionStrategy);
        }
    }
}
