// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
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
    internal class ToolManifestFinder : IToolManifestFinder
    {
        private readonly DirectoryPath _probStart;
        private readonly IFileSystem _fileSystem;
        private const string _manifestFilenameConvention = "localtool.manifest.json";

        public ToolManifestFinder(DirectoryPath probStart, IFileSystem fileSystem = null)
        {
            _probStart = probStart;
            _fileSystem = fileSystem ?? new FileSystemWrapper();
        }

        public IReadOnlyCollection<ToolManifestPackage> Find(FilePath? filePath = null)
        {
            var result = new List<ToolManifestPackage>();

            IEnumerable<FilePath> allPossibleManifests =
                filePath != null
                    ? new[] { filePath.Value }
                    : EnumerateDefaultAllPossibleManifests();

            foreach (FilePath possibleManifest in allPossibleManifests)
            {
                if (_fileSystem.File.Exists(possibleManifest.Value))
                {
                    SerializableLocalToolsManifest deserializedManifest = JsonConvert.DeserializeObject<SerializableLocalToolsManifest>(
                        _fileSystem.File.ReadAllText(possibleManifest.Value), new JsonSerializerSettings
                        {
                            MissingMemberHandling = MissingMemberHandling.Ignore
                        });

                    var errors = new List<string>();

                    if (!deserializedManifest.isRoot)
                    {
                        errors.Add("isRoot is false is not supported.");
                    }

                    if (deserializedManifest.version != 1)
                    {
                        errors.Add(string.Format("Tools manifest format version {0} is not supported.",
                            deserializedManifest.version));
                    }

                    foreach (var tools in deserializedManifest.tools)
                    {
                        var packageLevelErrors = new List<string>();
                        var packageIdString = tools.Key;
                        var packageId = new PackageId(packageIdString);

                        string versionString = tools.Value.version;
                        NuGetVersion version = null;
                        if (versionString is null)
                        {
                            packageLevelErrors.Add(LocalizableStrings.MissingVersion);
                        }
                        else
                        {
                            var versionParseResult = NuGetVersion.TryParse(
                                versionString, out version);

                            if (!versionParseResult)
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
                            var joined = string.Join(string.Empty, packageLevelErrors.Select(e => Environment.NewLine + "    " + e));
                            errors.Add(string.Format(LocalizableStrings.InPackage, packageId.ToString()) + joined);
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
                        throw new ToolManifestException(LocalizableStrings.InvalidManifestFilePrefix +
                            string.Join(string.Empty, errors.Select(e => Environment.NewLine + "  " + e)));
                    }

                    return result;
                }
            }

            throw new ToolManifestCannotFindException(
                string.Format(LocalizableStrings.CannotFindAnyManifestsFileSearched,
                    string.Join(Environment.NewLine, allPossibleManifests.Select(f => f.Value))));
        }

        private IEnumerable<FilePath> EnumerateDefaultAllPossibleManifests()
        {
            DirectoryPath? currentSearchDirectory = _probStart;
            while (currentSearchDirectory != null)
            {
                var tryManifest = currentSearchDirectory.Value.WithFile(_manifestFilenameConvention);
                yield return tryManifest;
                currentSearchDirectory = currentSearchDirectory.Value.GetParentPathNullable();
            }
        }

        private class SerializableLocalToolsManifest
        {
            [JsonProperty(Required = Required.Always)]
            public int version { get; set; }

            [JsonProperty(Required = Required.Always)]
            public bool isRoot { get; set; }

            [JsonProperty(Required = Required.Always)]
            // The dictionary's key is the package id
            public Dictionary<string, SerializableLocalToolSinglePackage> tools { get; set; }
        }

        private class SerializableLocalToolSinglePackage
        {
            public string version { get; set; }
            public string[] commands { get; set; }
            public string targetFramework { get; set; }
        }
    }
}
