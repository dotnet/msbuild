// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Serialization;

namespace Microsoft.Build.Tasks.Deployment.ManifestUtilities
{
    /// <summary>
    /// Describes a manifest file reference.
    /// </summary>
    [ComVisible(false)]
    public sealed class FileReference : BaseReference
    {
        private ComClass[] _comClasses;
        private string _writeableType;
        private ProxyStub[] _proxyStubs;
        private TypeLib[] _typeLibs;

        /// <summary>
        /// Initializes a new instance of the FileReference class.
        /// </summary>
        public FileReference()
        {
        }

        /// <summary>
        /// Initializes a new instance of the FileReference class.
        /// </summary>
        /// <param name="path">The specified source path of the file.</param>
        public FileReference(string path) : base(path)
        {
        }

        /// <summary>
        /// Specifies the set of COM classes referenced by the manifest for isolated applications and Reg-Free COM.
        /// </summary>
        [XmlIgnore]
        public ComClass[] ComClasses => _comClasses;

        internal bool ImportComComponent(string path, OutputMessageCollection outputMessages, string outputDisplayName)
        {
            var importer = new ComImporter(path, outputMessages, outputDisplayName);
            if (importer.Success)
            {
                var typeLibs = new List<TypeLib>();

                // Add TypeLib objects from importer...
                if (_typeLibs != null)
                {
                    typeLibs.AddRange(_typeLibs);
                }

                if (importer.TypeLib != null)
                {
                    typeLibs.Add(importer.TypeLib);
                }
                _typeLibs = typeLibs.ToArray();

                // Add ComClass objects from importer...
                var comClasses = new List<ComClass>();
                if (_comClasses != null)
                {
                    comClasses.AddRange(_comClasses);
                }

                if (importer.ComClasses != null)
                {
                    comClasses.AddRange(importer.ComClasses);
                }
                _comClasses = comClasses.ToArray();
            }
            return importer.Success;
        }

        /// <summary>
        /// Specifies whether the file is a data file.
        /// </summary>
        [XmlIgnore]
        public bool IsDataFile
        {
            get => String.Compare(_writeableType, "applicationData", StringComparison.OrdinalIgnoreCase) == 0;
            set => _writeableType = value ? "applicationData" : null;
        }

        /// <summary>
        /// Specifies the set of proxy stubs referenced by the manifest for isolated applications and Reg-Free COM.
        /// </summary>
        [XmlIgnore]
        public ProxyStub[] ProxyStubs => _proxyStubs;

        protected internal override string SortName => TargetPath;

        /// <summary>
        /// Specifies the set of type libraries referenced by the manifest.
        /// </summary>
        [XmlIgnore]
        public TypeLib[] TypeLibs => _typeLibs;

        #region " XmlSerializer "

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlArray("ComClasses")]
        public ComClass[] XmlComClasses
        {
            get => _comClasses;
            set => _comClasses = value;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlArray("ProxyStubs")]
        public ProxyStub[] XmlProxyStubs
        {
            get => _proxyStubs;
            set => _proxyStubs = value;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlArray("TypeLibs")]
        public TypeLib[] XmlTypeLibs
        {
            get => _typeLibs;
            set => _typeLibs = value;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("WriteableType")]
        public string XmlWriteableType
        {
            get => _writeableType;
            set => _writeableType = value;
        }

