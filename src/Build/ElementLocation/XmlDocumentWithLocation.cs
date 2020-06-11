// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Xml;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Construction
{
    /// <summary>
    /// Improvement to XmlDocument that during load attaches location information to all elements and attributes.
    /// We don't need a real XmlDocument, as we are careful not to expose Xml types in our public API.
    /// </summary>
    /// <remarks>
    /// XmlDocument has many members, and this can't substitute for all of them. Location finding probably won't work if
    /// certain XmlDocument members are used. So for extra robustness, this could wrap an XmlDocument instead,
    /// and expose the small number of members that the MSBuild code actually uses. 
    /// </remarks>
    internal class XmlDocumentWithLocation : XmlDocument
    {
        /// <summary>
        /// Used to cache strings used in attribute values and comments.
        /// </summary>
        private static ProjectStringCache s_globalStringCache = new ProjectStringCache();

        /// <summary>
        /// Used to cache tag names in loaded files.
        /// </summary>
        private static NameTable s_nameTable = new XmlNameTableThreadSafe();

        /// <summary>
        /// Whether we can selectively load as read-only (eg just when in program files directory)
        /// </summary>
        private static ReadOnlyLoadFlags s_readOnlyFlags;

        /// <summary>
        /// Reader we've hooked
        /// </summary>
        private IXmlLineInfo _reader;

        /// <summary>
        /// Path to the file loaded, if any, otherwise null.
        /// Easier to intercept and store than to derive it from the XmlDocument.BaseUri property.
        /// </summary>
        private string _fullPath;

        /// <summary>
        /// Local cache of strings for attribute values and comments. Used for testing.
        /// </summary>
        private ProjectStringCache _stringCache;

        /// <summary>
        /// Whether we can expect to never save this file.
        /// In such a case, we can discard as much as possible on load, like comments and whitespace.
        /// </summary>
        private bool? _loadAsReadOnly;

        /// <summary>
        /// Constructor
        /// </summary>
        internal XmlDocumentWithLocation()
            : base(s_nameTable)
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        internal XmlDocumentWithLocation(bool? loadAsReadOnly)
            : this()
        {
            _loadAsReadOnly = loadAsReadOnly;
        }

        /// <summary>
        /// Whether to load files read only
        /// </summary>
        private enum ReadOnlyLoadFlags
        {
            /// <summary>
            /// Not determined
            /// </summary>
            Undefined,

            /// <summary>
            /// Always load writeable
            /// </summary>
            LoadAllWriteable,

            /// <summary>
            /// Always load read-only, to save memory
            /// </summary>
            LoadAllReadOnly,

            /// <summary>
            /// Load read only selectively, Eg., just when file names begin with "Microsoft."
            /// </summary>
            LoadReadOnlyIfAppropriate
        }

        /// <summary>
        /// Path to the file loaded if any, otherwise null.
        /// If the XmlDocument hasn't been loaded from a file, we wouldn't have a full path.
        /// However the project might actually have been given a path - it might even have been saved.
        /// In order to allow created elements to be able to provide a location with at least
        /// that path, the setter here should be called when the project is given a path.
        /// It may be set to null.
        /// </summary>
        internal string FullPath
        {
            get { return _fullPath; }
            set { _fullPath = value; }
        }

        /// <summary>
        /// Sets or gets the string cache used by this XmlDocument.
        /// </summary>
        /// <remarks>
        /// When a particular instance has not been set will use the global string cache. The ability
        /// to use a particular instance is useful for tests.
        /// </remarks>
        internal ProjectStringCache StringCache
        {
            get { return _stringCache ?? s_globalStringCache; }
            set { _stringCache = value; }
        }

        /// <summary>
        /// Loads from an XmlReader, intercepting the reader.
        /// </summary>
        /// <remarks>
        /// This method is called within XmlDocument by all other
        /// Load(..) overloads, and by LoadXml(..), so however the client loads XML,
        /// we will grab the reader.
        /// </remarks>
        public override void Load(XmlReader reader)
        {
            if (reader.BaseURI.Length > 0)
            {
                string adjustedLocalPath = null;

                if (Uri.TryCreate(reader.BaseURI, UriKind.RelativeOrAbsolute, out Uri uri))
                {
                    adjustedLocalPath = uri.LocalPath;
                }

                DetermineWhetherToLoadReadOnly(adjustedLocalPath);
            }

            // Set the line info source if it is available given the specific implementation of XmlReader
            // we've been given.
            _reader = reader as IXmlLineInfo;

            // This call results in calls to our CreateElement and CreateAttribute methods,
            // which use this.reader within themselves.
            base.Load(reader);

            // After load, the reader is no use for location information; it isn't updated when
            // the document is edited. So null it out, so that elements and attributes created by subsequent
            // editing don't have meaningless location information.
            _reader = null;
        }

#if FEATURE_XML_LOADPATH
        /// <summary>
        /// Grab the path to the file, for use in our location information.
        /// </summary>
        public override void Load(string fullPath)
        {
            DetermineWhetherToLoadReadOnly(fullPath);

            _fullPath = fullPath;

            using(var xtr = XmlReaderExtension.Create(fullPath, _loadAsReadOnly ?? false))
            {
                this.Load(xtr.Reader);
            }
        }
#endif

        /// <summary>
        /// Called during load, to add an element.
        /// </summary>
        /// <remarks>
        /// We create our own kind of element, that we can give location information to.
        /// </remarks>
        public override XmlElement CreateElement(string prefix, string localName, string namespaceURI)
        {
            if (_reader != null)
            {
                return new XmlElementWithLocation(prefix, localName, namespaceURI, this, _reader.LineNumber, _reader.LinePosition);
            }

            // Must be a subsequent edit; we can't provide location information
            return new XmlElementWithLocation(prefix, localName, namespaceURI, this);
        }

        /// <summary>
        /// Called during load, to add an attribute.
        /// </summary>
        /// <remarks>
        /// We create our own kind of attribute, that we can give location information to.
        /// </remarks>
        public override XmlAttribute CreateAttribute(string prefix, string localName, string namespaceURI)
        {
            if (_reader != null)
            {
                return new XmlAttributeWithLocation(prefix, localName, namespaceURI, this, _reader.LineNumber, _reader.LinePosition);
            }

            // Must be a subsequent edit; we can't provide location information
            return new XmlAttributeWithLocation(prefix, localName, namespaceURI, this);
        }

        /// <summary>
        /// Create a whitespace node.
        /// Overridden to cache attribute values.
        /// </summary>
        public override XmlWhitespace CreateWhitespace(string text)
        {
            if (_loadAsReadOnly.HasValue && _loadAsReadOnly.Value)
            {
                text = String.Empty;
            }

            string interned = StringCache.Add(text, this);
            return base.CreateWhitespace(interned);
        }

        /// <summary>
        /// Create a whitespace node. The definition of "significant" whitespace is obscure
        /// and does not include whitespace in text values in element content, which we always want to keep.
        /// Overridden to cache attribute values.
        /// </summary>
        public override XmlSignificantWhitespace CreateSignificantWhitespace(string text)
        {
            if (_loadAsReadOnly.HasValue && _loadAsReadOnly.Value)
            {
                text = String.Empty;
            }

            string interned = StringCache.Add(text, this);
            return base.CreateSignificantWhitespace(interned);
        }

        /// <summary>
        /// Create a text node.
        /// Overridden to cache attribute values.
        /// </summary>
        public override XmlText CreateTextNode(string text)
        {
            string textNode = StringCache.Add(text, this);
            return base.CreateTextNode(textNode);
        }

        /// <summary>
        /// Create a comment node.
        /// Overridden in order to cache comment strings.
        /// </summary>
        public override XmlComment CreateComment(string data)
        {
            if (_loadAsReadOnly.HasValue && _loadAsReadOnly.Value)
            {
                data = String.Empty;
            }

            string interned = StringCache.Add(data, this);
            return base.CreateComment(interned);
        }

        /// <summary>
        /// Override Save to verify file was not loaded as readonly
        /// </summary>
        public override void Save(Stream outStream)
        {
            VerifyThrowNotReadOnly();
            base.Save(outStream);
        }

#if FEATURE_XML_LOADPATH
        /// <summary>
        /// Override Save to verify file was not loaded as readonly
        /// </summary>
        public override void Save(string filename)
        {
            VerifyThrowNotReadOnly();
            base.Save(filename);
        }
#endif

        /// <summary>
        /// Override Save to verify file was not loaded as readonly
        /// </summary>
        public override void Save(TextWriter writer)
        {
            VerifyThrowNotReadOnly();
            base.Save(writer);
        }

        /// <summary>
        /// Override Save to verify file was not loaded as readonly
        /// </summary>
        public override void Save(XmlWriter writer)
        {
            VerifyThrowNotReadOnly();
            base.Save(writer);
        }

        /// <summary>
        /// Override IsReadOnly property to correctly indicate the mode to callers
        /// </summary>
        public override bool IsReadOnly => _loadAsReadOnly.GetValueOrDefault();

        /// <summary>
        /// Reset state for unit tests that want to set the env var
        /// </summary>
        internal static void ClearReadOnlyFlags_UnitTestsOnly()
        {
            s_readOnlyFlags = ReadOnlyLoadFlags.Undefined;
        }

        /// <summary>
        /// Called when the XmlDocument is unloaded to remove this XML's
        /// contribution to the string interning cache.
        /// Does NOT zombie the ProjectRootElement or anything else.
        /// </summary>
        internal void ClearAnyCachedStrings()
        {
            StringCache.Clear(this);
        }

        /// <summary>
        /// Determine whether we should load this file read only.
        /// We decide yes if it is in program files or the OS directory, and the file name starts with "microsoft", else no.
        /// We are very selective because we don't want to load files read only that the host might want to save, nor
        /// any files in which comments within property/metadata values might be significant - MSBuild does not discard those, normally.
        /// </summary>
        private void DetermineWhetherToLoadReadOnly(string fullPath)
        {
            if (_loadAsReadOnly == null)
            {
                DetermineWhetherToLoadReadOnlyIfPossible();

                if (s_readOnlyFlags == ReadOnlyLoadFlags.LoadAllReadOnly)
                {
                    _loadAsReadOnly = true;
                }
                else if (s_readOnlyFlags == ReadOnlyLoadFlags.LoadReadOnlyIfAppropriate && fullPath is object)
                {
                    // Only files from Microsoft
                    if (Path.GetFileName(fullPath).StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase))
                    {
                        // If we are loading devdiv targets, we're in razzle
                        if (Path.GetFileName(fullPath).StartsWith("Microsoft.DevDiv", StringComparison.OrdinalIgnoreCase))
                        {
                            _loadAsReadOnly = true;
                        }
                        else // Else, only load if they're in program files or windows directories
                        {
                            ErrorUtilities.VerifyThrow(Path.IsPathRooted(fullPath), "should be full path");
                            string directory = Path.GetDirectoryName(fullPath);

                            string windowsFolder = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

                            if ((!String.IsNullOrEmpty(windowsFolder) && directory.StartsWith(windowsFolder, StringComparison.OrdinalIgnoreCase)) ||
                                (!String.IsNullOrEmpty(FrameworkLocationHelper.programFiles32) && directory.StartsWith(FrameworkLocationHelper.programFiles32, StringComparison.OrdinalIgnoreCase)) ||
                                (!String.IsNullOrEmpty(FrameworkLocationHelper.programFiles64) && directory.StartsWith(FrameworkLocationHelper.programFiles64, StringComparison.OrdinalIgnoreCase)))
                            {
                                _loadAsReadOnly = true;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Determine whether we would ever load read only
        /// </summary>
        private void DetermineWhetherToLoadReadOnlyIfPossible()
        {
            if (s_readOnlyFlags == ReadOnlyLoadFlags.Undefined)
            {
                s_readOnlyFlags = ReadOnlyLoadFlags.LoadAllWriteable;

                if (String.Equals(Environment.GetEnvironmentVariable("MSBuildLoadMicrosoftTargetsReadOnly"), "true", StringComparison.OrdinalIgnoreCase))
                {
                    s_readOnlyFlags = ReadOnlyLoadFlags.LoadReadOnlyIfAppropriate;
                }

                if (String.Equals(Environment.GetEnvironmentVariable("MSBUILDLOADALLFILESASREADONLY"), "1", StringComparison.OrdinalIgnoreCase))
                {
                    s_readOnlyFlags = ReadOnlyLoadFlags.LoadAllReadOnly;
                }

                // "Escape hatch" should someone really need to edit these - since we'll be switching it on in VS and msbuild.exe wholesale.
                if (String.Equals(Environment.GetEnvironmentVariable("MSBUILDLOADALLFILESASWRITEABLE"), "1", StringComparison.OrdinalIgnoreCase))
                {
                    s_readOnlyFlags = ReadOnlyLoadFlags.LoadAllWriteable;
                }
            }
        }

        /// <summary>
        /// Throw if this was loaded read only
        /// </summary>
        private void VerifyThrowNotReadOnly()
        {
            ErrorUtilities.VerifyThrowInvalidOperation(!_loadAsReadOnly.HasValue || !_loadAsReadOnly.Value, "OM_CannotSaveFileLoadedAsReadOnly", _fullPath);
        }
    }
}
