// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ToolPackage;
using Microsoft.Extensions.EnvironmentAbstractions;
using Newtonsoft.Json;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace Microsoft.DotNet.ToolManifest
{
    internal class ToolManifestFinder : IToolManifestFinder
    {
        private readonly DirectoryPath _probStart;
        private readonly IFileSystem _fileSystem;
        private const string _manifestFilenameConvention = "dotnet-tools.json";

        // The supported tool manifest file version.
        private const int SupportedVersion = 1;

        public ToolManifestFinder(DirectoryPath probStart, IFileSystem fileSystem = null)
        {
            _probStart = probStart;
            _fileSystem = fileSystem ?? new FileSystemWrapper();
        }

        public IReadOnlyCollection<ToolManifestPackage> Find(FilePath? filePath = null)
        {
            IEnumerable<FilePath> allPossibleManifests =
                filePath != null
                    ? new[] {filePath.Value}
                    : EnumerateDefaultAllPossibleManifests();

            bool findAnyManifest = false;
            var result = new List<ToolManifestPackage>();
            foreach (FilePath possibleManifest in allPossibleManifests)
            {
                if (!_fileSystem.File.Exists(possibleManifest.Value))
                {
                    continue;
                }

                findAnyManifest = true;
                SerializableLocalToolsManifest deserializedManifest =
                    DeserializeLocalToolsManifest(possibleManifest);

                List<ToolManifestPackage> toolManifestPackageFromOneManifestFile =
                    GetToolManifestPackageFromOneManifestFile(deserializedManifest, possibleManifest);

                foreach (ToolManifestPackage p in toolManifestPackageFromOneManifestFile)
                {
                    if (!result.Any(addedToolManifestPackages =>
                        addedToolManifestPackages.PackageId.Equals(p.PackageId)))
                    {
                        result.Add(p);
                    }
                }

                if (deserializedManifest.isRoot.Value)
                {
                    return result;
                }
            }

            if (!findAnyManifest)
            {
                throw new ToolManifestCannotBeFoundException(
                    string.Format(LocalizableStrings.CannotFindAnyManifestsFileSearched,
                        string.Join(Environment.NewLine, allPossibleManifests.Select(f => f.Value))));
            }

            return result;
        }

        public bool TryFind(ToolCommandName toolCommandName, out ToolManifestPackage toolManifestPackage)
        {
            toolManifestPackage = default(ToolManifestPackage);
            foreach (FilePath possibleManifest in EnumerateDefaultAllPossibleManifests())
            {
                if (!_fileSystem.File.Exists(possibleManifest.Value))
                {
                    continue;
                }

                SerializableLocalToolsManifest deserializedManifest =
                    DeserializeLocalToolsManifest(possibleManifest);

                List<ToolManifestPackage> toolManifestPackages =
                    GetToolManifestPackageFromOneManifestFile(deserializedManifest, possibleManifest);

                foreach (var package in toolManifestPackages)
                {
                    if (package.CommandNames.Contains(toolCommandName))
                    {
                        toolManifestPackage = package;
                        return true;
                    }
                }

                if (deserializedManifest.isRoot.Value)
                {
                    return false;
                }
            }

            return false;
        }

        private SerializableLocalToolsManifest DeserializeLocalToolsManifest(FilePath possibleManifest)
        {
            try
            {
                return JsonConvert.DeserializeObject<SerializableLocalToolsManifest>(
                    _fileSystem.File.ReadAllText(possibleManifest.Value), new JsonSerializerSettings
                    {
                        MissingMemberHandling = MissingMemberHandling.Ignore
                    });
            }
            catch (JsonReaderException e)
            {
                throw new ToolManifestException(string.Format(LocalizableStrings.JsonParsingError, possibleManifest.Value, e.Message));
            }
        }

        private List<ToolManifestPackage> GetToolManifestPackageFromOneManifestFile(
            SerializableLocalToolsManifest deserializedManifest, FilePath path)
        {
            List<ToolManifestPackage> result = new List<ToolManifestPackage>();
            var errors = new List<string>();

            if (deserializedManifest.version == 0)
            {
                errors.Add(LocalizableStrings.ManifestVersion0);
            }

            if (deserializedManifest.version > SupportedVersion)
            {
                errors.Add(
                    string.Format(
                        LocalizableStrings.ManifestVersionHigherThanSupported,
                        deserializedManifest.version, SupportedVersion));
            }

            if (!deserializedManifest.isRoot.HasValue)
            {
                errors.Add(string.Format(LocalizableStrings.ManifestMissingIsRoot, path.Value));
            }

            foreach (KeyValuePair<string, SerializableLocalToolSinglePackage> tools in deserializedManifest.tools)
            {
                var packageLevelErrors = new List<string>();
                var packageIdString = tools.Key;
                var packageId = new PackageId(packageIdString);

                string versionString = tools.Value.version;
                NuGetVersion version = null;
                if (versionString is null)
                {
                    packageLevelErrors.Add(LocalizableStrings.ToolMissingVersion);
                }
                else
                {
                    if (!NuGetVersion.TryParse(versionString, out version))
                    {
                        packageLevelErrors.Add(string.Format(LocalizableStrings.VersionIsInvalid, versionString));
                    }
                }

                if (tools.Value.commands == null
                    || (tools.Value.commands != null && tools.Value.commands.Length == 0))
                {
                    packageLevelErrors.Add(LocalizableStrings.FieldCommandsIsMissing);
                }

                if (packageLevelErrors.Any())
                {
                    var joinedWithIndentation = string.Join(Environment.NewLine,
                        packageLevelErrors.Select(e => "\t\t" + e));
                    errors.Add(string.Format(LocalizableStrings.InPackage, packageId.ToString(),
                        joinedWithIndentation));
                }
                else
                {
                    result.Add(new ToolManifestPackage(
                        packageId,
                        version,
                        ToolCommandName.Convert(tools.Value.commands)));
                }
            }

            if (errors.Any())
            {
                throw new ToolManifestException(
                    string.Format(LocalizableStrings.InvalidManifestFilePrefix,
                        path.Value,
                        string.Join(Environment.NewLine, errors.Select(e => "\t" + e))));
            }

            return result;
        }

        private IEnumerable<FilePath> EnumerateDefaultAllPossibleManifests()
        {
            DirectoryPath? currentSearchDirectory = _probStart;
            while (currentSearchDirectory.HasValue)
            {
                var currentSearchDotConfigDirectory = currentSearchDirectory.Value.WithSubDirectories(Constants.DotConfigDirectoryName);
                var tryManifest = currentSearchDirectory.Value.WithFile(_manifestFilenameConvention);
                yield return currentSearchDotConfigDirectory.WithFile(_manifestFilenameConvention);
                yield return tryManifest;
                currentSearchDirectory = currentSearchDirectory.Value.GetParentPathNullable();
            }
        }

        private class SerializableLocalToolsManifest
        {
            [DefaultValue(1)]
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            public int version { get; set; }

            public bool? isRoot { get; set; }

            [JsonProperty(Required = Required.Always)]
            // The dictionary's key is the package id
            public Dictionary<string, SerializableLocalToolSinglePackage> tools { get; set; }
        }

        private class SerializableLocalToolSinglePackage
        {
            public string version { get; set; }
            public string[] commands { get; set; }
        }
    }
}
