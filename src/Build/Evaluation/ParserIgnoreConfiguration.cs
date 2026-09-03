// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// Manages a set of allowed unknown attributes and elements that should be silently skipped
    /// during project parsing instead of throwing an InvalidProjectFileException.
    /// Configuration is loaded from Directory.Parse.config files discovered from:
    /// 1. MSBUILD_PARSE_CONFIG environment variable paths
    /// 2. Walking up from the project file's directory (passed via ProjectCollection constructor)
    /// </summary>
    internal sealed class ParserIgnoreConfiguration : ITranslatable
    {
        internal readonly struct Observation
        {
            internal Observation(string kind, string request, string? result, Exception? exception)
            {
                Kind = kind;
                Request = request;
                Result = result;
                ExceptionType = exception?.GetType().FullName;
                HResult = exception?.HResult ?? 0;
                Message = GetExceptionMessage(exception);
            }

            internal string Kind { get; }
            internal string Request { get; }
            internal string? Result { get; }
            internal string? ExceptionType { get; }
            internal int HResult { get; }
            internal string? Message { get; }

            private static string? GetExceptionMessage(Exception? exception)
            {
                try
                {
                    return exception?.Message;
                }
                catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
                {
                    return null;
                }
            }
        }

        internal const string ConfigFileName = "Directory.Parse.config";
        internal const string EnvironmentVariableName = "MSBUILD_PARSE_CONFIG";

        // Generic element names for dynamically-named elements in the config.
        // These allow rules like Attribute:Property:X to apply to all property elements.
        internal const string GenericPropertyElement = "Property";
        internal const string GenericItemElement = "Item";
        internal const string GenericItemDefinitionElement = "ItemDefinition";
        internal const string GenericMetadataElement = "Metadata";
        internal const string GenericUsingTaskBodyElement = "UsingTaskBody";

        /// <summary>
        /// Static collection of config file paths to embed in the binlog.
        /// Populated during config loading, consumed by BinaryLogger.Shutdown.
        /// </summary>
        private static ConcurrentBag<string> s_binlogEmbedPaths = new();

        /// <summary>Paths collected for binlog embedding.</summary>
        internal static IEnumerable<string> BinlogEmbedPaths => s_binlogEmbedPaths;

        /// <summary>Clears the embed paths after they've been written to the binlog.</summary>
        internal static void ClearBinlogEmbedPaths()
        {
            s_binlogEmbedPaths = new ConcurrentBag<string>();
        }

        private Dictionary<string, HashSet<string>> _allowedAttributes;
        private Dictionary<string, HashSet<string>> _allowedChildren;
        private HashSet<string> _loadedConfigFiles;
        private List<Observation>? _observations;
        private readonly ConcurrentDictionary<string, int> _skippedItems = new(StringComparer.OrdinalIgnoreCase);

        private ParserIgnoreConfiguration()
        {
            _allowedAttributes = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            _allowedChildren = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            _loadedConfigFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        private ParserIgnoreConfiguration(ITranslator translator)
            : this()
        {
            ((ITranslatable)this).Translate(translator);
        }

        internal IReadOnlyCollection<string> LoadedConfigFiles => _loadedConfigFiles;
        internal IReadOnlyList<Observation> Observations => _observations ?? [];

        internal static ParserIgnoreConfiguration Empty { get; } = new ParserIgnoreConfiguration();

        public static bool Equals(ParserIgnoreConfiguration? left, ParserIgnoreConfiguration? right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }
            
            if (left is null || right is null)
            {
                return false;
            }

            return left._loadedConfigFiles.SetEquals(right._loadedConfigFiles)
                && CollectionHelpers.DictionaryEquals(left._allowedAttributes, right._allowedAttributes, HashSet<string>.CreateSetComparer())
                && CollectionHelpers.DictionaryEquals(left._allowedChildren, right._allowedChildren, HashSet<string>.CreateSetComparer());
        }

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

            _skippedItems.Clear();

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

        internal static ParserIgnoreConfiguration Merge(ParserIgnoreConfiguration left, ParserIgnoreConfiguration right)
        {
            ArgumentNullException.ThrowIfNull(left);
            ArgumentNullException.ThrowIfNull(right);

            var merged = new ParserIgnoreConfiguration();

            UnionEntries(merged._allowedAttributes, left._allowedAttributes);
            UnionEntries(merged._allowedAttributes, right._allowedAttributes);
            UnionEntries(merged._allowedChildren, left._allowedChildren);
            UnionEntries(merged._allowedChildren, right._allowedChildren);
            merged._loadedConfigFiles.UnionWith(left._loadedConfigFiles);
            merged._loadedConfigFiles.UnionWith(right._loadedConfigFiles);
            MergeObservations(merged, left);
            MergeObservations(merged, right);
            MergeSkippedItems(merged._skippedItems, left._skippedItems);
            MergeSkippedItems(merged._skippedItems, right._skippedItems);

            return merged;
        }

        internal static ParserIgnoreConfiguration LoadFromFile(string filePath)
        {
            var config = new ParserIgnoreConfiguration();
            config.LoadFile(filePath);
            return config;
        }

        internal static ParserIgnoreConfiguration LoadGlobalConfig()
        {
            var config = new ParserIgnoreConfiguration();

            string? envValue = Environment.GetEnvironmentVariable(EnvironmentVariableName);
            config.RecordObservation("Environment", EnvironmentVariableName, envValue);
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

        internal static ParserIgnoreConfiguration FactoryForDeserialization(ITranslator translator)
        {
            return new ParserIgnoreConfiguration(translator);
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

            bool exists = FileSystems.Default.FileExists(fullPath);
            RecordObservation("FileProbe", fullPath, exists.ToString());
            if (!exists || !_loadedConfigFiles.Add(fullPath))
            {
                return;
            }

            s_binlogEmbedPaths.Add(fullPath);

            try
            {
                var settings = new System.Xml.XmlReaderSettings { DtdProcessing = System.Xml.DtdProcessing.Prohibit };
                var doc = new System.Xml.XmlDocument();
                using (Stream stream = OpenConfigStream(fullPath))
                using (var reader = System.Xml.XmlReader.Create(stream, settings))
                {
                    doc.Load(reader);
                }

                System.Xml.XmlElement? root = doc.DocumentElement;
                if (root is null || !root.Name.Equals("ParseConfig", StringComparison.OrdinalIgnoreCase))
                {
                    RecordObservation("ParseOutcome", fullPath, "ParsedUnexpectedRoot");
                    return;
                }

                RecordObservation("ParseOutcome", fullPath, "ParsedParseConfig");
                foreach (System.Xml.XmlNode child in root.ChildNodes)
                {
                    if (child.NodeType != System.Xml.XmlNodeType.Element)
                    {
                        continue;
                    }

                    Dictionary<string, HashSet<string>>? target = null;
                    if (child.Name.Equals("IgnoreAttributes", StringComparison.OrdinalIgnoreCase))
                    {
                        target = _allowedAttributes;
                    }
                    else if (child.Name.Equals("IgnoreChildren", StringComparison.OrdinalIgnoreCase))
                    {
                        target = _allowedChildren;
                    }

                    if (target is null)
                    {
                        continue;
                    }

                    foreach (System.Xml.XmlNode ignoreNode in child.ChildNodes)
                    {
                        if (ignoreNode.NodeType != System.Xml.XmlNodeType.Element
                            || !ignoreNode.Name.Equals("Ignore", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        System.Xml.XmlElement ignoreElement = (System.Xml.XmlElement)ignoreNode;
                        string elementName = ignoreElement.GetAttribute("Element");
                        string name = ignoreElement.GetAttribute("Name");

                        if (!string.IsNullOrEmpty(elementName) && !string.IsNullOrEmpty(name))
                        {
                            AddAllowedName(target, elementName, name);
                        }
                    }
                }
            }
            catch (System.Xml.XmlException ex)
            {
                RecordObservation(
                    "ParseOutcome",
                    fullPath,
                    string.Concat("MalformedXml:", ex.GetType().FullName));
            }
            catch (Exception ex)
            {
                RecordObservation("LoadFailure", fullPath, ex.GetType().FullName, ex);
            }
        }

        private Stream OpenConfigStream(string fullPath)
        {
            if (!EvaluationObservationSession.IsEnabled)
            {
                return File.OpenRead(fullPath);
            }

            byte[] content = File.ReadAllBytes(fullPath);
            RecordObservation(
                "RawContentHash",
                fullPath,
                EvaluationObservationSession.ComputeBytesHash(content));
            return new MemoryStream(content, writable: false);
        }

        internal void RecordUpwardSearch(
            string startDirectory,
            IReadOnlyList<string> candidates,
            string? selectedPath)
        {
            if (!EvaluationObservationSession.IsEnabled)
            {
                return;
            }

            RecordObservation("UpwardSearchStart", startDirectory, selectedPath);
            foreach (string candidate in candidates)
            {
                bool isSelected = !string.IsNullOrEmpty(selectedPath) &&
                    FileUtilities.PathsEqual(candidate, selectedPath!);
                RecordObservation(
                    "UpwardSearchCandidate",
                    candidate,
                    isSelected.ToString());
                if (isSelected)
                {
                    break;
                }
            }
        }

        private void RecordObservation(
            string kind,
            string request,
            string? result,
            Exception? exception = null)
        {
            if (EvaluationObservationSession.IsEnabled)
            {
                (_observations ??= []).Add(new Observation(kind, request, result, exception));
            }
        }

        private static void MergeObservations(
            ParserIgnoreConfiguration destination,
            ParserIgnoreConfiguration source)
        {
            if (source._observations is not null)
            {
                (destination._observations ??= []).AddRange(source._observations);
            }
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
