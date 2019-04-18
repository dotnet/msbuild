// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ToolPackage;
using Microsoft.Extensions.EnvironmentAbstractions;
using System.Text.Json.Serialization;
using NuGet.Frameworks;
using NuGet.Versioning;
using System.Text.Json;
using System.Text;

namespace Microsoft.DotNet.ToolManifest
{
    internal class ToolManifestEditor : IToolManifestEditor
    {
        private readonly IDangerousFileDetector _dangerousFileDetector;
        private readonly IFileSystem _fileSystem;

        private const int SupportedToolManifestFileVersion = 1;
        private const int DefaultToolManifestFileVersion = 1;
        private const string JsonPropertyVersion = "version";
        private const string JsonPropertyIsRoot = "isRoot";
        private const string JsonPropertyCommands = "commands";
        private const string JsonPropertyTools = "tools";

        public ToolManifestEditor(IFileSystem fileSystem = null, IDangerousFileDetector dangerousFileDetector = null)
        {
            _dangerousFileDetector = dangerousFileDetector ?? new DangerousFileDetector();
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
                    existingPackage.Version.ToNormalizedString(),
                    existingPackage.PackageId.ToString(),
                    to.Value,
                    nuGetVersion.ToNormalizedString()));
            }

            if (deserializedManifest.Tools == null)
            {
                deserializedManifest.Tools = new Dictionary<string, SerializableLocalToolSinglePackage>();
            }

            deserializedManifest.Tools.Add(
                packageId.ToString(),
                new SerializableLocalToolSinglePackage
                {
                    Version = nuGetVersion.ToNormalizedString(),
                    Commands = toolCommandNames.Select(c => c.Value).ToArray()
                });

            _fileSystem.File.WriteAllText(to.Value, deserializedManifest.ToJson());
        }

        public (List<ToolManifestPackage> content, bool isRoot)
            Read(FilePath manifest, DirectoryPath correspondingDirectory)
        {
            if (_dangerousFileDetector.IsDangerous(manifest.Value))
            {
                throw new ToolManifestException(
                    string.Format(LocalizableStrings.ManifestHasMarkOfTheWeb, manifest.Value));
            }

            SerializableLocalToolsManifest deserializedManifest =
                DeserializeLocalToolsManifest(manifest);

            List<ToolManifestPackage> toolManifestPackages =
                GetToolManifestPackageFromOneManifestFile(
                    deserializedManifest,
                    manifest,
                    correspondingDirectory);

            return (toolManifestPackages, deserializedManifest.IsRoot.Value);
        }

        private SerializableLocalToolsManifest DeserializeLocalToolsManifest(FilePath possibleManifest)
        {
            var serializableLocalToolsManifest = new SerializableLocalToolsManifest();
            try
            {
                using (Stream jsonStream = _fileSystem.File.OpenRead(possibleManifest.Value))
                using (JsonDocument doc = JsonDocument.Parse(jsonStream))
                {
                    JsonElement root = doc.RootElement;

                    if (root.TryGetInt32Value(JsonPropertyVersion, out var version))
                    {
                        serializableLocalToolsManifest.Version = version;
                    }

                    if (root.TryGetBooleanValue(JsonPropertyIsRoot, out var isRoot))
                    {
                        serializableLocalToolsManifest.IsRoot = isRoot;
                    }

                    if (root.TryGetProperty(JsonPropertyTools, out var tools))
                    {
                        serializableLocalToolsManifest.Tools =
                            new Dictionary<string, SerializableLocalToolSinglePackage>();

                        if (tools.Type != JsonValueType.Object)
                        {
                            throw new ToolManifestException(
                                string.Format(LocalizableStrings.UnexpectedTypeInJson,
                                    JsonValueType.Object.ToString(),
                                    JsonPropertyTools));
                        }

                        foreach (var toolJson in tools.EnumerateObject())
                        {
                            var serializableLocalToolSinglePackage = new SerializableLocalToolSinglePackage();
                            if (toolJson.Value.TryGetStringValue(JsonPropertyVersion, out var versionJson))
                            {
                                serializableLocalToolSinglePackage.Version = versionJson;
                            }

                            var commands = new List<string>();
                            if (toolJson.Value.TryGetProperty(JsonPropertyCommands, out var commandsJson))
                            {
                                if (commandsJson.Type != JsonValueType.Array)
                                {
                                    throw new ToolManifestException(
                                        string.Format(LocalizableStrings.UnexpectedTypeInJson,
                                            JsonValueType.Array.ToString(),
                                            JsonPropertyCommands));
                                }

                                foreach (var command in commandsJson.EnumerateArray())
                                {
                                    if (command.Type != JsonValueType.String)
                                    {
                                        throw new ToolManifestException(
                                            string.Format(LocalizableStrings.UnexpectedTypeInJson,
                                                JsonValueType.String.ToString(),
                                                "command"));
                                    }

                                    commands.Add(command.GetString());
                                }

                                serializableLocalToolSinglePackage.Commands = commands.ToArray();
                            }

                            serializableLocalToolsManifest.Tools.Add(toolJson.Name, serializableLocalToolSinglePackage);
                        }
                    }
                }

                return serializableLocalToolsManifest;
            }
            catch (Exception e) when (
                e is JsonReaderException ||
                e is ArgumentException) // "duplicated key"
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

            ValidateVersion(deserializedManifest, errors);

            if (!deserializedManifest.IsRoot.HasValue)
            {
                errors.Add(string.Format(LocalizableStrings.ManifestMissingIsRoot, path.Value));
            }

            foreach (KeyValuePair<string, SerializableLocalToolSinglePackage> tools
                in (deserializedManifest.Tools ?? new Dictionary<string, SerializableLocalToolSinglePackage>()))
            {
                var packageLevelErrors = new List<string>();
                var packageIdString = tools.Key;
                var packageId = new PackageId(packageIdString);

                string versionString = tools.Value.Version;
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

                if (tools.Value.Commands == null
                    || (tools.Value.Commands != null && tools.Value.Commands.Length == 0))
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
                        ToolCommandName.Convert(tools.Value.Commands),
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

        private static void ValidateVersion(SerializableLocalToolsManifest deserializedManifest,
                                            List<string> errors)
        {
            var deserializedManifestVersion = deserializedManifest.Version;
            if (deserializedManifestVersion == null)
            {
                deserializedManifestVersion = DefaultToolManifestFileVersion;
            }

            if (deserializedManifestVersion == 0)
            {
                errors.Add(LocalizableStrings.ManifestVersion0);
            }

            if (deserializedManifestVersion > SupportedToolManifestFileVersion)
            {
                errors.Add(
                    string.Format(
                        LocalizableStrings.ManifestVersionHigherThanSupported,
                        deserializedManifestVersion, SupportedToolManifestFileVersion));
            }
        }

        private class SerializableLocalToolSinglePackage
        {
            public string Version { get; set; }
            public string[] Commands { get; set; }
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
            public int? Version { get; set; }

            public bool? IsRoot { get; set; }

            /// <summary>
            /// The dictionary's key is the package id
            /// this field could be null
            /// </summary>
            public Dictionary<string, SerializableLocalToolSinglePackage> Tools { get; set; }

            public string ToJson()
            {
                var state = new JsonWriterState(options: new JsonWriterOptions { Indented = true });
                using (var arrayBufferWriter = new ArrayBufferWriter<byte>())
                {
                    var writer = new Utf8JsonWriter(arrayBufferWriter, state);

                    writer.WriteStartObject();

                    if (Version.HasValue)
                    {
                        writer.WriteNumber(propertyName: JsonPropertyVersion, value: Version.Value);
                    }

                    if (IsRoot.HasValue)
                    {
                        writer.WriteBoolean(JsonPropertyIsRoot, IsRoot.Value);
                    }

                    writer.WriteStartObject(JsonPropertyTools);

                    foreach (var tool in Tools)
                    {
                        writer.WriteStartObject(tool.Key);
                        writer.WriteString(JsonPropertyVersion, tool.Value.Version);
                        writer.WriteStartArray(JsonPropertyCommands);
                        foreach (var toolCommandName in tool.Value.Commands)
                        {
                            writer.WriteStringValue(toolCommandName);
                        }

                        writer.WriteEndArray();
                        writer.WriteEndObject();
                    }

                    writer.WriteEndObject();
                    writer.WriteEndObject();
                    writer.Flush(true);

                    return Encoding.UTF8.GetString(arrayBufferWriter.WrittenMemory.ToArray());
                }
            }
        }

        public void Remove(FilePath fromFilePath, PackageId packageId)
        {
            SerializableLocalToolsManifest serializableLocalToolsManifest =
                DeserializeLocalToolsManifest(fromFilePath);

            List<ToolManifestPackage> toolManifestPackages =
                GetToolManifestPackageFromOneManifestFile(
                    serializableLocalToolsManifest,
                    fromFilePath,
                    fromFilePath.GetDirectoryPath());

            if (!toolManifestPackages.Any(t => t.PackageId.Equals(packageId)))
            {
                throw new ToolManifestException(string.Format(
                    LocalizableStrings.CannotFindPackageIdInManifest, packageId));
            }

            if (serializableLocalToolsManifest.Tools == null)
            {
                throw new InvalidOperationException(
                    $"Invalid state {nameof(serializableLocalToolsManifest)} if out of sync with {nameof(toolManifestPackages)}. " +
                    $"{nameof(serializableLocalToolsManifest)} cannot be null when " +
                    $"the package id can be found in {nameof(toolManifestPackages)}.");
            }

            serializableLocalToolsManifest.Tools = serializableLocalToolsManifest.Tools
                .Where(pair => !pair.Key.Equals(packageId.ToString(), StringComparison.Ordinal))
                .ToDictionary(pair => pair.Key, pair => pair.Value);

            _fileSystem.File.WriteAllText(
                           fromFilePath.Value,
                           serializableLocalToolsManifest.ToJson());
        }
    }
}
