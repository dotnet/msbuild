// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.Tools.MSBuild;
using Microsoft.DotNet.Tools.Restore;
using Microsoft.DotNet.Workloads.Workload.Install;

namespace Microsoft.DotNet.Tools
{
    public class RestoringCommand : MSBuildForwardingApp
    {
        public RestoreCommand SeparateRestoreCommand { get; }

        public RestoringCommand(
            IEnumerable<string> msbuildArgs,
            bool noRestore,
            string msbuildPath = null)
            : base(GetCommandArguments(msbuildArgs, noRestore), msbuildPath)
        {
            Task.Run(() => WorkloadManifestUpdater.BackgroundUpdateAdvertisingManifestsAsync());
            SeparateRestoreCommand = GetSeparateRestoreCommand(msbuildArgs, noRestore, msbuildPath);
        }

        private static IEnumerable<string> GetCommandArguments(
            IEnumerable<string> arguments,
            bool noRestore)
        {
            if (noRestore) 
            {
                return arguments;
            }

            if (HasArgumentToExcludeFromRestore(arguments))
            {
                return Prepend("-nologo", arguments);
            }

            return Prepend("-restore", arguments);
        }

        private static RestoreCommand GetSeparateRestoreCommand(
            IEnumerable<string> arguments,
            bool noRestore,
            string msbuildPath)
        {
            if (noRestore || !HasArgumentToExcludeFromRestore(arguments))
            {
                return null;
            }

            IEnumerable<string> restoreArguments = new string[] { "-target:Restore" };
            if (arguments != null)
            {
                restoreArguments = restoreArguments.Concat(arguments.Where(
                    a => !IsExcludedFromRestore(a) && !IsExcludedFromSeparateRestore(a)));
            }

            return new RestoreCommand(restoreArguments, msbuildPath);
        }

        private static IEnumerable<string> Prepend(string argument, IEnumerable<string> arguments)
            => new[] { argument }.Concat(arguments);

        private static bool HasArgumentToExcludeFromRestore(IEnumerable<string> arguments)
            => arguments.Any(a => IsExcludedFromRestore(a));

        private static readonly string[] propertyPrefixes = new string[]{ "-", "/" };

        private static bool IsExcludedFromRestore(string argument)
            => propertyPrefixes.Any(prefix => argument.StartsWith($"{prefix}property:TargetFramework=", StringComparison.Ordinal)) ||
               propertyPrefixes.Any(prefix => argument.StartsWith($"{prefix}p:TargetFramework=", StringComparison.Ordinal));

        //  These arguments don't by themselves require that restore be run in a separate process,
        //  but if there is a separate restore process they shouldn't be passed to it
        private static bool IsExcludedFromSeparateRestore(string argument)
            => propertyPrefixes.Any(prefix => argument.StartsWith($"{prefix}t:", StringComparison.Ordinal)) ||
               propertyPrefixes.Any(prefix => argument.StartsWith($"{prefix}target:", StringComparison.Ordinal)) ||
               propertyPrefixes.Any(prefix => argument.StartsWith($"{prefix}consoleloggerparameters:", StringComparison.Ordinal)) ||
               propertyPrefixes.Any(prefix => argument.StartsWith($"{prefix}clp:", StringComparison.Ordinal));

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
