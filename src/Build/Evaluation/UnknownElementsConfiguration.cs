// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared.FileSystem;

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// An immutable set of allowed unknown attributes and elements that are silently skipped during
    /// project parsing instead of throwing an InvalidProjectFileException (MSB4066/MSB4067).
    /// </summary>
    /// <remarks>
    /// The configuration is resolved exactly once per build, anchored on the entry project's directory,
    /// by walking up for <c>Directory.Parse.config</c> files. The walk stops at a file declaring
    /// <c>root = true</c>; entries from nearer files win over farther ones.
    ///
    /// Instances are immutable and carry a content-based <see cref="Identity"/>. A
    /// <see cref="ProjectRootElementCache"/> is constructed with one configuration and keeps it for its
    /// lifetime, so a cache can never hold elements parsed under differing rules. Reuse points
    /// (worker nodes, the MSBuild server) compare <see cref="Identity"/> and replace the cache when it
    /// differs rather than mutating shared state.
    /// </remarks>
    internal sealed class UnknownElementsConfiguration : ITranslatable, IEquatable<UnknownElementsConfiguration>
    {
        internal const string ConfigFileName = "Directory.Parse.config";

        private const string AttributeTypeName = "Attribute";
        private const string ElementTypeName = "Element";
        private const string RootKeyName = "root";

        private FrozenDictionary<string, FrozenSet<string>> _allowedAttributes;
        private FrozenDictionary<string, FrozenSet<string>> _allowedChildren;
        private HashSet<string> _loadedConfigFiles;
        private HashSet<string> _malformedEntries;

        /// <summary>
        /// Content-based identity. Empty string for a configuration that permits nothing, so every
        /// build without a config file shares one identity and behaves exactly as it did before.
        /// </summary>
        private string _identity;

        /// <summary>
        /// Diagnostic-only counters. Not part of <see cref="Identity"/> or equality.
        /// </summary>
        private readonly ConcurrentDictionary<string, int> _skippedItems = new(StringComparer.OrdinalIgnoreCase);

        private UnknownElementsConfiguration()
        {
            _allowedAttributes = FrozenDictionary<string, FrozenSet<string>>.Empty;
            _allowedChildren = FrozenDictionary<string, FrozenSet<string>>.Empty;
            _loadedConfigFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _malformedEntries = new HashSet<string>(StringComparer.Ordinal);
            _identity = string.Empty;
        }

        private UnknownElementsConfiguration(ITranslator translator)
            : this()
        {
            ((ITranslatable)this).Translate(translator);
        }

        /// <summary>
        /// The configuration that permits nothing. Used wherever no config file applies, so callers
        /// never have to deal with null.
        /// </summary>
        internal static UnknownElementsConfiguration Empty { get; } = new UnknownElementsConfiguration();

        internal string Identity => _identity;

        internal bool IsEmpty => _identity.Length == 0;

        internal IReadOnlyCollection<string> LoadedConfigFiles => _loadedConfigFiles;

        /// <summary>
        /// Resolves the configuration that applies to a build anchored at <paramref name="startingDirectory"/>,
        /// which should be the directory of the entry project (or the current directory when no project
        /// was specified). Walks up collecting <c>Directory.Parse.config</c> files, nearest first,
        /// stopping at one that declares <c>root = true</c>.
        /// </summary>
        internal static UnknownElementsConfiguration Resolve(string? startingDirectory)
        {
            if (string.IsNullOrEmpty(startingDirectory) || Traits.Instance.EscapeHatches.DisableParseConfig)
            {
                return Empty;
            }

            List<string> chain = DiscoverConfigFiles(startingDirectory!);
            return chain.Count == 0 ? Empty : LoadFromFiles(chain);
        }

        /// <summary>
        /// Returns the config files applying to <paramref name="startingDirectory"/>, ordered nearest first.
        /// </summary>
        private static List<string> DiscoverConfigFiles(string startingDirectory)
        {
            List<string> chain = new();

            string? searchDirectory = startingDirectory;
            while (!string.IsNullOrEmpty(searchDirectory))
            {
                string configPath;
                try
                {
                    configPath = FileUtilities.GetPathOfFileAbove(ConfigFileName, searchDirectory);
                }
                catch
                {
                    break;
                }

                if (string.IsNullOrEmpty(configPath))
                {
                    break;
                }

                chain.Add(configPath);

                if (IsRootConfig(configPath))
                {
                    break;
                }

                // Continue searching from the parent of the directory that contained the file we just found.
                searchDirectory = Path.GetDirectoryName(Path.GetDirectoryName(configPath));
            }

            return chain;
        }

        /// <summary>
        /// Builds a configuration from a single config file. Convenience for tests and callers that already
        /// know the exact file.
        /// </summary>
        internal static UnknownElementsConfiguration LoadFromFile(string filePath)
            => LoadFromFiles(new[] { filePath });

        /// <summary>
        /// Builds a configuration from an ordered list of config file paths, nearest first.
        /// Entries are applied farthest-first so nearer files take precedence.
        /// </summary>
        internal static UnknownElementsConfiguration LoadFromFiles(IReadOnlyList<string> configFilePathsNearestFirst)
        {
            ArgumentNullException.ThrowIfNull(configFilePathsNearestFirst);

            Dictionary<string, HashSet<string>> attributes = NewNameMap();
            Dictionary<string, HashSet<string>> children = NewNameMap();
            HashSet<string> loadedFiles = new(StringComparer.OrdinalIgnoreCase);
            HashSet<string> malformed = new(StringComparer.Ordinal);

            for (int i = configFilePathsNearestFirst.Count - 1; i >= 0; i--)
            {
                LoadFile(configFilePathsNearestFirst[i], attributes, children, loadedFiles, malformed);
            }

            if (attributes.Count == 0 && children.Count == 0)
            {
                // Nothing was permitted; still surface which files were read so diagnostics are accurate,
                // but keep the canonical empty identity so caches remain shared.
                UnknownElementsConfiguration emptyWithProvenance = new();
                emptyWithProvenance._loadedConfigFiles = loadedFiles;
                emptyWithProvenance._malformedEntries = malformed;
                return emptyWithProvenance;
            }

            UnknownElementsConfiguration config = new();
            config.Initialize(attributes, children, loadedFiles, malformed);
            return config;
        }

        private void Initialize(
            Dictionary<string, HashSet<string>> attributes,
            Dictionary<string, HashSet<string>> children,
            HashSet<string> loadedFiles,
            HashSet<string> malformed)
        {
            _allowedAttributes = Freeze(attributes);
            _allowedChildren = Freeze(children);
            _loadedConfigFiles = loadedFiles;
            _malformedEntries = malformed;
            _identity = ComputeIdentity(attributes, children);
        }

        internal bool CheckSkipAttribute(string elementName, string attributeName)
        {
            if (_allowedAttributes.Count == 0)
            {
                return false;
            }

            if (!_allowedAttributes.TryGetValue(elementName, out FrozenSet<string>? allowedAttributes) || !allowedAttributes.Contains(attributeName))
            {
                return false;
            }

            RecordSkippedItem($"{AttributeTypeName}:{elementName}:{attributeName}");
            return true;
        }

        internal bool CheckSkipElement(string parentElementName, string childElementName)
        {
            if (_allowedChildren.Count == 0)
            {
                return false;
            }

            if (!_allowedChildren.TryGetValue(parentElementName, out FrozenSet<string>? allowedChildren) || !allowedChildren.Contains(childElementName))
            {
                return false;
            }

            RecordSkippedItem($"{ElementTypeName}:{parentElementName}:{childElementName}");
            return true;
        }

        internal string? GetSkippedSummaryMessage()
        {
            if (_skippedItems.IsEmpty)
            {
                return null;
            }

            StringBuilder sb = new("Skipped unrecognized items allowed by Directory.Parse.config:");
            foreach (KeyValuePair<string, int> kvp in _skippedItems)
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

        /// <summary>
        /// Lines that looked like entries but could not be understood. Surfaced so that a typo in a
        /// config file does not silently present as an unrelated MSB4066/MSB4067.
        /// </summary>
        internal string? GetMalformedEntriesMessage()
        {
            if (_malformedEntries.Count == 0)
            {
                return null;
            }

            return $"Ignored malformed Directory.Parse.config entries: {string.Join("; ", _malformedEntries)}";
        }

        public bool Equals(UnknownElementsConfiguration? other)
            => other is not null && string.Equals(_identity, other._identity, StringComparison.Ordinal);

        public override bool Equals(object? obj) => Equals(obj as UnknownElementsConfiguration);

        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(_identity);

        void ITranslatable.Translate(ITranslator translator)
        {
            Dictionary<string, HashSet<string>> attributes = Thaw(_allowedAttributes);
            Dictionary<string, HashSet<string>> children = Thaw(_allowedChildren);

            translator.TranslateDictionary(ref attributes, StringComparer.OrdinalIgnoreCase, TranslateHashSetValue, HashSetValueFactory);
            translator.TranslateDictionary(ref children, StringComparer.OrdinalIgnoreCase, TranslateHashSetValue, HashSetValueFactory);
            translator.Translate(ref _loadedConfigFiles);
            translator.Translate(ref _malformedEntries);

            if (translator.Mode == TranslationDirection.ReadFromStream)
            {
                Initialize(
                    attributes ?? NewNameMap(),
                    children ?? NewNameMap(),
                    _loadedConfigFiles ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                    _malformedEntries ?? new HashSet<string>(StringComparer.Ordinal));
            }
        }

        internal static UnknownElementsConfiguration FactoryForDeserialization(ITranslator translator)
        {
            return new UnknownElementsConfiguration(translator);
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

        private static Dictionary<string, HashSet<string>> NewNameMap()
            => new(StringComparer.OrdinalIgnoreCase);

        private static FrozenDictionary<string, FrozenSet<string>> Freeze(Dictionary<string, HashSet<string>> source)
        {
            if (source.Count == 0)
            {
                return FrozenDictionary<string, FrozenSet<string>>.Empty;
            }

            Dictionary<string, FrozenSet<string>> frozenValues = new(source.Count, StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, HashSet<string>> kvp in source)
            {
                frozenValues[kvp.Key] = kvp.Value.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
            }

            return frozenValues.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        }

        private static Dictionary<string, HashSet<string>> Thaw(FrozenDictionary<string, FrozenSet<string>> source)
        {
            Dictionary<string, HashSet<string>> result = new(source.Count, StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, FrozenSet<string>> kvp in source)
            {
                result[kvp.Key] = new HashSet<string>(kvp.Value, StringComparer.OrdinalIgnoreCase);
            }

            return result;
        }

        /// <summary>
        /// A stable, order-independent rendering of the permitted names. Two configurations reached via
        /// different files but permitting the same names are interchangeable and share a cache.
        /// </summary>
        private static string ComputeIdentity(Dictionary<string, HashSet<string>> attributes, Dictionary<string, HashSet<string>> children)
        {
            if (attributes.Count == 0 && children.Count == 0)
            {
                return string.Empty;
            }

            List<string> entries = new();
            AppendEntries(entries, AttributeTypeName, attributes);
            AppendEntries(entries, ElementTypeName, children);
            entries.Sort(StringComparer.OrdinalIgnoreCase);

            return string.Join("\n", entries);

            static void AppendEntries(List<string> entries, string type, Dictionary<string, HashSet<string>> source)
            {
                foreach (KeyValuePair<string, HashSet<string>> kvp in source)
                {
                    foreach (string name in kvp.Value)
                    {
                        entries.Add($"{type}:{kvp.Key}:{name}");
                    }
                }
            }
        }

        private void RecordSkippedItem(string key)
        {
            _skippedItems.AddOrUpdate(key, 1, static (_, count) => count + 1);
        }

        /// <summary>
        /// Returns true if the config file declares <c>root = true</c>, which terminates the upward walk.
        /// </summary>
        private static bool IsRootConfig(string filePath)
        {
            try
            {
                foreach (string rawLine in File.ReadLines(filePath))
                {
                    if (TryParseRootDeclaration(rawLine, out bool isRoot))
                    {
                        return isRoot;
                    }
                }
            }
            catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
            {
                // A config we cannot read simply does not terminate the walk.
            }

            return false;
        }

        private static bool TryParseRootDeclaration(string rawLine, out bool isRoot)
        {
            isRoot = false;

            string entry = rawLine.Trim();
            if (entry.Length == 0 || entry[0] == '#')
            {
                return false;
            }

            int equals = entry.IndexOf('=');
            if (equals < 0)
            {
                return false;
            }

            if (!entry.AsSpan(0, equals).Trim().Equals(RootKeyName.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            isRoot = entry.AsSpan(equals + 1).Trim().Equals(bool.TrueString.AsSpan(), StringComparison.OrdinalIgnoreCase);
            return true;
        }

        private static void LoadFile(
            string filePath,
            Dictionary<string, HashSet<string>> attributes,
            Dictionary<string, HashSet<string>> children,
            HashSet<string> loadedFiles,
            HashSet<string> malformed)
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

            if (!FileSystems.Default.FileExists(fullPath) || !loadedFiles.Add(fullPath))
            {
                return;
            }

            IEnumerable<string> lines;
            try
            {
                lines = File.ReadAllLines(fullPath);
            }
            catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
            {
                return;
            }

            foreach (string rawLine in lines)
            {
                string entry = rawLine.Trim();
                if (entry.Length == 0 || entry[0] == '#')
                {
                    continue;
                }

                if (TryParseRootDeclaration(entry, out _))
                {
                    continue;
                }

                if (!TryParseEntry(entry, out bool isAttribute, out string elementName, out string unknownName))
                {
                    // Recorded rather than dropped: a typo here would otherwise present as an
                    // unrelated MSB4066/MSB4067 with no hint that the config was at fault.
                    malformed.Add($"{fullPath}: {entry}");
                    continue;
                }

                AddAllowedName(isAttribute ? attributes : children, elementName, unknownName);
            }
        }

        private static bool TryParseEntry(string entry, out bool isAttribute, out string elementName, out string unknownName)
        {
            isAttribute = false;
            elementName = string.Empty;
            unknownName = string.Empty;

            int firstColon = entry.IndexOf(':');
            if (firstColon < 0)
            {
                return false;
            }

            int secondColon = entry.IndexOf(':', firstColon + 1);
            if (secondColon < 0 || entry.IndexOf(':', secondColon + 1) >= 0)
            {
                return false;
            }

            string type = entry.Substring(0, firstColon).Trim();
            elementName = entry.Substring(firstColon + 1, secondColon - firstColon - 1).Trim();
            unknownName = entry.Substring(secondColon + 1).Trim();

            if (elementName.Length == 0 || unknownName.Length == 0)
            {
                return false;
            }

            if (type.Equals(AttributeTypeName, StringComparison.OrdinalIgnoreCase))
            {
                isAttribute = true;
                return true;
            }

            return type.Equals(ElementTypeName, StringComparison.OrdinalIgnoreCase);
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
