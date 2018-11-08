// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ToolPackage;
using Microsoft.Extensions.EnvironmentAbstractions;
using Newtonsoft.Json;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace Microsoft.DotNet.ToolManifest
{
    internal class ToolManifestEditor
    {
        private readonly IFileSystem _fileSystem;

        // The supported tool manifest file version.
        private const int SupportedVersion = 1;

        public ToolManifestEditor(IFileSystem fileSystem = null)
        {
            _fileSystem = fileSystem ?? new FileSystemWrapper();
        }

        public void Add(
            FilePath to,
            PackageId packageId,
            NuGetVersion nuGetVersion,
            ToolCommandName[] toolCommandNames)
        {
            SerializableLocalToolsManifest deserializedManifest =
                DeserializeLocalToolsManifest(to);

            List<ToolManifestPackage> toolManifestPackages =
                GetToolManifestPackageFromOneManifestFile(deserializedManifest, to, to.GetDirectoryPath());

            var existing = toolManifestPackages.Where(t => t.PackageId.Equals(packageId)).ToArray();
            if (existing.Any())
            {
                var existingPackage = existing.Single();

                if (existingPackage.PackageId.Equals(packageId)
                    && existingPackage.Version == nuGetVersion
                    && CommandNamesEqual(existingPackage.CommandNames, toolCommandNames))
                {
                    return;
                }

                throw new ToolManifestException(string.Format(
                    LocalizableStrings.ManifestPackageIdCollision,
                    packageId.ToString(),
                    nuGetVersion.ToNormalizedString(),
                    to.Value,
                    existingPackage.PackageId.ToString(),
                    existingPackage.Version.ToNormalizedString()));
            }

            deserializedManifest.tools.Add(
                packageId.ToString(),
                new SerializableLocalToolSinglePackage
                {
                    version = nuGetVersion.ToNormalizedString(),
                    commands = toolCommandNames.Select(c => c.Value).ToArray()
                });

            _fileSystem.File.WriteAllText(
                to.Value,
                JsonConvert.SerializeObject(deserializedManifest, Formatting.Indented));
        }

        public (List<ToolManifestPackage> content, bool isRoot) 
            Read(FilePath manifest, DirectoryPath correspondingDirectory)
        {
            SerializableLocalToolsManifest deserializedManifest =
                DeserializeLocalToolsManifest(manifest);

            List<ToolManifestPackage> toolManifestPackages =
                GetToolManifestPackageFromOneManifestFile(
                    deserializedManifest,
                    manifest,
                    correspondingDirectory);

            return (toolManifestPackages, deserializedManifest.isRoot.Value);
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
                throw new ToolManifestException(string.Format(LocalizableStrings.JsonParsingError,
                    possibleManifest.Value, e.Message));
            }
        }

        private List<ToolManifestPackage> GetToolManifestPackageFromOneManifestFile(
            SerializableLocalToolsManifest deserializedManifest,
            FilePath path,
            DirectoryPath correspondingDirectory)
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
                        ToolCommandName.Convert(tools.Value.commands),
                        correspondingDirectory));
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

        private class SerializableLocalToolSinglePackage
        {
            public string version { get; set; }
            public string[] commands { get; set; }
        }

        private static bool CommandNamesEqual(ToolCommandName[] left, ToolCommandName[] right)
        {
            if (left == null)
            {
                return right == null;
            }

            if (right == null)
            {
                return false;
            }

            return left.SequenceEqual(right);
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
    }
}
