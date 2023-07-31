// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.Tools.MSBuild;
using Microsoft.DotNet.Tools.Restore;
using Microsoft.DotNet.Workloads.Workload.Install;

namespace Microsoft.DotNet.Tools
{
    public class RestoringCommand : MSBuildForwardingApp
    {
        public RestoreCommand SeparateRestoreCommand { get; }

        private bool AdvertiseWorkloadUpdates;

        public RestoringCommand(
            IEnumerable<string> msbuildArgs,
            bool noRestore,
            string msbuildPath = null,
            string userProfileDir = null,
            bool advertiseWorkloadUpdates = true)
            : base(GetCommandArguments(msbuildArgs, noRestore), msbuildPath)
        {
            userProfileDir = CliFolderPathCalculator.DotnetUserProfileFolderPath;
            Task.Run(() => WorkloadManifestUpdater.BackgroundUpdateAdvertisingManifestsAsync(userProfileDir));
            SeparateRestoreCommand = GetSeparateRestoreCommand(msbuildArgs, noRestore, msbuildPath);
            AdvertiseWorkloadUpdates = advertiseWorkloadUpdates;

            if (!noRestore)
            {
                NuGetSignatureVerificationEnabler.ConditionallyEnable(this);
            }
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

        private static readonly string[] propertyPrefixes = new string[]{ "-", "/", "--" };

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
            int exitCode;
            if (SeparateRestoreCommand != null)
            {
                exitCode = SeparateRestoreCommand.Execute();
                if (exitCode != 0)
                {
                    return exitCode;
                }
            }

            exitCode = base.Execute();
            if (AdvertiseWorkloadUpdates)
            {
                WorkloadManifestUpdater.AdvertiseWorkloadUpdates();
            }
            return exitCode;
        }
    }
}