        #endregion
    }

    [ComVisible(false)]
    public class ComClass
    {
        private string _clsid;
        private string _description;
        private string _progid;
        private string _threadingModel;
        private string _tlbid;

        public ComClass()
        {
        }

        internal ComClass(Guid tlbId, Guid clsId, string progId, string threadingModel, string description)
        {
            _tlbid = tlbId.ToString("B");
            _clsid = clsId.ToString("B");
            _progid = progId;
            _threadingModel = threadingModel;
            _description = description;
        }

        [XmlIgnore]
        public string ClsId => _clsid;

        [XmlIgnore]
        public string Description => _description;

        [XmlIgnore]
        public string ProgId => _progid;

        [XmlIgnore]
        public string ThreadingModel => _threadingModel;

        [XmlIgnore]
        public string TlbId => _tlbid;

        #region " XmlSerializer "

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("Clsid")]
        public string XmlClsId
        {
            get => _clsid;
            set => _clsid = value;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("Description")]
        public string XmlDescription
        {
            get => _description;
            set => _description = value;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("Progid")]
        public string XmlProgId
        {
            get => _progid;
            set => _progid = value;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("ThreadingModel")]
        public string XmlThreadingModel
        {
            get => _threadingModel;
            set => _threadingModel = value;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("Tlbid")]
        public string XmlTlbId
        {
            get => _tlbid;
            set => _tlbid = value;
        }

        #endregion
    }

    [ComVisible(false)]
    public class TypeLib
    {
        private string _flags;
        private string _helpDirectory;
        private string _resourceid;
        private string _tlbid;
        private string _version;

        public TypeLib()
        {
        }

        internal TypeLib(Guid tlbId, Version version, string helpDirectory, int resourceId, int flags)
        {
            _tlbid = tlbId.ToString("B");
            _version = version.ToString(2);
            _helpDirectory = helpDirectory;
            _resourceid = Convert.ToString(resourceId, 16);
            _flags = FlagsFromInt(flags);
        }

        [XmlIgnore]
        public string Flags => _flags;

        private static string FlagsFromInt(int flags)
        {
            var sb = new StringBuilder();
            if ((flags & 1) != 0)
            {
                sb.Append("RESTRICTED,");
            }

            if ((flags & 2) != 0)
            {
                sb.Append("CONTROL,");
            }

            if ((flags & 4) != 0)
            {
                sb.Append("HIDDEN,");
            }

            if ((flags & 8) != 0)
            {
                sb.Append("HASDISKIMAGE,");
            }
            return sb.ToString().TrimEnd(',');
        }

        [XmlIgnore]
        public string HelpDirectory => _helpDirectory;

        [XmlIgnore]
        public string ResourceId => _resourceid;

        [XmlIgnore]
        public string TlbId => _tlbid;

        [XmlIgnore]
        public string Version => _version;

        #region " XmlSerializer "

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("Flags")]
        public string XmlFlags
        {
            get => _flags;
            set => _flags = value;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("HelpDir")]
        public string XmlHelpDirectory
        {
            get => _helpDirectory;
            set => _helpDirectory = value;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("ResourceId")]
        public string XmlResourceId
        {
            get => _resourceid;
            set => _resourceid = value;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("Tlbid")]
        public string XmlTlbId
        {
            get => _tlbid;
            set => _tlbid = value;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("Version")]
        public string XmlVersion
        {
            get => _version;
            set => _version = value;
        }

        #endregion
    }

    [ComVisible(false)]
    public class WindowClass
    {
        private string _name;
        private string _versioned;

        public WindowClass()
        {
        }

        public WindowClass(string name, bool versioned)
        {
            _name = name;
            _versioned = versioned ? "yes" : "no";
        }

        [XmlIgnore]
        public string Name => _name;

        [XmlIgnore]
        public bool Versioned
        {
            get
            {
                if (String.Compare(_versioned, "yes", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return true;
                }

                if (String.Compare(_versioned, "no", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return false;
                }
                return true;
            }
        }

        #region " XmlSerializer "

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("Name")]
        public string XmlName
        {
            get => _name;
            set => _name = value;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("Versioned")]
        public string XmlVersioned
        {
            get => _versioned;
            set => _versioned = value;
        }

        #endregion
    }

    [ComVisible(false)]
    public class ProxyStub
    {
        private string _baseInterface;
        private string _iid;
        private string _name;
        private string _numMethods;
        private string _tlbid;

        [XmlIgnore]
        public string BaseInterface => _baseInterface;

        [XmlIgnore]
        public string IID => _iid;

        [XmlIgnore]
        public string Name => _name;

        [XmlIgnore]
        public string NumMethods => _numMethods;

        [XmlIgnore]
        public string TlbId => _tlbid;

        #region " XmlSerializer "

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("BaseInterface")]
        public string XmlBaseInterface
        {
            get => _baseInterface;
            set => _baseInterface = value;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("Iid")]
        public string XmlIID
        {
            get => _iid;
            set => _iid = value;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("Name")]
        public string XmlName
        {
            get => _name;
            set => _name = value;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("NumMethods")]
        public string XmlNumMethods
        {
            get => _numMethods;
            set => _numMethods = value;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("Tlbid")]
        public string XmlTlbId
        {
            get => _tlbid;
            set => _tlbid = value;
        }

        #endregion
    }
}
