// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.Tools.MSBuild;
using Microsoft.DotNet.Tools.Restore;

namespace Microsoft.DotNet.Tools
{
    public class RestoringCommand : MSBuildForwardingApp
    {
        public RestoreCommand SeparateRestoreCommand { get; }

        public RestoringCommand(
            IEnumerable<string> msbuildArgs,
            IEnumerable<string> parsedArguments,
            IEnumerable<string> trailingArguments,
            bool noRestore,
            string msbuildPath = null)
            : base(GetCommandArguments(msbuildArgs, parsedArguments, noRestore), msbuildPath)
        {
            SeparateRestoreCommand = GetSeparateRestoreCommand(parsedArguments, trailingArguments, noRestore, msbuildPath);
        }

        private static IEnumerable<string> GetCommandArguments(
            IEnumerable<string> msbuildArgs,
            IEnumerable<string> parsedArguments,
            bool noRestore)
        {
            if (noRestore) 
            {
                return msbuildArgs;
            }

            if (HasArgumentToExcludeFromRestore(parsedArguments))
            {
                return Prepend("-nologo", msbuildArgs);
            }

            return Prepend("-restore", msbuildArgs);
        }

        private static RestoreCommand GetSeparateRestoreCommand(
            IEnumerable<string> parsedArguments,
            IEnumerable<string> trailingArguments, 
            bool noRestore,
            string msbuildPath)
        {
            if (noRestore || !HasArgumentToExcludeFromRestore(parsedArguments))
            {
                return null;
            }

            var restoreArguments = parsedArguments
                .Where(a => !IsExcludedFromRestore(a))
                .Concat(trailingArguments);

            return RestoreCommand.FromArgs(
                restoreArguments.ToArray(), 
                msbuildPath, 
                noLogo: false);
        }

        private static IEnumerable<string> Prepend(string argument, IEnumerable<string> arguments)
            => new[] { argument }.Concat(arguments);

        private static bool HasArgumentToExcludeFromRestore(IEnumerable<string> arguments)
            => arguments.Any(a => IsExcludedFromRestore(a));

        private static bool IsExcludedFromRestore(string argument) 
            => argument.StartsWith("-property:TargetFramework=", StringComparison.Ordinal);

        public override int Execute()
        {
            if (SeparateRestoreCommand != null)
            {
                int exitCode = SeparateRestoreCommand.Execute();
                if (exitCode != 0)
                {
                    return exitCode;
                }
            }

            return base.Execute();
        }
    }
}