// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// Manages a set of allowed unknown attributes and elements that should be silently skipped
    /// during project parsing instead of throwing an InvalidProjectFileException.
    /// Configuration is loaded from Directory.Parse.config files discovered additively from:
    /// 1. Next to the MSBuild executable
    /// 2. User profile (~/.msbuild/)
    /// 3. MSBUILD_PARSE_CONFIG environment variable paths
    /// </summary>
    internal sealed class UnknownElementsConfiguration : ITranslatable
    {
        internal const string ConfigFileName = "Directory.Parse.config";
        internal const string EnvironmentVariableName = "MSBUILD_PARSE_CONFIG";

        private const string UserProfileSubfolder = ".msbuild";

        private Dictionary<string, HashSet<string>> _allowedAttributes;
        private Dictionary<string, HashSet<string>> _allowedChildren;
        private HashSet<string> _loadedConfigFiles;
        private readonly ConcurrentDictionary<string, int> _skippedItems = new(StringComparer.OrdinalIgnoreCase);

        private UnknownElementsConfiguration()
        {
            _allowedAttributes = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            _allowedChildren = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            _loadedConfigFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        private UnknownElementsConfiguration(ITranslator translator)
            : this()
        {
            ((ITranslatable)this).Translate(translator);
        }

        internal IReadOnlyCollection<string> LoadedConfigFiles => _loadedConfigFiles;

        internal static UnknownElementsConfiguration Empty { get; } = new UnknownElementsConfiguration();

        /// <summary>
        /// Checks whether the given file path has already been loaded into this configuration.
        /// </summary>
        internal bool ContainsLoadedFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return false;
            }

            try
            {
                return _loadedConfigFiles.Contains(FileUtilities.NormalizePath(filePath));
            }
            catch
            {
                return false;
            }
        }

        internal bool CheckSkipAttribute(string elementName, string attributeName)
        {
            if (_allowedAttributes.Count == 0)
            {
                return false;
            }

            if (!_allowedAttributes.TryGetValue(elementName, out HashSet<string>? allowedAttributes) || !allowedAttributes.Contains(attributeName))
            {
                return false;
            }

            RecordSkippedItem($"Attribute:{elementName}:{attributeName}");
            return true;
        }

        internal bool CheckSkipElement(string parentElementName, string childElementName)
        {
            if (_allowedChildren.Count == 0)
            {
                return false;
            }

            if (!_allowedChildren.TryGetValue(parentElementName, out HashSet<string>? allowedChildren) || !allowedChildren.Contains(childElementName))
            {
                return false;
            }

            RecordSkippedItem($"Element:{parentElementName}:{childElementName}");
            return true;
        }

        internal string? GetSkippedSummaryMessage()
        {
            if (_skippedItems.IsEmpty)
            {
                return null;
            }

            var sb = new StringBuilder("Skipped unrecognized items allowed by Directory.Parse.config:");
            foreach (var kvp in _skippedItems)
            {
                sb.Append($" {kvp.Key} ({kvp.Value} occurrence{(kvp.Value > 1 ? "s" : string.Empty)});");
            }

            sb.Length--;
            return sb.ToString();
        }

        internal string? GetLoadedConfigsMessage()
        {
            if (_loadedConfigFiles.Count == 0)
            {
                return null;
            }

            return $"Loaded Directory.Parse.config from: {string.Join(", ", _loadedConfigFiles)}";
        }

        internal UnknownElementsConfiguration Merge(UnknownElementsConfiguration other)
        {
            ArgumentNullException.ThrowIfNull(other);

            var merged = new UnknownElementsConfiguration();

            UnionEntries(merged._allowedAttributes, _allowedAttributes);
            UnionEntries(merged._allowedAttributes, other._allowedAttributes);
            UnionEntries(merged._allowedChildren, _allowedChildren);
            UnionEntries(merged._allowedChildren, other._allowedChildren);
            merged._loadedConfigFiles.UnionWith(_loadedConfigFiles);
            merged._loadedConfigFiles.UnionWith(other._loadedConfigFiles);
            MergeSkippedItems(merged._skippedItems, _skippedItems);
            MergeSkippedItems(merged._skippedItems, other._skippedItems);

            return merged;
        }

        internal static UnknownElementsConfiguration LoadFromFile(string filePath)
        {
            var config = new UnknownElementsConfiguration();
            config.LoadFile(filePath);
            return config;
        }

        internal static UnknownElementsConfiguration LoadGlobalConfig(string? startingDirectory = null)
        {
            var config = new UnknownElementsConfiguration();

            string? msbuildExeDir = BuildEnvironmentHelper.Instance?.CurrentMSBuildToolsDirectory;
            if (!string.IsNullOrEmpty(msbuildExeDir))
            {
                config.LoadFile(Path.Combine(msbuildExeDir, ConfigFileName));
            }

            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(userProfile))
            {
                config.LoadFile(Path.Combine(userProfile, UserProfileSubfolder, ConfigFileName));
            }

            string? envValue = Environment.GetEnvironmentVariable(EnvironmentVariableName);
            if (!string.IsNullOrEmpty(envValue))
            {
                string[] paths = envValue.Split(new[] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string path in paths)
                {
                    string trimmedPath = path.Trim();
                    if (trimmedPath.Length > 0)
                    {
                        config.LoadFile(trimmedPath);
                    }
                }
            }

            // Walk up from the starting directory looking for a Directory.Parse.config
            if (!string.IsNullOrEmpty(startingDirectory))
            {
                string directoryConfigPath = FileUtilities.GetPathOfFileAbove(ConfigFileName, startingDirectory!);
                if (!string.IsNullOrEmpty(directoryConfigPath) && !config.ContainsLoadedFile(directoryConfigPath))
                {
                    config.LoadFile(directoryConfigPath);
                }
            }

            return config;
        }

        void ITranslatable.Translate(ITranslator translator)
        {
            translator.TranslateDictionary(ref _allowedAttributes, StringComparer.OrdinalIgnoreCase, TranslateHashSetValue, HashSetValueFactory);
            translator.TranslateDictionary(ref _allowedChildren, StringComparer.OrdinalIgnoreCase, TranslateHashSetValue, HashSetValueFactory);
            translator.Translate(ref _loadedConfigFiles);
        }

        private static void TranslateHashSetValue(ITranslator translator, NodePacketValueFactory<HashSet<string>> factory, ref HashSet<string> value)
        {
            translator.Translate(ref value);
        }

        private static HashSet<string> HashSetValueFactory(ITranslator translator)
        {
            HashSet<string>? set = null;
            translator.Translate(ref set);
            return set ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        internal static UnknownElementsConfiguration FactoryForDeserialization(ITranslator translator)
        {
            return new UnknownElementsConfiguration(translator);
        }

        private static void UnionEntries(Dictionary<string, HashSet<string>> destination, Dictionary<string, HashSet<string>> source)
        {
            foreach (var kvp in source)
            {
                if (!destination.TryGetValue(kvp.Key, out HashSet<string>? names))
                {
                    names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    destination[kvp.Key] = names;
                }

                names.UnionWith(kvp.Value);
            }
        }

        private static void MergeSkippedItems(ConcurrentDictionary<string, int> destination, ConcurrentDictionary<string, int> source)
        {
            foreach (var kvp in source)
            {
                destination.AddOrUpdate(kvp.Key, kvp.Value, (_, count) => count + kvp.Value);
            }
        }

        private void RecordSkippedItem(string key)
        {
            _skippedItems.AddOrUpdate(key, 1, static (_, count) => count + 1);
        }

        private void LoadFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return;
            }

            string fullPath;
            try
            {
                fullPath = FileUtilities.NormalizePath(filePath);
            }
            catch
            {
                return;
            }

            if (!FileSystems.Default.FileExists(fullPath) || !_loadedConfigFiles.Add(fullPath))
            {
                return;
            }

            foreach (string rawLine in File.ReadLines(filePath))
            {
                string entry = rawLine.Trim();
                if (string.IsNullOrEmpty(entry) || entry[0] == '#')
                {
                    continue;
                }

                int firstColon = entry.IndexOf(':');
                if (firstColon < 0)
                {
                    continue;
                }

                int secondColon = entry.IndexOf(':', firstColon + 1);
                if (secondColon < 0 || entry.IndexOf(':', secondColon + 1) >= 0)
                {
                    continue;
                }

                string type = entry.Substring(0, firstColon).Trim();
                string elementName = entry.Substring(firstColon + 1, secondColon - firstColon - 1).Trim();
                string unknownName = entry.Substring(secondColon + 1).Trim();

                if (type.Length == 0 || elementName.Length == 0 || unknownName.Length == 0)
                {
                    continue;
                }

                if (type.Equals("ATTRIBUTE", StringComparison.OrdinalIgnoreCase))
                {
                    AddAllowedName(_allowedAttributes, elementName, unknownName);
                }
                else if (type.Equals("ELEMENT", StringComparison.OrdinalIgnoreCase))
                {
                    AddAllowedName(_allowedChildren, elementName, unknownName);
                }
            }

            _loadedConfigFiles.Add(fullPath);
        }

        private static void AddAllowedName(Dictionary<string, HashSet<string>> destination, string elementName, string unknownName)
        {
            if (!destination.TryGetValue(elementName, out HashSet<string>? names))
            {
                names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                destination[elementName] = names;
            }

            names.Add(unknownName);
        }
    }
}
