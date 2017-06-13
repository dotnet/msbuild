// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.Tools.MSBuild;
using Microsoft.DotNet.Tools.Restore;

namespace Microsoft.DotNet.Tools
{
    public class RestoringCommand : MSBuildForwardingApp
    {
        private bool NoRestore { get; }

        private IEnumerable<string> ParsedArguments { get; }

        private IEnumerable<string> TrailingArguments { get; }

        private IEnumerable<string> ArgsToForwardToRestore()
        {
            var restoreArguments = ParsedArguments.Where(a =>
                !a.StartsWith("/p:TargetFramework"));

            if (!restoreArguments.Any(a => a.StartsWith("/verbosity:")))
            {
                restoreArguments = restoreArguments.Concat(new string[] { "/verbosity:q" });
            }

            return restoreArguments.Concat(TrailingArguments);
        }

        private bool ShouldRunImplicitRestore => !NoRestore;

        public RestoringCommand(
            IEnumerable<string> msbuildArgs,
            IEnumerable<string> parsedArguments,
            IEnumerable<string> trailingArguments,
            bool noRestore,
            string msbuildPath = null)
            : base(msbuildArgs, msbuildPath)
        {
            NoRestore = noRestore;
            ParsedArguments = parsedArguments;
            TrailingArguments = trailingArguments;
        }

        public override int Execute()
        {
            if (ShouldRunImplicitRestore)
            {
                int exitCode = RestoreCommand.Run(ArgsToForwardToRestore().ToArray());
                if (exitCode != 0)
                {
                    return exitCode;
                }
            }

            return base.Execute();
        }
    }
}