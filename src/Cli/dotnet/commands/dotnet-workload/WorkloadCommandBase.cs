// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine.Parsing;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Workloads.Workload
{
    /// <summary>
    /// Base class for workload related commands.
    /// </summary>
    internal abstract class WorkloadCommandBase : CommandBase
    {
        /// <summary>
        /// Gets whether signatures for workload packages and installers should be verified.
        /// </summary>
        protected bool VerifySignatures
        {
            get;
        }

        public WorkloadCommandBase(ParseResult parseResult) : base(parseResult)
        {
            VerifySignatures = ShouldVerifySignatures(parseResult);
        }

        /// <summary>
        /// Determines whether workload packs and installer signatures should be verified based on whether 
        /// dotnet is signed, the skip option was specified, and whether a global policy enforcing verification
        /// was set.
        /// </summary>
        /// <param name="parseResult"></param>
        /// <returns></returns>
        /// <exception cref="GracefulException" />
        private bool ShouldVerifySignatures(ParseResult parseResult)
        {
            if (!SignCheck.IsDotNetSigned())
            {
                // Can't enforce anything if we already allowed an unsigned dotnet to be installed.
                return false;
            }

            bool skipSignCheck = parseResult.GetValueForOption(WorkloadInstallCommandParser.SkipSignCheckOption);
            bool policyEnabled = SignCheck.IsWorkloadSignVerificationPolicySet();

            if (skipSignCheck && policyEnabled)
            {
                // Can't override the global policy by using the skip option.
                throw new GracefulException(LocalizableStrings.SkipSignCheckInvalidOption);
            }

            return !skipSignCheck;
        }
    }
}
