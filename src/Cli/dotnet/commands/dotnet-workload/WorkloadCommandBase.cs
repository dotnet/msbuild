// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Common;

namespace Microsoft.DotNet.Workloads.Workload
{
    /// <summary>
    /// Base class for workload related commands.
    /// </summary>
    internal abstract class WorkloadCommandBase : CommandBase
    {
        /// <summary>
        /// The package downloader to use for acquiring NuGet packages.
        /// </summary>
        protected INuGetPackageDownloader PackageDownloader
        {
            get;
        }

        /// <summary>
        /// Provides basic output primitives for the command.
        /// </summary>
        protected IReporter Reporter
        {
            get;
        }

        /// <summary>
        /// Configuration options used by NuGet when performing a restore.
        /// </summary>
        protected RestoreActionConfig RestoreActionConfiguration
        {
            get;
        }

        /// <summary>
        /// Temporary directory used for downloading NuGet packages.
        /// </summary>
        protected DirectoryPath TempPackagesDirectory
        {
            get;
        }

        /// <summary>
        /// The path of the temporary temporary directory to use.
        /// </summary>
        protected string TempDirectoryPath
        {
            get;
        }

        /// <summary>
        /// The verbosity level to use when reporting output from the command.
        /// </summary>
        protected VerbosityOptions Verbosity
        {
            get;
        }

        /// <summary>
        /// Gets whether signatures for workload packages and installers should be verified.
        /// </summary>
        protected bool VerifySignatures
        {
            get;
        }

        /// <summary>
        /// Initializes a new <see cref="WorkloadCommandBase"/> instance.
        /// </summary>
        /// <param name="parseResult">The results of parsing the command line.</param>
        /// <param name="verbosityOptions">The command line option used to define the verbosity level.</param>
        /// <param name="reporter">The reporter to use for output.</param>
        /// <param name="tempDirPath">The directory to use for volatile output. If no value is specified, the commandline
        /// option is used if present, otherwise the default temp directory used.</param>
        /// <param name="nugetPackageDownloader">The package downloader to use for acquiring NuGet packages.</param>
        public WorkloadCommandBase(ParseResult parseResult,
            Option<VerbosityOptions> verbosityOptions = null,
            IReporter reporter = null,
            string tempDirPath = null,
            INuGetPackageDownloader nugetPackageDownloader = null) : base(parseResult)
        {
            VerifySignatures = ShouldVerifySignatures(parseResult);

            RestoreActionConfiguration = _parseResult.ToRestoreActionConfig();

            Verbosity = verbosityOptions == null
                ? parseResult.GetValueForOption(CommonOptions.VerbosityOption)
                : parseResult.GetValueForOption(verbosityOptions);

            ILogger nugetLogger = Verbosity.VerbosityIsDetailedOrDiagnostic() ? new NuGetConsoleLogger() : new NullLogger();

            Reporter = reporter ?? Cli.Utils.Reporter.Output;

            TempDirectoryPath = !string.IsNullOrWhiteSpace(tempDirPath)
                ? tempDirPath
                : !string.IsNullOrWhiteSpace(parseResult.GetValueForOption(WorkloadInstallCommandParser.TempDirOption))
                ? parseResult.GetValueForOption(WorkloadInstallCommandParser.TempDirOption)
                : Path.GetTempPath();

            TempPackagesDirectory = new DirectoryPath(Path.Combine(TempDirectoryPath, "dotnet-sdk-advertising-temp"));

            PackageDownloader = nugetPackageDownloader ?? new NuGetPackageDownloader(TempPackagesDirectory,
                filePermissionSetter: null,
                new FirstPartyNuGetPackageSigningVerifier(),
                nugetLogger,
                restoreActionConfig: RestoreActionConfiguration,
                verifySignatures: VerifySignatures);
        }

        /// <summary>
        /// Determines whether workload packs and installer signatures should be verified based on whether 
        /// dotnet is signed, the skip option was specified, and whether a global policy enforcing verification
        /// was set.
        /// </summary>
        /// <param name="parseResult">The results of parsing the command line.</param>
        /// <returns><see langword="true"/> if signatures of packages and installers should be verified.</returns>
        /// <exception cref="GracefulException" />
        private static bool ShouldVerifySignatures(ParseResult parseResult)
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
