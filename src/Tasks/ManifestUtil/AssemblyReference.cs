// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Xml.Serialization;

namespace Microsoft.Build.Tasks.Deployment.ManifestUtilities
{
    /// <summary>
    /// Describes the type of an assembly reference.
    /// </summary>
    public enum AssemblyReferenceType
    {
        /// <summary>
        /// Assembly type is unspecified and will be determined by the UpdateFileInfo method.
        /// </summary>
        Unspecified,
        /// <summary>
        /// Specifies a ClickOnce manifest.
        /// </summary>
        ClickOnceManifest,
        /// <summary>
        /// Specifies a .NET assembly.
        /// </summary>
        ManagedAssembly,
        /// <summary>
        /// Specifies a Win32 native assembly.
        /// </summary>
        NativeAssembly
    };

    /// <summary>
    /// Describes a manifest assembly reference.
    /// </summary>
    [ComVisible(false)]
    public sealed class AssemblyReference : BaseReference
    {
        private AssemblyIdentity _assemblyIdentity = null;
        private bool _isPrerequisite = false;
        private AssemblyReferenceType _referenceType = AssemblyReferenceType.Unspecified;
        private bool _isPrimary = false;

        /// <summary>
        /// Initializes a new instance of the AssemblyReference class.
        /// </summary>
        public AssemblyReference()
        {
        }

        /// <summary>
        /// Initializes a new instance of the AssemblyReference class.
        /// </summary>
        /// <param name="path">The specified source path of the file.</param>
        public AssemblyReference(string path) : base(path)
        {
        }

        /// <summary>
        /// Specifies the identity of the assembly reference.
        /// </summary>
        [XmlIgnore]
        public AssemblyIdentity AssemblyIdentity
        {
            get { return _assemblyIdentity; }
            set { _assemblyIdentity = value; }
        }

        /// <summary>
        /// Specifies whether the assembly reference is a prerequisite.
        /// </summary>
        [XmlIgnore]
        public bool IsPrerequisite
        {
            get { return _isPrerequisite; }
            set { _isPrerequisite = value; }
        }

        [XmlIgnore]
        internal bool IsVirtual
        {
            get
            {
                if (AssemblyIdentity == null)
                    return false;
                if (String.Equals(AssemblyIdentity.Name, Constants.CLRPlatformAssemblyName, StringComparison.OrdinalIgnoreCase))
                    return true;
                else
                    return false;
            }
        }

        /// <summary>
        /// Specifies the type of the assembly reference.
        /// </summary>
        [XmlIgnore]
        public AssemblyReferenceType ReferenceType
        {
            get { return _referenceType; }
            set { _referenceType = value; }
        }

        /// <summary>
        /// True if the reference is specified in the project file, false if it is added to the manifest as a result
        /// of computing the closure of all project references.
        /// </summary>
        [XmlIgnore]
        internal bool IsPrimary
        {
            get { return _isPrimary; }
            set { _isPrimary = value; }
        }

        protected internal override string SortName
        {
            get
            {
                if (_assemblyIdentity == null)
                    return null;
                string name = _assemblyIdentity.ToString();
                if (IsVirtual)
                    name = "1: " + name; // virtual assemblies are first
                else if (_isPrerequisite)
                    name = "2: " + name; // prerequisites are second
                else
                    name = "3: " + name + ", " + TargetPath; // eveything else...
                return name;
            }
        }

        public override string ToString()
        {
            string str = base.ToString();
            if (!String.IsNullOrEmpty(str))
                return str;
            if (_assemblyIdentity != null)
                return _assemblyIdentity.ToString();
            return String.Empty;
        }

        #region " XmlSerializer "

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlElement("AssemblyIdentity")]
        public AssemblyIdentity XmlAssemblyIdentity
        {
            get { return _assemblyIdentity; }
            set { _assemblyIdentity = value; }
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("IsNative")]
        public string XmlIsNative
        {
            get { return _referenceType == AssemblyReferenceType.NativeAssembly ? "true" : "false"; }
            set { _referenceType = ConvertUtil.ToBoolean(value) ? AssemblyReferenceType.NativeAssembly : AssemblyReferenceType.ManagedAssembly; }
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("IsPrerequisite")]
        public string XmlIsPrerequisite
        {
            get { return Convert.ToString(_isPrerequisite, CultureInfo.InvariantCulture).ToLower(CultureInfo.InvariantCulture); }
            set { _isPrerequisite = ConvertUtil.ToBoolean(value); }
        }

        #endregion
    }
}
