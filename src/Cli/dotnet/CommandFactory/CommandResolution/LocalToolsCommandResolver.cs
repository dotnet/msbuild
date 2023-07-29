// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ToolManifest;
using Microsoft.DotNet.ToolPackage;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Frameworks;

namespace Microsoft.DotNet.CommandFactory
{
    internal class LocalToolsCommandResolver : ICommandResolver
    {
        private readonly ToolManifestFinder _toolManifest;
        private readonly ILocalToolsResolverCache _localToolsResolverCache;
        private readonly IFileSystem _fileSystem;
        private const string LeadingDotnetPrefix = "dotnet-";

        public LocalToolsCommandResolver(
            ToolManifestFinder toolManifest = null,
            ILocalToolsResolverCache localToolsResolverCache = null,
            IFileSystem fileSystem = null)
        {
            _toolManifest = toolManifest ?? new ToolManifestFinder(new DirectoryPath(Directory.GetCurrentDirectory()));
            _localToolsResolverCache = localToolsResolverCache ?? new LocalToolsResolverCache();
            _fileSystem = fileSystem ?? new FileSystemWrapper();
        }

        public CommandSpec ResolveStrict(CommandResolverArguments arguments)
        {
            if (arguments == null || string.IsNullOrWhiteSpace(arguments.CommandName))
            {
                return null;
            }

            if (!arguments.CommandName.StartsWith(LeadingDotnetPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var resolveResult = GetPackageCommandSpecUsingMuxer(arguments,
                new ToolCommandName(arguments.CommandName.Substring(LeadingDotnetPrefix.Length)));

            return resolveResult;
        }

        public CommandSpec Resolve(CommandResolverArguments arguments)
        {
            if (arguments == null || string.IsNullOrWhiteSpace(arguments.CommandName))
            {
                return null;
            }

            if (!arguments.CommandName.StartsWith(LeadingDotnetPrefix, StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(arguments.CommandName.Substring(LeadingDotnetPrefix.Length)))
            {
                return null;
            }

            // Try resolving without prefix first
            var result = GetPackageCommandSpecUsingMuxer(
                arguments,
                new ToolCommandName(arguments.CommandName.Substring(LeadingDotnetPrefix.Length)));

            if (result != null)
            {
                return result;
            }

            // Fallback to resolving with the prefix
            return GetPackageCommandSpecUsingMuxer(arguments, new ToolCommandName(arguments.CommandName));
        }

        private CommandSpec GetPackageCommandSpecUsingMuxer(CommandResolverArguments arguments,
            ToolCommandName toolCommandName)
        {
            if (!_toolManifest.TryFind(toolCommandName, out var toolManifestPackage))
            {
                return null;
            }

            if (_localToolsResolverCache.TryLoad(
                new RestoredCommandIdentifier(
                    toolManifestPackage.PackageId,
                    toolManifestPackage.Version,
                    NuGetFramework.Parse(BundledTargetFramework.GetTargetFrameworkMoniker()),
                    Constants.AnyRid,
                    toolCommandName),
                out var restoredCommand))
            {
                if (!_fileSystem.File.Exists(restoredCommand.Executable.Value))
                {
                    throw new GracefulException(string.Format(LocalizableStrings.NeedRunToolRestore,
                        toolCommandName.ToString()));
                }

                return MuxerCommandSpecMaker.CreatePackageCommandSpecUsingMuxer(
                    restoredCommand.Executable.Value,
                    arguments.CommandArguments);
            }
            else
            {
                throw new GracefulException(string.Format(LocalizableStrings.NeedRunToolRestore,
                        toolCommandName.ToString()));
            }
        }
    }
}
