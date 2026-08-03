// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
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
        /// was specified). Walks up for the nearest <c>Directory.Parse.config</c> and uses that one.
        /// </summary>
        /// <remarks>
        /// First found wins, with no layering, matching how <c>Directory.Build.props</c>,
        /// <c>Directory.Build.rsp</c> and <c>Directory.Solution.props</c> are discovered. Layering would be
        /// borrowing a per-file notion from <c>.editorconfig</c>, but a build resolves exactly one
        /// configuration, so there is nothing for it to compose across. A nearer file that omits a permission
        /// granted by a farther one fails loudly, with MSB4066/MSB4067 naming the attribute or element.
        /// </remarks>
        internal static UnknownElementsConfiguration Resolve(string? startingDirectory)
        {
            if (string.IsNullOrEmpty(startingDirectory) || Traits.Instance.EscapeHatches.DisableParseConfig)
            {
                return Empty;
            }

            string configPath;
            try
            {
                configPath = FileUtilities.GetPathOfFileAbove(ConfigFileName, startingDirectory!);
            }
            catch
            {
                return Empty;
            }

            return string.IsNullOrEmpty(configPath) ? Empty : LoadFromFile(configPath);
        }

        /// <summary>
        /// Builds a configuration from a single config file.
        /// </summary>
        internal static UnknownElementsConfiguration LoadFromFile(string filePath)
        {
            ParsedConfigFile file = ParsedConfigFile.Read(filePath);

            Dictionary<string, HashSet<string>> attributes = NewNameMap();
            Dictionary<string, HashSet<string>> children = NewNameMap();
            HashSet<string> loadedFiles = new(StringComparer.OrdinalIgnoreCase);
            HashSet<string> malformed = new(StringComparer.Ordinal);

            if (file.Exists)
            {
                loadedFiles.Add(file.FullPath);

                foreach (string problem in file.Problems)
                {
                    malformed.Add(problem);
                }

                foreach ((bool isAttribute, string owner, string name) in file.Entries)
                {
                    AddAllowedName(isAttribute ? attributes : children, owner, name);
                }
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

        private static void AddAllowedName(Dictionary<string, HashSet<string>> destination, string elementName, string unknownName)
        {
            if (!destination.TryGetValue(elementName, out HashSet<string>? names))
            {
                names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                destination[elementName] = names;
            }

            names.Add(unknownName);
        }

        /// <summary>
        /// One <c>Directory.Parse.config</c> file, read from disk.
        /// </summary>
        /// <remarks>
        /// The format is XML, matching both MSBuild itself and the other <c>.config</c> files in the
        /// ecosystem (<c>NuGet.config</c>, <c>app.config</c>):
        /// <code>
        /// &lt;ParseConfig&gt;
        ///   &lt;AllowAttribute Element="Target" Name="CustomAttr" /&gt;
        ///   &lt;AllowElement Parent="Project" Name="ToolConfiguration" /&gt;
        /// &lt;/ParseConfig&gt;
        /// </code>
        /// Unrecognised elements are ignored rather than rejected, so a future MSBuild can add directives
        /// without older engines failing on files that use them. They are still reported, so that a typo
        /// does not silently present later as an unrelated MSB4066/MSB4067.
        /// </remarks>
        private sealed class ParsedConfigFile
        {
            private const string RootElementName = "ParseConfig";
            private const string AllowAttributeElementName = "AllowAttribute";
            private const string AllowElementElementName = "AllowElement";
            private const string ElementAttributeName = "Element";
            private const string ParentAttributeName = "Parent";
            private const string NameAttributeName = "Name";

            private ParsedConfigFile(string fullPath, bool exists)
            {
                FullPath = fullPath;
                Exists = exists;
                Entries = new List<(bool, string, string)>();
                Problems = new List<string>();
            }

            internal string FullPath { get; }

            internal bool Exists { get; }

            internal List<(bool IsAttribute, string Owner, string Name)> Entries { get; }

            internal List<string> Problems { get; }

            internal static ParsedConfigFile Read(string filePath)
            {
                string fullPath;
                try
                {
                    fullPath = FileUtilities.NormalizePath(filePath);
                }
                catch
                {
                    return new ParsedConfigFile(filePath ?? string.Empty, exists: false);
                }

                if (!FileSystems.Default.FileExists(fullPath))
                {
                    return new ParsedConfigFile(fullPath, exists: false);
                }

                ParsedConfigFile result = new(fullPath, exists: true);

                // DTD processing and external resolution are disabled: this file is read very early, from a
                // repository that may not be trusted, and it has no need for either.
                XmlReaderSettings settings = new()
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    IgnoreComments = true,
                    IgnoreWhitespace = true,
                    IgnoreProcessingInstructions = true,
                    CloseInput = true,
                };

                try
                {
                    // The Stream overload is required by this repository's banned-API rules.
                    using FileStream stream = File.OpenRead(fullPath);
                    using XmlReader reader = XmlReader.Create(stream, settings);
                    result.ReadDocument(reader);
                }
                catch (XmlException e)
                {
                    // A file that is not well-formed is not trusted at all: discard anything read before the
                    // error rather than applying a partial configuration.
                    result.Entries.Clear();
                    result.Problems.Add($"{fullPath}: {e.Message}");
                }
                catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
                {
                    // A config we cannot read contributes nothing and does not terminate the walk.
                    return new ParsedConfigFile(fullPath, exists: false);
                }

                return result;
            }

            private void ReadDocument(XmlReader reader)
            {
                if (!reader.ReadToFollowing(RootElementName))
                {
                    Problems.Add($"{FullPath}: expected a <{RootElementName}> root element.");
                    return;
                }

                if (reader.IsEmptyElement)
                {
                    return;
                }

                int depth = reader.Depth;
                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.EndElement && reader.Depth <= depth)
                    {
                        break;
                    }

                    if (reader.NodeType != XmlNodeType.Element || reader.Depth != depth + 1)
                    {
                        continue;
                    }

                    ReadDirective(reader);
                }
            }

            private void ReadDirective(XmlReader reader)
            {
                string directive = reader.LocalName;

                if (string.Equals(directive, AllowAttributeElementName, StringComparison.OrdinalIgnoreCase))
                {
                    AddEntry(isAttribute: true, reader.GetAttribute(ElementAttributeName), reader.GetAttribute(NameAttributeName), directive, ElementAttributeName);
                }
                else if (string.Equals(directive, AllowElementElementName, StringComparison.OrdinalIgnoreCase))
                {
                    AddEntry(isAttribute: false, reader.GetAttribute(ParentAttributeName), reader.GetAttribute(NameAttributeName), directive, ParentAttributeName);
                }
                else
                {
                    // Forward compatibility: a directive this engine does not know is not an error, because a
                    // newer MSBuild may define it. Reported so typos remain diagnosable.
                    Problems.Add($"{FullPath}: ignored unrecognized directive <{directive}>.");
                }
            }

            private void AddEntry(bool isAttribute, string? owner, string? name, string directive, string ownerAttributeName)
            {
                if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(name))
                {
                    Problems.Add($"{FullPath}: <{directive}> requires non-empty {ownerAttributeName} and {NameAttributeName} attributes.");
                    return;
                }

                Entries.Add((isAttribute, owner!.Trim(), name!.Trim()));
            }
        }
    }
}
