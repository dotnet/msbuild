// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.PlatformAbstractions;
using Microsoft.DotNet.ToolManifest;
using Microsoft.DotNet.ToolPackage;
using NuGet.Frameworks;
using Microsoft.DotNet.Cli;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.CommandFactory
{
    internal class LocalToolsCommandResolver : ICommandResolver
    {
        private readonly ToolManifestFinder _toolManifest;
        private readonly ILocalToolsResolverCache _localToolsResolverCache;
        private readonly IFileSystem _fileSystem;
        private readonly DirectoryPath _nugetGlobalPackagesFolder;
        private const string LeadingDotnetPrefix = "dotnet-";

        public LocalToolsCommandResolver(
            ToolManifestFinder toolManifest = null,
            ILocalToolsResolverCache localToolsResolverCache = null,
            IFileSystem fileSystem = null,
            DirectoryPath? nugetGlobalPackagesFolder = null)
        {
            _toolManifest = toolManifest ?? new ToolManifestFinder(new DirectoryPath(Directory.GetCurrentDirectory()));
            _localToolsResolverCache = localToolsResolverCache ?? new LocalToolsResolverCache();
            _fileSystem = fileSystem ?? new FileSystemWrapper();
            _nugetGlobalPackagesFolder =
                nugetGlobalPackagesFolder ?? new DirectoryPath(NuGetGlobalPackagesFolder.GetLocation());
        }

        public CommandSpec Resolve(CommandResolverArguments arguments)
        {
            if (arguments == null || string.IsNullOrWhiteSpace(arguments.CommandName))
            {
                return null;
            }

            if (!arguments.CommandName.StartsWith(LeadingDotnetPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var resolveResultWithoutLeadingDotnet = GetPackageCommandSpecUsingMuxer(arguments,
                new ToolCommandName(arguments.CommandName.Substring(LeadingDotnetPrefix.Length)));

            var resolveResultWithLeadingDotnet =
                GetPackageCommandSpecUsingMuxer(arguments, new ToolCommandName(arguments.CommandName));

            if (resolveResultWithoutLeadingDotnet != null && resolveResultWithLeadingDotnet != null)
            {
                return resolveResultWithoutLeadingDotnet;
            }
            else
            {
                return resolveResultWithoutLeadingDotnet ?? resolveResultWithLeadingDotnet;
            }
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
                _nugetGlobalPackagesFolder,
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

            return null;
        }
    }
}
