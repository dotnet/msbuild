// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.DotNet.ProjectModel;

namespace Microsoft.DotNet.Cli.Utils.CommandResolution
{
    public class PublishPathCommandSpecFactory : IPublishedPathCommandSpecFactory
    {
        public CommandSpec CreateCommandSpecFromPublishFolder(
            string commandPath,
            IEnumerable<string> commandArguments,
            CommandResolutionStrategy commandResolutionStrategy,
            string depsFilePath,
            string runtimeConfigPath)
        {
            return CreateCommandSpecWrappingWithMuxerIfDll(
                commandPath,
                commandArguments,
                depsFilePath,
                commandResolutionStrategy,
                runtimeConfigPath);
        }

        private CommandSpec CreateCommandSpecWrappingWithMuxerIfDll(
            string commandPath,
            IEnumerable<string> commandArguments,
            string depsFilePath,
            CommandResolutionStrategy commandResolutionStrategy,
            string runtimeConfigPath)
        {
            var commandExtension = Path.GetExtension(commandPath);

            if (commandExtension == FileNameSuffixes.DotNet.DynamicLib)
            {
                return CreatePackageCommandSpecUsingMuxer(
                    commandPath,
                    commandArguments,
                    depsFilePath,
                    commandResolutionStrategy,
                    runtimeConfigPath);
            }

            return CreateCommandSpec(commandPath, commandArguments, commandResolutionStrategy);
        }
        private CommandSpec CreatePackageCommandSpecUsingMuxer(
            string commandPath,
            IEnumerable<string> commandArguments,
            string depsFilePath,
            CommandResolutionStrategy commandResolutionStrategy,
            string runtimeConfigPath)
        {
            var arguments = new List<string>();

            var muxer = new Muxer();

            var host = muxer.MuxerPath;
            if (host == null)
            {
                throw new Exception("Unable to locate dotnet multiplexer");
            }

            arguments.Add("exec");

            if (runtimeConfigPath != null)
            {
                arguments.Add("--runtimeconfig");
                arguments.Add(runtimeConfigPath);
            }

            if (depsFilePath != null)
            {
                arguments.Add("--depsfile");
                arguments.Add(depsFilePath);
            }

            arguments.Add(commandPath);
            arguments.AddRange(commandArguments);

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
