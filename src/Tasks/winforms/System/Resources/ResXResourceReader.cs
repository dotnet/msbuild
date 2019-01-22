// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Resources {

    using System.Diagnostics;
    using System.Runtime.Serialization;
    using System;
    using System.Windows.Forms;
    using System.Reflection;
    using Microsoft.Win32;
    using System.Drawing;
    using System.IO;
    using System.Text;
    using System.ComponentModel;
    using System.Collections;
    using System.Collections.Specialized;
    using System.Runtime.CompilerServices;
    using System.Resources;
    using System.Xml;
    using System.ComponentModel.Design;
    using System.Globalization;
    
    /// <include file='doc\ResXResourceReader.uex' path='docs/doc[@for="ResXResourceReader"]/*' />
    /// <devdoc>
    ///     ResX resource reader.
    /// </devdoc>
    internal class ResXResourceReader : IResourceReader {
        string fileName = null;
        TextReader reader = null;
        Stream stream = null;
        string fileContents = null;
        AssemblyName[] assemblyNames;
        string basePath;
        bool isReaderDirty = false;

        ITypeResolutionService typeResolver;
        IAliasResolver aliasResolver =null;

        ListDictionary resData = null;
        ListDictionary resMetadata = null;
        string resHeaderVersion = null;
        string resHeaderMimeType = null;
        string resHeaderReaderType = null;
        string resHeaderWriterType = null;
        bool useResXDataNodes = false;


        private ResXResourceReader(ITypeResolutionService typeResolver) {
            this.typeResolver = typeResolver;
            this.aliasResolver = new ReaderAliasResolver();
        }

        private ResXResourceReader(AssemblyName[] assemblyNames) {
            this.assemblyNames = assemblyNames;
            this.aliasResolver = new ReaderAliasResolver();
        }


        public ResXResourceReader(string fileName) : this(fileName, (ITypeResolutionService)null, (IAliasResolver)null) {
        }
        public ResXResourceReader(string fileName, ITypeResolutionService typeResolver) : this(fileName, typeResolver, (IAliasResolver)null) {
        }
        internal ResXResourceReader(string fileName, ITypeResolutionService typeResolver, IAliasResolver aliasResolver) {
            this.fileName = fileName;
            this.typeResolver = typeResolver;
            this.aliasResolver = aliasResolver;
            if(this.aliasResolver == null) {
                 this.aliasResolver = new ReaderAliasResolver();
            }
        }

        public ResXResourceReader(TextReader reader) : this(reader, (ITypeResolutionService)null, (IAliasResolver)null) {
        }
        public ResXResourceReader(TextReader reader, ITypeResolutionService typeResolver) : this(reader, typeResolver, (IAliasResolver)null) {
        }
        internal ResXResourceReader(TextReader reader, ITypeResolutionService typeResolver, IAliasResolver aliasResolver) {
            this.reader = reader;
            this.typeResolver = typeResolver;
            this.aliasResolver = aliasResolver;
            if(this.aliasResolver == null) {
                 this.aliasResolver = new ReaderAliasResolver();
            }
        }
        

        public ResXResourceReader(Stream stream) : this(stream, (ITypeResolutionService)null, (IAliasResolver)null) {
        }
        public ResXResourceReader(Stream stream, ITypeResolutionService typeResolver) : this(stream, typeResolver, (IAliasResolver)null) {
        }
        internal ResXResourceReader(Stream stream, ITypeResolutionService typeResolver, IAliasResolver aliasResolver) {
            this.stream = stream;
            this.typeResolver = typeResolver;
            this.aliasResolver = aliasResolver;
            if(this.aliasResolver == null) {
                 this.aliasResolver = new ReaderAliasResolver();
            }
        }

        public ResXResourceReader(Stream stream, AssemblyName[] assemblyNames) : this(stream, assemblyNames, (IAliasResolver)null){
        }
        internal ResXResourceReader(Stream stream, AssemblyName[] assemblyNames, IAliasResolver aliasResolver) {
            this.stream = stream;
            this.assemblyNames = assemblyNames;
            this.aliasResolver = aliasResolver;
            if(this.aliasResolver == null) {
                 this.aliasResolver = new ReaderAliasResolver();
            }
        }

        public ResXResourceReader(TextReader reader, AssemblyName[] assemblyNames) : this(reader, assemblyNames, (IAliasResolver)null){
        }
        internal ResXResourceReader(TextReader reader, AssemblyName[] assemblyNames, IAliasResolver aliasResolver) {
            this.reader = reader;
            this.assemblyNames = assemblyNames;
            this.aliasResolver = aliasResolver;
            if(this.aliasResolver == null) {
                 this.aliasResolver = new ReaderAliasResolver();
            }
        }

        public ResXResourceReader(string fileName, AssemblyName[] assemblyNames) : this(fileName, assemblyNames, (IAliasResolver)null){
        }
        internal ResXResourceReader(string fileName, AssemblyName[] assemblyNames, IAliasResolver aliasResolver) {
            this.fileName = fileName;
            this.assemblyNames = assemblyNames;
            this.aliasResolver = aliasResolver;
            if(this.aliasResolver == null) {
                 this.aliasResolver = new ReaderAliasResolver();
            }
        }

        

        /// <include file='doc\ResXResourceReader.uex' path='docs/doc[@for=".Finalize"]/*' />
        ~ResXResourceReader() {
            Dispose(false);
        }

        /// <include file='doc\ResXResourceReader.uex' path='docs/doc[@for="ResXResourceReader.BasePath"]/*' />
        /// <devdoc>
        ///     BasePath for relatives filepaths with ResXFileRefs.
        /// </devdoc>
        public string BasePath {
            get {
                return basePath;
            }
            set {
                if(isReaderDirty) {
                    throw new InvalidOperationException(SR.InvalidResXBasePathOperation);
                }
                basePath = value;
            }
        }

        /// <include file='doc\ResXResourceReader.uex' path='docs/doc[@for="ResXResourceReader.ResXResourceReader5"]/*' />
        /// <devdoc>
        ///     ResXFileRef's TypeConverter automatically unwraps it, creates the referenced
        ///     object and returns it. This property gives the user control over whether this unwrapping should
        ///     happen, or a ResXFileRef object should be returned. Default is true for backward compat and common case
        ///     scenario.
        /// </devdoc>
        public bool UseResXDataNodes {
            get {
                return this.useResXDataNodes;
            }
            set {
                if(isReaderDirty) {
                    throw new InvalidOperationException(SR.InvalidResXBasePathOperation);
                }
                this.useResXDataNodes = value;
            }
        }

        /// <include file='doc\ResXResourceReader.uex' path='docs/doc[@for="ResXResourceReader.Close"]/*' />
        /// <devdoc>
        ///     Closes and files or streams being used by the reader.
        /// </devdoc>
        // NOTE: Part of IResourceReader - not protected by class level LinkDemand.
        public void Close() {
            ((IDisposable)this).Dispose();
        }

        /// <include file='doc\ResXResourceReader.uex' path='docs/doc[@for="ResXResourceReader.IDisposable.Dispose"]/*' />
        /// <internalonly/>
        // NOTE: Part of IDisposable - not protected by class level LinkDemand.
        void IDisposable.Dispose() {
            GC.SuppressFinalize(this);
            Dispose(true);
        }

        /// <include file='doc\ResXResourceReader.uex' path='docs/doc[@for="ResXResourceReader.Dispose"]/*' />
        protected virtual void Dispose(bool disposing) {
            if (disposing) {
                if (fileName != null && stream != null) {
                    stream.Close();
                    stream = null;
                }

                if (reader != null) {
                    reader.Close();
                    reader = null;
                }
            }
        }

        private void SetupNameTable(XmlReader reader) {
            reader.NameTable.Add(ResXResourceWriter.TypeStr);
            reader.NameTable.Add(ResXResourceWriter.NameStr);
            reader.NameTable.Add(ResXResourceWriter.DataStr);
            reader.NameTable.Add(ResXResourceWriter.MetadataStr);
            reader.NameTable.Add(ResXResourceWriter.MimeTypeStr);
            reader.NameTable.Add(ResXResourceWriter.ValueStr);
            reader.NameTable.Add(ResXResourceWriter.ResHeaderStr);
            reader.NameTable.Add(ResXResourceWriter.VersionStr);
            reader.NameTable.Add(ResXResourceWriter.ResMimeTypeStr);
            reader.NameTable.Add(ResXResourceWriter.ReaderStr);
            reader.NameTable.Add(ResXResourceWriter.WriterStr);
            reader.NameTable.Add(ResXResourceWriter.BinSerializedObjectMimeType);
            reader.NameTable.Add(ResXResourceWriter.SoapSerializedObjectMimeType);
            reader.NameTable.Add(ResXResourceWriter.AssemblyStr);
            reader.NameTable.Add(ResXResourceWriter.AliasStr);
        }

        /// <devdoc>
        ///     Demand loads the resource data.
        /// </devdoc>
        private void EnsureResData() {
            if (resData == null) {
                resData = new ListDictionary();
                resMetadata = new ListDictionary();

                XmlTextReader contentReader = null;

                try {
                    // Read data in any which way
                    if (fileContents != null) {
                        contentReader = new XmlTextReader(new StringReader(fileContents));
                    }
                    else if (reader != null) {
                        contentReader = new XmlTextReader(reader);
                    }
                    else if (fileName != null || stream != null) {
                        if (stream == null) {
                            stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
                        }

                        contentReader = new XmlTextReader(stream);
                    }

                    SetupNameTable(contentReader);
                    contentReader.WhitespaceHandling = WhitespaceHandling.None;
                    ParseXml(contentReader);
                }
                finally {
                    if (fileName != null && stream != null) {
                        stream.Close();
                        stream = null;
                    }
                }
            }
        }                                



        /// <include file='doc\ResXResourceReader.uex' path='docs/doc[@for="ResXResourceReader.FromFileContents"]/*' />
        /// <devdoc>
        ///     Creates a reader with the specified file contents.
        /// </devdoc>
        public static ResXResourceReader FromFileContents(string fileContents) {
            return FromFileContents(fileContents, (ITypeResolutionService)null);
        }

        /// <include file='doc\ResXResourceReader.uex' path='docs/doc[@for="ResXResourceReader.FromFileContents1"]/*' />
        /// <internalonly/>
        /// <devdoc>
        ///     Creates a reader with the specified file contents.
        /// </devdoc>
        public static ResXResourceReader FromFileContents(string fileContents, ITypeResolutionService typeResolver) {
            ResXResourceReader result = new ResXResourceReader(typeResolver);
            result.fileContents = fileContents;
            return result;
        }

        /// <include file='doc\ResXResourceReader.uex' path='docs/doc[@for="ResXResourceReader.FromFileContents1"]/*' />
        /// <internalonly/>
        /// <devdoc>
        ///     Creates a reader with the specified file contents.
        /// </devdoc>
        public static ResXResourceReader FromFileContents(string fileContents, AssemblyName[] assemblyNames) {
            ResXResourceReader result = new ResXResourceReader(assemblyNames);
            result.fileContents = fileContents;
            return result;
        }

        /// <include file='doc\ResXResourceReader.uex' path='docs/doc[@for="ResXResourceReader.IEnumerable.GetEnumerator"]/*' />
        /// <internalonly/>
        // NOTE: Part of IEnumerable - not protected by class level LinkDemand.
        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        /// <include file='doc\ResXResourceReader.uex' path='docs/doc[@for="ResXResourceReader.GetEnumerator"]/*' />
        // NOTE: Part of IResourceReader - not protected by class level LinkDemand.
        public IDictionaryEnumerator GetEnumerator() {
            isReaderDirty = true;
            EnsureResData();
            return resData.GetEnumerator();
        }

        /// <include file='doc\ResXResourceReader.uex' path='docs/doc[@for="ResXResourceReader.GetMetadataEnumerator"]/*' />
        /// <devdoc>
        ///    Returns a dictionary enumerator that can be used to enumerate the <metadata> elements in the .resx file.
        /// </devdoc>
        public IDictionaryEnumerator GetMetadataEnumerator() {
            EnsureResData();
            return resMetadata.GetEnumerator();
        }

        /// <devdoc>
        ///    Attempts to return the line and column (Y, X) of the XML reader.
        /// </devdoc>
        private Point GetPosition(XmlReader reader) {
            Point pt = new Point(0, 0);
            IXmlLineInfo lineInfo = reader as IXmlLineInfo;

            if (lineInfo != null) {
                pt.Y = lineInfo.LineNumber;
                pt.X = lineInfo.LinePosition;
            }

            return pt;
        }


        private void ParseXml(XmlTextReader reader) {
            bool success = false;
            try {
                try {
                    while (reader.Read()) {
                        if (reader.NodeType == XmlNodeType.Element) {
                            string s = reader.LocalName;
                            
                            if (reader.LocalName.Equals(ResXResourceWriter.AssemblyStr)) {
                                ParseAssemblyNode(reader, false);
                            }
                            else if (reader.LocalName.Equals(ResXResourceWriter.DataStr)) {
                                ParseDataNode(reader, false);
                            }
                            else if (reader.LocalName.Equals(ResXResourceWriter.ResHeaderStr)) {
                                ParseResHeaderNode(reader);
                            }
                            else if (reader.LocalName.Equals(ResXResourceWriter.MetadataStr)) {
                                ParseDataNode(reader, true);
                            }
                        }
                    }

                    success = true;
                }
                catch (SerializationException se) {
                    Point pt = GetPosition(reader);
                    string newMessage = string.Format(SR.SerializationException, reader[ResXResourceWriter.TypeStr], pt.Y, pt.X, se.Message);
                    XmlException xml = new XmlException(newMessage, se, pt.Y, pt.X);
                    SerializationException newSe = new SerializationException(newMessage, xml);

                    throw newSe;
                }
                catch (TargetInvocationException tie) {
                    Point pt = GetPosition(reader);
                    string newMessage = string.Format(SR.InvocationException, reader[ResXResourceWriter.TypeStr], pt.Y, pt.X, tie.InnerException.Message);
                    XmlException xml = new XmlException(newMessage, tie.InnerException, pt.Y, pt.X);
                    TargetInvocationException newTie = new TargetInvocationException(newMessage, xml);

                    throw newTie;
                }
                catch (XmlException e) {
                    throw new ArgumentException(string.Format(SR.InvalidResXFile, e.Message), e);
                }
                catch (Exception e) {
                    if (ClientUtils.IsSecurityOrCriticalException(e)) {
                        throw;
                    } else {
                        Point pt = GetPosition(reader);
                        XmlException xmlEx = new XmlException(e.Message, e, pt.Y, pt.X);
                        throw new ArgumentException(string.Format(SR.InvalidResXFile, xmlEx.Message), xmlEx);
                    }
                }
            }
            finally {
                if (!success) {
                    resData = null;
                    resMetadata = null;
                }
            }

            bool validFile = false;

            if (object.Equals(resHeaderMimeType, ResXResourceWriter.ResMimeType)) {

                Type readerType = typeof(ResXResourceReader);
                Type writerType = typeof(ResXResourceWriter);

                string readerTypeName = resHeaderReaderType;
                string writerTypeName = resHeaderWriterType;
                if (readerTypeName != null &&readerTypeName.IndexOf(',') != -1) {
                    readerTypeName = readerTypeName.Split(new char[] {','})[0].Trim();
                }
                if (writerTypeName != null && writerTypeName.IndexOf(',') != -1) {
                    writerTypeName = writerTypeName.Split(new char[] {','})[0].Trim();
                }

                if (readerTypeName != null && 
                    writerTypeName != null && 
                    readerTypeName.Equals(readerType.FullName) && 
                    writerTypeName.Equals(writerType.FullName)) {
                    validFile = true;
                }
            }

            if (!validFile) {
                resData = null;
                resMetadata = null;
                throw new ArgumentException(SR.InvalidResXFileReaderWriterTypes);
            }
        }

        private void ParseResHeaderNode(XmlReader reader) {
            string name = reader[ResXResourceWriter.NameStr];
            if (name != null) {
                reader.ReadStartElement();

                // The "1.1" schema requires the correct casing of the strings
                // in the resheader, however the "1.0" schema had a different
                // casing. By checking the Equals first, we should 
                // see significant performance improvements.
                //

                if (object.Equals(name, ResXResourceWriter.VersionStr)) {
                    if (reader.NodeType == XmlNodeType.Element) {
                        resHeaderVersion = reader.ReadElementString();
                    }
                    else {
                        resHeaderVersion = reader.Value.Trim();
                    }
                }
                else if (object.Equals(name, ResXResourceWriter.ResMimeTypeStr)) {
                    if (reader.NodeType == XmlNodeType.Element) {
                        resHeaderMimeType = reader.ReadElementString();
                    }
                    else {
                        resHeaderMimeType = reader.Value.Trim();
                    }
                }
                else if (object.Equals(name, ResXResourceWriter.ReaderStr)) {
                    if (reader.NodeType == XmlNodeType.Element) {
                        resHeaderReaderType = reader.ReadElementString();
                    }
                    else {
                        resHeaderReaderType = reader.Value.Trim();
                    }
                }
                else if (object.Equals(name, ResXResourceWriter.WriterStr)) {
                    if (reader.NodeType == XmlNodeType.Element) {
                        resHeaderWriterType = reader.ReadElementString();
                    }
                    else {
                        resHeaderWriterType = reader.Value.Trim();
                    }
                }
                else {
                    switch (name.ToLower(CultureInfo.InvariantCulture)) {
                        case ResXResourceWriter.VersionStr:
                            if (reader.NodeType == XmlNodeType.Element) {
                                resHeaderVersion = reader.ReadElementString();
                            }
                            else {
                                resHeaderVersion = reader.Value.Trim();
                            }
                            break;
                        case ResXResourceWriter.ResMimeTypeStr:
                            if (reader.NodeType == XmlNodeType.Element) {
                                resHeaderMimeType = reader.ReadElementString();
                            }
                            else {
                                resHeaderMimeType = reader.Value.Trim();
                            }
                            break;
                        case ResXResourceWriter.ReaderStr:
                            if (reader.NodeType == XmlNodeType.Element) {
                                resHeaderReaderType = reader.ReadElementString();
                            }
                            else {
                                resHeaderReaderType = reader.Value.Trim();
                            }
                            break;
                        case ResXResourceWriter.WriterStr:
                            if (reader.NodeType == XmlNodeType.Element) {
                                resHeaderWriterType = reader.ReadElementString();
                            }
                            else {
                                resHeaderWriterType = reader.Value.Trim();
                            }
                            break;
                    }
                }
            }
        }

        private void ParseAssemblyNode(XmlReader reader, bool isMetaData)
        {
            string alias = reader[ResXResourceWriter.AliasStr];
            string typeName = reader[ResXResourceWriter.NameStr];

            AssemblyName assemblyName = new AssemblyName(typeName);
            
            if (string.IsNullOrEmpty(alias)) {
                alias = assemblyName.Name;
            }
            aliasResolver.PushAlias(alias, assemblyName);
        }
        

        private void ParseDataNode(XmlTextReader reader, bool isMetaData) {
            DataNodeInfo nodeInfo = new DataNodeInfo();
            
            nodeInfo.Name = reader[ResXResourceWriter.NameStr];
            string typeName = reader[ResXResourceWriter.TypeStr];

            string alias = null;
            AssemblyName assemblyName = null;
            
            if (!string.IsNullOrEmpty(typeName)) {
                alias  = GetAliasFromTypeName(typeName);
            }
            if (!string.IsNullOrEmpty(alias)) {
                assemblyName = aliasResolver.ResolveAlias(alias);
            }
            if (assemblyName != null )
            {
                nodeInfo.TypeName = GetTypeFromTypeName(typeName) + ", " + assemblyName.FullName;
            }
            else {
                nodeInfo.TypeName = reader[ResXResourceWriter.TypeStr];
            }
            
            nodeInfo.MimeType = reader[ResXResourceWriter.MimeTypeStr];

            bool finishedReadingDataNode = false;
            nodeInfo.ReaderPosition = GetPosition(reader);
            while(!finishedReadingDataNode && reader.Read()) {
                if(reader.NodeType == XmlNodeType.EndElement && ( reader.LocalName.Equals(ResXResourceWriter.DataStr) || reader.LocalName.Equals(ResXResourceWriter.MetadataStr) )) {
                    // we just found </data>, quit or </metadata>
                    finishedReadingDataNode = true;
                } else {
                    // could be a <value> or a <comment>
                    if (reader.NodeType == XmlNodeType.Element) {
                        if (reader.Name.Equals(ResXResourceWriter.ValueStr)) {
                            WhitespaceHandling oldValue = reader.WhitespaceHandling;
                            try {
                                // based on the documentation at http://msdn.microsoft.com/library/default.asp?url=/library/en-us/cpref/html/frlrfsystemxmlxmltextreaderclasswhitespacehandlingtopic.asp 
                                // this is ok because:
                                // "Because the XmlTextReader does not have DTD information available to it,
                                // SignificantWhitepsace nodes are only returned within the an xml:space='preserve' scope." 
                                // the xml:space would not be present for anything else than string and char (see ResXResourceWriter)
                                // so this would not cause any breaking change while reading data from Everett (we never outputed
                                // xml:space then) or from whidbey that is not specifically either a string or a char.
                                // However please note that manually editing a resx file in Everett and in Whidbey because of the addition
                                // of xml:space=preserve might have different consequences...
                                reader.WhitespaceHandling = WhitespaceHandling.Significant;
                                nodeInfo.ValueData = reader.ReadString();
                            } finally {
                                reader.WhitespaceHandling = oldValue;
                            }
                        } else if (reader.Name.Equals(ResXResourceWriter.CommentStr)) {
                            nodeInfo.Comment = reader.ReadString();
                        }
                    } else {
                        // weird, no <xxxx> tag, just the inside of <data> as text
                        nodeInfo.ValueData = reader.Value.Trim();
                    }
                }
            }            

            if (nodeInfo.Name==null) {
                throw new ArgumentException(string.Format(SR.InvalidResXResourceNoName, nodeInfo.ValueData));
            }

            ResXDataNode dataNode = new ResXDataNode(nodeInfo, BasePath);

            if(UseResXDataNodes) {
                resData[nodeInfo.Name] = dataNode;
            } else {
                IDictionary data = (isMetaData ? resMetadata : resData);
                if(assemblyNames == null) {
                    data[nodeInfo.Name] = dataNode.GetValue(typeResolver);
                } else {
                    data[nodeInfo.Name] = dataNode.GetValue(assemblyNames);
                }
            }
        }

        private string GetAliasFromTypeName(string typeName) {
             
             int indexStart = typeName.IndexOf(",");
             return typeName.Substring(indexStart + 2); 
        
        }

        private string GetTypeFromTypeName(string typeName) {
             
             int indexStart = typeName.IndexOf(",");
             return typeName.Substring(0, indexStart); 
        
        }


        private sealed class ReaderAliasResolver : IAliasResolver {
            private Hashtable cachedAliases;

            internal ReaderAliasResolver() {
                this.cachedAliases = new Hashtable();
            }

            public AssemblyName ResolveAlias(string alias) {

                AssemblyName result = null;
                if(cachedAliases != null) {
                    result = (AssemblyName)cachedAliases[alias];
                } 
                return result;
            }

            public void PushAlias(string alias, AssemblyName name) {
                if (this.cachedAliases != null && !string.IsNullOrEmpty(alias)) {
                    cachedAliases[alias] = name;
                }
            }
            
        }
    }
}

