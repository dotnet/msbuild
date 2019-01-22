// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Resources {

    using System.Diagnostics;
    using System.Reflection;
    using System;
    using System.Windows.Forms;    
    using Microsoft.Win32;
    using System.Drawing;
    using System.IO;
    using System.Text;
    using System.ComponentModel;
    using System.Collections;
    using System.Resources;
    using System.Xml;
    using System.Runtime.Serialization;
    using System.Diagnostics.CodeAnalysis;

    /// <include file='doc\ResXResourceWriter.uex' path='docs/doc[@for="ResXResourceWriter"]/*' />
    /// <devdoc>
    ///     ResX resource writer. See the text in "ResourceSchema" for more 
    ///     information.
    /// </devdoc>
    public class ResXResourceWriter : IResourceWriter {
        internal const string TypeStr = "type";
        internal const string NameStr = "name";
        internal const string DataStr = "data";
        internal const string MetadataStr = "metadata";
        internal const string MimeTypeStr = "mimetype";
        internal const string ValueStr = "value";
        internal const string ResHeaderStr = "resheader";
        internal const string VersionStr = "version";
        internal const string ResMimeTypeStr = "resmimetype";
        internal const string ReaderStr = "reader";
        internal const string WriterStr = "writer";
        internal const string CommentStr = "comment";
        internal const string AssemblyStr ="assembly";
        internal const string AliasStr= "alias" ;

        private Hashtable cachedAliases;

        private static TraceSwitch ResValueProviderSwitch = new TraceSwitch("ResX", "Debug the resource value provider");

        // 







        internal static readonly string Beta2CompatSerializedObjectMimeType = "text/microsoft-urt/psuedoml-serialized/base64";

        // These two "compat" mimetypes are here. In Beta 2 and RTM we used the term "URT"
        // internally to refer to parts of the .NET Framework. Since these references
        // will be in Beta 2 ResX files, and RTM ResX files for customers that had 
        // early access to releases, we don't want to break that. We will read 
        // and parse these types correctly in version 1.0, but will always 
        // write out the new version. So, opening and editing a ResX file in VS will
        // update it to the new types.
        //
        internal static readonly string CompatBinSerializedObjectMimeType = "text/microsoft-urt/binary-serialized/base64";
        internal static readonly string CompatSoapSerializedObjectMimeType = "text/microsoft-urt/soap-serialized/base64";

        /// <include file='doc\ResXResourceWriter.uex' path='docs/doc[@for="ResXResourceWriter.BinSerializedObjectMimeType"]/*' />
        /// <internalonly/>
        public static readonly string BinSerializedObjectMimeType = "application/x-microsoft.net.object.binary.base64";
        /// <include file='doc\ResXResourceWriter.uex' path='docs/doc[@for="ResXResourceWriter.SoapSerializedObjectMimeType"]/*' />
        /// <internalonly/>
        public static readonly string SoapSerializedObjectMimeType = "application/x-microsoft.net.object.soap.base64";
        /// <include file='doc\ResXResourceWriter.uex' path='docs/doc[@for="ResXResourceWriter.DefaultSerializedObjectMimeType"]/*' />
        /// <internalonly/>
        public static readonly string DefaultSerializedObjectMimeType = BinSerializedObjectMimeType;
        /// <include file='doc\ResXResourceWriter.uex' path='docs/doc[@for="ResXResourceWriter.ByteArraySerializedObjectMimeType"]/*' />
        /// <internalonly/>
        public static readonly string ByteArraySerializedObjectMimeType = "application/x-microsoft.net.object.bytearray.base64";
        /// <include file='doc\ResXResourceWriter.uex' path='docs/doc[@for="ResXResourceWriter.ResMimeType"]/*' />
        /// <internalonly/>
        public static readonly string ResMimeType = "text/microsoft-resx";
        /// <include file='doc\ResXResourceWriter.uex' path='docs/doc[@for="ResXResourceWriter.Version"]/*' />
        public static readonly string Version = "2.0";

        /// <include file='doc\ResXResourceWriter.uex' path='docs/doc[@for="ResXResourceWriter.ResourceSchema"]/*' />
        /// <internalonly/>
        public static readonly string ResourceSchema = @"
    <!-- 
    Microsoft ResX Schema 
    
    Version " + Version + @"
    
    The primary goals of this format is to allow a simple XML format 
    that is mostly human readable. The generation and parsing of the 
    various data types are done through the TypeConverter classes 
    associated with the data types.
    
    Example:
    
    ... ado.net/XML headers & schema ...
    <resheader name=""resmimetype"">text/microsoft-resx</resheader>
    <resheader name=""version"">" + Version + @"</resheader>
    <resheader name=""reader"">System.Resources.ResXResourceReader, System.Windows.Forms, ...</resheader>
    <resheader name=""writer"">System.Resources.ResXResourceWriter, System.Windows.Forms, ...</resheader>
    <data name=""Name1""><value>this is my long string</value><comment>this is a comment</comment></data>
    <data name=""Color1"" type=""System.Drawing.Color, System.Drawing"">Blue</data>
    <data name=""Bitmap1"" mimetype=""" + BinSerializedObjectMimeType + @""">
        <value>[base64 mime encoded serialized .NET Framework object]</value>
    </data>
    <data name=""Icon1"" type=""System.Drawing.Icon, System.Drawing"" mimetype=""" + ByteArraySerializedObjectMimeType + @""">
        <value>[base64 mime encoded string representing a byte array form of the .NET Framework object]</value>
        <comment>This is a comment</comment>
    </data>
                
    There are any number of ""resheader"" rows that contain simple 
    name/value pairs.
    
    Each data row contains a name, and value. The row also contains a 
    type or mimetype. Type corresponds to a .NET class that support 
    text/value conversion through the TypeConverter architecture. 
    Classes that don't support this are serialized and stored with the 
    mimetype set.
    
    The mimetype is used for serialized objects, and tells the 
    ResXResourceReader how to depersist the object. This is currently not 
    extensible. For a given mimetype the value must be set accordingly:
    
    Note - " + BinSerializedObjectMimeType + @" is the format 
    that the ResXResourceWriter will generate, however the reader can 
    read any of the formats listed below.
    
    mimetype: " + BinSerializedObjectMimeType + @"
    value   : The object must be serialized with 
            : System.Runtime.Serialization.Formatters.Binary.BinaryFormatter
            : and then encoded with base64 encoding.
    
    mimetype: " + SoapSerializedObjectMimeType + @"
    value   : The object must be serialized with 
            : System.Runtime.Serialization.Formatters.Soap.SoapFormatter
            : and then encoded with base64 encoding.

    mimetype: " + ByteArraySerializedObjectMimeType + @"
    value   : The object must be serialized into a byte array 
            : using a System.ComponentModel.TypeConverter
            : and then encoded with base64 encoding.
    -->
    <xsd:schema id=""root"" xmlns="""" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:msdata=""urn:schemas-microsoft-com:xml-msdata"">
        <xsd:import namespace=""http://www.w3.org/XML/1998/namespace""/>
        <xsd:element name=""root"" msdata:IsDataSet=""true"">
            <xsd:complexType>
                <xsd:choice maxOccurs=""unbounded"">
                    <xsd:element name=""metadata"">
                        <xsd:complexType>
                            <xsd:sequence>
                            <xsd:element name=""value"" type=""xsd:string"" minOccurs=""0""/>
                            </xsd:sequence>
                            <xsd:attribute name=""name"" use=""required"" type=""xsd:string""/>
                            <xsd:attribute name=""type"" type=""xsd:string""/>
                            <xsd:attribute name=""mimetype"" type=""xsd:string""/>
                            <xsd:attribute ref=""xml:space""/>                            
                        </xsd:complexType>
                    </xsd:element>
                    <xsd:element name=""assembly"">
                      <xsd:complexType>
                        <xsd:attribute name=""alias"" type=""xsd:string""/>
                        <xsd:attribute name=""name"" type=""xsd:string""/>
                      </xsd:complexType>
                    </xsd:element>
                    <xsd:element name=""data"">
                        <xsd:complexType>
                            <xsd:sequence>
                                <xsd:element name=""value"" type=""xsd:string"" minOccurs=""0"" msdata:Ordinal=""1"" />
                                <xsd:element name=""comment"" type=""xsd:string"" minOccurs=""0"" msdata:Ordinal=""2"" />
                            </xsd:sequence>
                            <xsd:attribute name=""name"" type=""xsd:string"" use=""required"" msdata:Ordinal=""1"" />
                            <xsd:attribute name=""type"" type=""xsd:string"" msdata:Ordinal=""3"" />
                            <xsd:attribute name=""mimetype"" type=""xsd:string"" msdata:Ordinal=""4"" />
                            <xsd:attribute ref=""xml:space""/>
                        </xsd:complexType>
                    </xsd:element>
                    <xsd:element name=""resheader"">
                        <xsd:complexType>
                            <xsd:sequence>
                                <xsd:element name=""value"" type=""xsd:string"" minOccurs=""0"" msdata:Ordinal=""1"" />
                            </xsd:sequence>
                            <xsd:attribute name=""name"" type=""xsd:string"" use=""required"" />
                        </xsd:complexType>
                    </xsd:element>
                </xsd:choice>
            </xsd:complexType>
        </xsd:element>
        </xsd:schema>
        ";
        
        IFormatter binaryFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter ();
        string fileName;
        Stream stream;
        TextWriter textWriter;
        XmlTextWriter xmlTextWriter;
        string basePath;

        bool hasBeenSaved;
        bool initialized;

        private Func<Type, string> typeNameConverter; // no public property to be consistent with ResXDataNode class.
        
        /// <include file='doc\ResXResourceWriter.uex' path='docs/doc[@for="ResXResourceWriter.BasePath"]/*' />
        /// <devdoc>
        ///     Base Path for ResXFileRefs.
        /// </devdoc>
        public string BasePath {
            get {
                return basePath;
            }
            set {
                basePath = value;
            }
        }

        /// <include file='doc\ResXResourceWriter.uex' path='docs/doc[@for="ResXResourceWriter.ResXResourceWriter"]/*' />
        /// <devdoc>
        ///     Creates a new ResXResourceWriter that will write to the specified file.
        /// </devdoc>
        public ResXResourceWriter(string fileName) {
            this.fileName = fileName;
        }
        public ResXResourceWriter(string fileName, Func<Type, string> typeNameConverter) {
            this.fileName = fileName;
            this.typeNameConverter = typeNameConverter;
        }

        /// <include file='doc\ResXResourceWriter.uex' path='docs/doc[@for="ResXResourceWriter.ResXResourceWriter1"]/*' />
        /// <devdoc>
        ///     Creates a new ResXResourceWriter that will write to the specified stream.
        /// </devdoc>
        public ResXResourceWriter(Stream stream) {
            this.stream = stream;
        }
        public ResXResourceWriter(Stream stream, Func<Type, string> typeNameConverter) {
            this.stream = stream;
            this.typeNameConverter = typeNameConverter;
        }

        /// <include file='doc\ResXResourceWriter.uex' path='docs/doc[@for="ResXResourceWriter.ResXResourceWriter2"]/*' />
        /// <devdoc>
        ///     Creates a new ResXResourceWriter that will write to the specified TextWriter.
        /// </devdoc>
        public ResXResourceWriter(TextWriter textWriter) {
            this.textWriter = textWriter;
        }
        public ResXResourceWriter(TextWriter textWriter, Func<Type, string> typeNameConverter) {
            this.textWriter = textWriter;
            this.typeNameConverter = typeNameConverter;
        }

        /// <include file='doc\ResXResourceWriter.uex' path='docs/doc[@for="ResXResourceWriter.Finalize"]/*' />
        ~ResXResourceWriter() {
            Dispose(false);
        }

        private void InitializeWriter() {
            if (xmlTextWriter == null) {
                // 

                bool writeHeaderRequired = false;

                if (textWriter != null) {
                    textWriter.WriteLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
                    writeHeaderRequired = true;

                    xmlTextWriter = new XmlTextWriter(textWriter);
                }
                else if (stream != null) {
                    xmlTextWriter = new XmlTextWriter(stream, System.Text.Encoding.UTF8);
                }
                else {
                    Debug.Assert(fileName != null, "Nothing to output to");
                    xmlTextWriter = new XmlTextWriter(fileName, System.Text.Encoding.UTF8);
                }
                xmlTextWriter.Formatting = Formatting.Indented;
                xmlTextWriter.Indentation = 2;

                if (!writeHeaderRequired) {
                    xmlTextWriter.WriteStartDocument(); // writes <?xml version="1.0" encoding="utf-8"?>
                }
            }
            else {
                xmlTextWriter.WriteStartDocument();
            }

            xmlTextWriter.WriteStartElement("root");
            XmlTextReader reader = new XmlTextReader(new StringReader(ResourceSchema));
            reader.WhitespaceHandling = WhitespaceHandling.None;
            xmlTextWriter.WriteNode(reader, true);

            xmlTextWriter.WriteStartElement(ResHeaderStr); {
                xmlTextWriter.WriteAttributeString(NameStr, ResMimeTypeStr);
                xmlTextWriter.WriteStartElement(ValueStr); {
                    xmlTextWriter.WriteString(ResMimeType);
                }
                xmlTextWriter.WriteEndElement();
            }
            xmlTextWriter.WriteEndElement();
            xmlTextWriter.WriteStartElement(ResHeaderStr); {
                xmlTextWriter.WriteAttributeString(NameStr, VersionStr);
                xmlTextWriter.WriteStartElement(ValueStr); {
                    xmlTextWriter.WriteString(Version);
                }
                xmlTextWriter.WriteEndElement();
            }
            xmlTextWriter.WriteEndElement();
            xmlTextWriter.WriteStartElement(ResHeaderStr); {
                xmlTextWriter.WriteAttributeString(NameStr, ReaderStr);
                xmlTextWriter.WriteStartElement(ValueStr); {
                    xmlTextWriter.WriteString(MultitargetUtil.GetAssemblyQualifiedName(typeof(ResXResourceReader), this.typeNameConverter));
                }
                xmlTextWriter.WriteEndElement();
            }
            xmlTextWriter.WriteEndElement();
            xmlTextWriter.WriteStartElement(ResHeaderStr); {
                xmlTextWriter.WriteAttributeString(NameStr, WriterStr);
                xmlTextWriter.WriteStartElement(ValueStr); {
                    xmlTextWriter.WriteString(MultitargetUtil.GetAssemblyQualifiedName(typeof(ResXResourceWriter), this.typeNameConverter));
                }
                xmlTextWriter.WriteEndElement();
            }
            xmlTextWriter.WriteEndElement();

            initialized = true;
        }

        private XmlWriter Writer {
            get {
                if (!initialized) {
                    InitializeWriter();
                }
                return xmlTextWriter;
            }
        }

        /// <include file='doc\ResXResourceWriter.uex' path='docs/doc[@for="ResXResourceWriter.AddAlias"]/*' />
        /// <devdoc>
        ///    Adds aliases to the resource file...
        /// </devdoc>
        public virtual void AddAlias(string aliasName, AssemblyName assemblyName) {
           if (assemblyName == null) {
               throw new ArgumentNullException(nameof(assemblyName));
           }

           if (cachedAliases == null) {
               cachedAliases = new Hashtable();
           }

           cachedAliases[assemblyName.FullName] = aliasName; 
       }


        /// <include file='doc\ResXResourceWriter.uex' path='docs/doc[@for="ResXResourceWriter.AddMetadata"]/*' />
        /// <devdoc>
        ///    Adds the given value to the collection of metadata.  These name/value pairs 
        ///    will be emitted to the <metadata> elements in the .resx file.
        /// </devdoc>
        public void AddMetadata(string name, byte[] value) {
            AddDataRow(MetadataStr, name, value);
        }

        /// <include file='doc\ResXResourceWriter.uex' path='docs/doc[@for="ResXResourceWriter.AddMetadata1"]/*' />
        /// <devdoc>
        ///    Adds the given value to the collection of metadata.  These name/value pairs 
        ///    will be emitted to the <metadata> elements in the .resx file.
        /// </devdoc>
        public void AddMetadata(string name, string value) {
            AddDataRow(MetadataStr, name, value);
        }

        /// <include file='doc\ResXResourceWriter.uex' path='docs/doc[@for="ResXResourceWriter.AddMetadata2"]/*' />
        /// <devdoc>
        ///    Adds the given value to the collection of metadata.  These name/value pairs 
        ///    will be emitted to the <metadata> elements in the .resx file.
        /// </devdoc>
        public void AddMetadata(string name, object value) {
            AddDataRow(MetadataStr, name, value);
        }

        /// <include file='doc\ResXResourceWriter.uex' path='docs/doc[@for="ResXResourceWriter.AddResource"]/*' />
        /// <devdoc>
        ///     Adds a blob resource to the resources.
        /// </devdoc>
        // NOTE: Part of IResourceWriter - not protected by class level LinkDemand.
        public void AddResource(string name, byte[] value) {
            AddDataRow(DataStr, name, value);
        }

        /// <include file='doc\ResXResourceWriter.uex' path='docs/doc[@for="ResXResourceWriter.AddResource1"]/*' />
        /// <devdoc>
        ///     Adds a resource to the resources. If the resource is a string,
        ///     it will be saved that way, otherwise it will be serialized
        ///     and stored as in binary.
        /// </devdoc>
        // NOTE: Part of IResourceWriter - not protected by class level LinkDemand.
        public void AddResource(string name, object value) {
            if (value is ResXDataNode) {
                AddResource((ResXDataNode)value);
            }
            else {
                AddDataRow(DataStr, name, value);
            }
        }

        /// <include file='doc\ResXResourceWriter.uex' path='docs/doc[@for="ResXResourceWriter.AddResource2"]/*' />
        /// <devdoc>
        ///     Adds a string resource to the resources.
        /// </devdoc>
        // NOTE: Part of IResourceWriter - not protected by class level LinkDemand.
        public void AddResource(string name, string value) {
            AddDataRow(DataStr, name, value);
        }

         /// <include file='doc\ResXResourceWriter.uex' path='docs/doc[@for="ResXResourceWriter.AddResource3"]/*' />
        /// <devdoc>
        ///     Adds a string resource to the resources.
        /// </devdoc>
        public void AddResource(ResXDataNode node) {
            // we're modifying the node as we're adding it to the resxwriter
            // this is BAD, so we clone it. adding it to a writer doesnt change it
            // we're messing with a copy
            ResXDataNode nodeClone = node.DeepClone();
            
            ResXFileRef fileRef = nodeClone.FileRef;
            string modifiedBasePath = BasePath;
            
            if (!string.IsNullOrEmpty(modifiedBasePath)) {
                if (!(modifiedBasePath.EndsWith("\\")))
                {
                    modifiedBasePath += "\\";
                }
                if (fileRef != null) {
                    fileRef.MakeFilePathRelative(modifiedBasePath);
                }
            }
            DataNodeInfo info = nodeClone.GetDataNodeInfo();
            AddDataRow(DataStr, info.Name, info.ValueData, info.TypeName, info.MimeType, info.Comment);
        }

        /// <devdoc>
        ///     Adds a blob resource to the resources.
        /// </devdoc>
        private void AddDataRow(string elementName, string name, byte[] value) {
            AddDataRow(elementName, name, ToBase64WrappedString(value), TypeNameWithAssembly(typeof(byte[])), null, null);
        }

        /// <devdoc>
        ///     Adds a resource to the resources. If the resource is a string,
        ///     it will be saved that way, otherwise it will be serialized
        ///     and stored as in binary.
        /// </devdoc>
        private void AddDataRow(string elementName, string name, object value) {
            Debug.WriteLineIf(ResValueProviderSwitch.TraceVerbose, "  resx: adding resource " + name);
            if (value is string) {
                AddDataRow(elementName, name, (string)value);
            }
            else if (value is byte[]) {
                AddDataRow(elementName, name, (byte[])value);
            }
            else if(value is ResXFileRef) {
                ResXFileRef fileRef = (ResXFileRef)value;
                ResXDataNode node = new ResXDataNode(name, fileRef, this.typeNameConverter);
                if (fileRef != null) {
                    fileRef.MakeFilePathRelative(BasePath);
                }
                DataNodeInfo info = node.GetDataNodeInfo();
                AddDataRow(elementName, info.Name, info.ValueData, info.TypeName, info.MimeType, info.Comment);
            } else {
                ResXDataNode node = new ResXDataNode(name, value, this.typeNameConverter);
                DataNodeInfo info = node.GetDataNodeInfo();
                AddDataRow(elementName, info.Name, info.ValueData, info.TypeName, info.MimeType, info.Comment);
            }
        }        

        /// <devdoc>
        ///     Adds a string resource to the resources.
        /// </devdoc>
        private void AddDataRow(string elementName, string name, string value) {
            if(value == null) {
                // if it's a null string, set it here as a resxnullref
                AddDataRow(elementName, name, value, MultitargetUtil.GetAssemblyQualifiedName(typeof(ResXNullRef), this.typeNameConverter), null, null);                
            } else {
                AddDataRow(elementName, name, value, null, null, null);
            }
        }

        /// <devdoc>
        ///     Adds a new row to the Resources table. This helper is used because
        ///     we want to always late bind to the columns for greater flexibility.
        /// </devdoc>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        private void AddDataRow(string elementName, string name, string value, string type, string mimeType, string comment) {
            if (hasBeenSaved)
                throw new InvalidOperationException(SR.ResXResourceWriterSaved);
            
            string alias = null;
            if (!string.IsNullOrEmpty(type) && elementName == DataStr)
            {
                string assemblyName = GetFullName(type);
                if(string.IsNullOrEmpty(assemblyName)) {
                    try {
                        Type typeObject = Type.GetType(type);
                        if(typeObject == typeof(string)) {
                            type = null;
                        } else if(typeObject != null) {
                            assemblyName = GetFullName(MultitargetUtil.GetAssemblyQualifiedName(typeObject, this.typeNameConverter));
                            alias = GetAliasFromName(new AssemblyName(assemblyName));
                        }
                    } catch {
                    }
                } else {
                    alias = GetAliasFromName(new AssemblyName(GetFullName(type)));
                }
                //AddAssemblyRow(AssemblyStr, alias, GetFullName(type));
            }
            
            Writer.WriteStartElement(elementName); {
                Writer.WriteAttributeString(NameStr, name);
                
                if (!string.IsNullOrEmpty(alias) && !string.IsNullOrEmpty(type) && elementName == DataStr) {
                     // CHANGE: we still output version information. This might have
                    // to change in 3.2
                    string typeName = GetTypeName(type);
                    string typeValue = typeName + ", " + alias;
                    Writer.WriteAttributeString(TypeStr, typeValue);
                }
                else {
                     if (type != null)
                     {
                        Writer.WriteAttributeString(TypeStr, type);
                     }
                }

                if (mimeType != null) {
                    Writer.WriteAttributeString(MimeTypeStr, mimeType);
                }
                
                if((type == null && mimeType == null) || (type != null && type.StartsWith("System.Char", StringComparison.Ordinal))) {
                    Writer.WriteAttributeString("xml", "space", null, "preserve");
                }
                
                Writer.WriteStartElement(ValueStr); {
                    if(!string.IsNullOrEmpty(value)) {
                        Writer.WriteString(value);
                    }
                }
                Writer.WriteEndElement();
                if(!string.IsNullOrEmpty(comment)) {
                    Writer.WriteStartElement(CommentStr); {
                        Writer.WriteString(comment);
                    }
                    Writer.WriteEndElement();
                }
            }
            Writer.WriteEndElement();
        }


        private void AddAssemblyRow(string elementName, string alias, string name)
        {

            Writer.WriteStartElement(elementName); {
                if (!string.IsNullOrEmpty(alias)) {
                      Writer.WriteAttributeString(AliasStr, alias);
                }
            
                if (!string.IsNullOrEmpty(name)) {
                    Writer.WriteAttributeString(NameStr, name);
                }
                //Writer.WriteEndElement();
            }
            Writer.WriteEndElement();
        }

        private string GetAliasFromName(AssemblyName assemblyName)
        {
             if (cachedAliases == null)
            {
                cachedAliases = new Hashtable();
            }
            string alias =  (string) cachedAliases[assemblyName.FullName]; 
            if (string.IsNullOrEmpty(alias))
            {
                alias =  assemblyName.Name;
                AddAlias(alias, assemblyName);               
                AddAssemblyRow(AssemblyStr, alias, assemblyName.FullName);
            }
            return alias;
        }

        /// <include file='doc\ResXResourceWriter.uex' path='docs/doc[@for="ResXResourceWriter.Close"]/*' />
        /// <devdoc>
        ///     Closes any files or streams locked by the writer.
        /// </devdoc>
        // NOTE: Part of IResourceWriter - not protected by class level LinkDemand.
        public void Close() {
            Dispose();
        }

        /// <include file='doc\ResXResourceWriter.uex' path='docs/doc[@for="ResXResourceWriter.Dispose"]/*' />
        // NOTE: Part of IDisposable - not protected by class level LinkDemand.
        public virtual void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <include file='doc\ResXResourceWriter.uex' path='docs/doc[@for="ResXResourceWriter.Dispose1"]/*' />
        protected virtual void Dispose(bool disposing) {
            if (disposing) {
                if (!hasBeenSaved) {
                    Generate();
                }
                if (xmlTextWriter != null) {
                    xmlTextWriter.Close();
                    xmlTextWriter = null;
                }
                if (stream != null) {
                    stream.Close();
                    stream = null;
                }
                if (textWriter != null) {
                    textWriter.Close();
                    textWriter = null;
                }
            }
        }

        private string GetTypeName(string typeName) {
             int indexStart = typeName.IndexOf(",");
             return ((indexStart == -1) ? typeName : typeName.Substring(0, indexStart));
        }


        private string GetFullName(string typeName) {
             int indexStart = typeName.IndexOf(",");
             if(indexStart == -1)
                return null;
             return typeName.Substring(indexStart + 2);
        }    

        static string ToBase64WrappedString(byte[] data) {
            const int lineWrap = 80;
            const string crlf = "\r\n";
            const string prefix = "        ";
            string raw = Convert.ToBase64String(data);
            if (raw.Length > lineWrap) {
                StringBuilder output = new StringBuilder(raw.Length + (raw.Length / lineWrap) * 3); // word wrap on lineWrap chars, \r\n
                int current = 0;
                for (; current < raw.Length - lineWrap; current+=lineWrap) {
                    output.Append(crlf);
                    output.Append(prefix);
                    output.Append(raw, current, lineWrap);
                }
                output.Append(crlf);
                output.Append(prefix);
                output.Append(raw, current, raw.Length - current);
                output.Append(crlf);
                return output.ToString();
            }
            else {
                return raw;
            }
        }

        private string TypeNameWithAssembly(Type type) {
            // 











            string result = MultitargetUtil.GetAssemblyQualifiedName(type, this.typeNameConverter);
            return result;
        }

        /// <include file='doc\ResXResourceWriter.uex' path='docs/doc[@for="ResXResourceWriter.Generate"]/*' />
        /// <devdoc>
        ///     Writes the resources out to the file or stream.
        /// </devdoc>
        // NOTE: Part of IResourceWriter - not protected by class level LinkDemand.
        public void Generate() {
            if (hasBeenSaved)
                throw new InvalidOperationException(SR.ResXResourceWriterSaved);

            hasBeenSaved = true;
            Debug.WriteLineIf(ResValueProviderSwitch.TraceVerbose, "writing XML");

            Writer.WriteEndElement();
            Writer.Flush();

            Debug.WriteLineIf(ResValueProviderSwitch.TraceVerbose, "done");
        }
    }
}



