// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;
using FrameworkNameVersioning = System.Runtime.Versioning.FrameworkName;

namespace Microsoft.Build.Tasks.Deployment.ManifestUtilities
{
    /// <summary>
    /// Describes the identity of an assembly.
    /// </summary>
    /// <remarks>This is a serialization format, do not remove or change the private fields.</remarks>
    [ComVisible(false)]
    [XmlRoot("AssemblyIdentity")]
    public sealed class AssemblyIdentity
    {
        /// <summary>
        /// Specifies which attributes are to be returned by the GetFullName function.
        /// </summary>
        [Flags]
        public enum FullNameFlags
        {
            /// <summary>
            /// Include the Name, Version, Culture, and PublicKeyToken attributes.
            /// </summary>
            Default = 0x0000,
            /// <summary>
            /// Include the Name, Version, Culture, PublicKeyToken, and ProcessorArchitecture attributes.
            /// </summary>
            ProcessorArchitecture = 0x0001,
            /// <summary>
            /// Include the Name, Version, Culture, PublicKeyToken, and Type attributes.
            /// </summary>
            Type = 0x0002,
            /// <summary>
            /// Include all attributes.
            /// </summary>
            All = 0x0003
        }

        private string _name;
        private string _version;
        private string _publicKeyToken;
        private string _culture;
        private string _processorArchitecture;
        private string _type;

        /// <summary>
        /// Initializes a new instance of the AssemblyIdentity class.
        /// </summary>
        public AssemblyIdentity()
        {
        }

        /// <summary>
        /// Initializes a new instance of the AssemblyIdentity class.
        /// </summary>
        /// <param name="name">Specifies the simple name of the assembly.</param>
        public AssemblyIdentity(string name)
        {
            _name = name;
        }

        /// <summary>
        /// Initializes a new instance of the AssemblyIdentity class.
        /// </summary>
        /// <param name="name">Specifies the simple name of the assembly.</param>
        /// <param name="version">Specifies the version of the assembly.</param>
        public AssemblyIdentity(string name, string version)
        {
            _name = name;
            _version = version;
        }

        /// <summary>
        /// Initializes a new instance of the AssemblyIdentity class.
        /// </summary>
        /// <param name="name">Specifies the simple name of the assembly.</param>
        /// <param name="version">Specifies the version of the assembly.</param>
        /// <param name="publicKeyToken">Specifies the public key token of the assembly, which is the last 8 bytes of the SHA-1 hash of the public key under which the assembly is signed.</param>
        /// <param name="culture">Specifies the culture of the assembly. A blank string indicates the invariant culture.</param>
        public AssemblyIdentity(string name, string version, string publicKeyToken, string culture)
        {
            _name = name;
            _version = version;
            _publicKeyToken = publicKeyToken;
            _culture = culture;
        }

        /// <summary>
        /// Initializes a new instance of the AssemblyIdentity class.
        /// </summary>
        /// <param name="name">Specifies the simple name of the assembly.</param>
        /// <param name="version">Specifies the version of the assembly.</param>
        /// <param name="publicKeyToken">Specifies the public key token of the assembly, which is the last 8 bytes of the SHA-1 hash of the public key under which the assembly is signed.</param>
        /// <param name="culture">Specifies the culture of the assembly. A blank string indicates the invariant culture.</param>
        /// <param name="processorArchitecture">Specifies the processor architecture of the assembly. Valid values are "msil", "x86", "ia64", "amd64".</param>
        public AssemblyIdentity(string name, string version, string publicKeyToken, string culture, string processorArchitecture)
        {
            _name = name;
            _version = version;
            _publicKeyToken = publicKeyToken;
            _culture = culture;
            _processorArchitecture = processorArchitecture;
        }

        /// <summary>
        /// Initializes a new instance of the AssemblyIdentity class.
        /// </summary>
        /// <param name="name">Specifies the simple name of the assembly.</param>
        /// <param name="version">Specifies the version of the assembly.</param>
        /// <param name="publicKeyToken">Specifies the public key token of the assembly, which is the last 8 bytes of the SHA-1 hash of the public key under which the assembly is signed.</param>
        /// <param name="culture">Specifies the culture of the assembly. A blank string indicates the invariant culture.</param>
        /// <param name="processorArchitecture">Specifies the processor architecture of the assembly. Valid values are "msil", "x86", "ia64", "amd64".</param>
        /// <param name="type">Specifies the type attribute of the assembly. Valid values are "win32" or a blank string.</param>
        public AssemblyIdentity(string name, string version, string publicKeyToken, string culture, string processorArchitecture, string type)
        {
            _name = name;
            _version = version;
            _publicKeyToken = publicKeyToken;
            _culture = culture;
            _processorArchitecture = processorArchitecture;
            _type = type;
        }

        /// <summary>
        /// Initializes a new instance of the AssemblyIdentity class.
        /// </summary>
        /// <param name="identity">Specifies another instance to duplicate.</param>
        public AssemblyIdentity(AssemblyIdentity identity)
        {
            if (identity == null)
                return;
            _name = identity._name;
            _version = identity._version;
            _publicKeyToken = identity._publicKeyToken;
            _culture = identity._culture;
            _processorArchitecture = identity._processorArchitecture;
            _type = identity._type;
        }

        /// <summary>
        /// Parses string to obtain an assembly identity.
        /// Returns null if identity could not be obtained.
        /// </summary>
        /// <param name="assemblyName">The full name of the assembly, also known as the display name.</param>
        /// <returns>The resulting assembly identity.</returns>
        public static AssemblyIdentity FromAssemblyName(string assemblyName)
        {
            // NOTE: We're not using System.Reflection.AssemblyName class here because we need ProcessorArchitecture and Type attributes.
            Regex re = new Regex("^(?<name>[^,]*)(, Version=(?<version>[^,]*))?(, Culture=(?<culture>[^,]*))?(, PublicKeyToken=(?<pkt>[^,]*))?(, ProcessorArchitecture=(?<pa>[^,]*))?(, Type=(?<type>[^,]*))?");
            Match m = re.Match(assemblyName);
            string name = m.Result("${name}");
            string version = m.Result("${version}");
            string publicKeyToken = m.Result("${pkt}");
            string culture = m.Result("${culture}");
            string processorArchitecture = m.Result("${pa}");
            string type = m.Result("${type}");
            return new AssemblyIdentity(name, version, publicKeyToken, culture, processorArchitecture, type);
        }

        /// <summary>
        /// Obtains identity of the specified manifest file.
        /// File must be a stand-alone xml manifest file.
        /// Returns null if identity could not be obtained.
        /// </summary>
        /// <param name="path">The name of the file from which the identity is to be obtained.</param>
        /// <returns>The assembly identity of the specified file.</returns>
        public static AssemblyIdentity FromManifest(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            var document = new XmlDocument();
            try
            {
                var readerSettings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore };
                using (XmlReader xmlReader = XmlReader.Create(path, readerSettings))
                {
                    document.Load(xmlReader);
                }
            }
            catch (XmlException)
            {
                return null;
            }

            return FromManifest(document);
        }

        private static AssemblyIdentity FromManifest(Stream s)
        {
            var document = new XmlDocument();
            var xrSettings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore };
            try
            {
                using (XmlReader xr = XmlReader.Create(s, xrSettings))
                {
                    document.Load(xr);
                }
            }
            catch (XmlException)
            {
                return null;
            }
            return FromManifest(document);
        }

        private static AssemblyIdentity FromManifest(XmlDocument document)
        {
            XmlNamespaceManager nsmgr = XmlNamespaces.GetNamespaceManager(document.NameTable);
            var element = (XmlElement)document.SelectSingleNode(XPaths.assemblyIdentityPath, nsmgr);
            if (element == null)
            {
                return null;
            }

            XmlNode node = element.Attributes.GetNamedItem("name");
            string name = node?.Value;
            node = element.Attributes.GetNamedItem("version");
            string version = node?.Value;
            node = element.Attributes.GetNamedItem("publicKeyToken");
            string publicKeyToken = node?.Value;
            node = element.Attributes.GetNamedItem("language");
            string culture = node?.Value;
            node = element.Attributes.GetNamedItem("processorArchitecture");
            string processorArchitecture = node?.Value;
            node = element.Attributes.GetNamedItem("type");
            string type = node?.Value;
            return new AssemblyIdentity(name, version, publicKeyToken, culture, processorArchitecture, type);
        }

        /// <summary>
        /// Obtains identity of the specified .NET assembly.
        /// File must be a .NET assembly.
        /// Returns null if identity could not be obtained.
        /// </summary>
        /// <param name="path">The name of the file from which the identity is to be obtained.</param>
        /// <returns>The assembly identity of the specified file.</returns>
        public static AssemblyIdentity FromManagedAssembly(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            // NOTE: We're not using System.Reflection.AssemblyName class here because we need ProcessorArchitecture
            using (MetadataReader r = MetadataReader.Create(path))
            {
                AssemblyIdentity identity = null;
                if (r != null)
                {
                    try
                    {
                        identity = new AssemblyIdentity(r.Name, r.Version, r.PublicKeyToken, r.Culture, r.ProcessorArchitecture);
                    }
                    catch (ArgumentException e)
                    {
                        if (e.HResult != unchecked((int)0x80070057))
                        {
                            throw;
                        }
                        // 0x80070057 - "Value does not fall within the expected range." is returned from 
                        // GetAssemblyIdentityFromFile for WinMD components
                    }
                }
                return identity;
            }
        }

        /// <summary>
        /// Obtains identity of the specified native assembly.
        /// File must be either a PE with an embedded xml manifest, or a stand-alone xml manifest file.
        /// Returns null if identity could not be obtained.
        /// </summary>
        /// <param name="path">The name of the file from which the identity is to be obtained.</param>
        /// <returns>The assembly identity of the specified file.</returns>
        public static AssemblyIdentity FromNativeAssembly(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            if (PathUtil.IsPEFile(path))
            {
                Stream m = EmbeddedManifestReader.Read(path);
                if (m == null)
                {
                    return null;
                }
                return FromManifest(m);
            }
            return FromManifest(path);
        }

        /// <summary>
        /// Obtains identity of the specified assembly.
        /// File can be a PE with an embedded xml manifest, a stand-alone xml manifest file, or a .NET assembly.
        /// Returns null if identity could not be obtained.
        /// </summary>
        /// <param name="path">The name of the file from which the identity is to be obtained.</param>
        /// <returns>The assembly identity of the specified file.</returns>
        public static AssemblyIdentity FromFile(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            return FromNativeAssembly(path) ?? FromManagedAssembly(path);
        }

        internal static bool IsEqual(AssemblyIdentity a1, AssemblyIdentity a2)
        {
            return IsEqual(a1, a2, true);
        }

        internal static bool IsEqual(AssemblyIdentity a1, AssemblyIdentity a2, bool specificVersion)
        {
            if (a1 == null || a2 == null)
            {
                return false;
            }
            if (specificVersion)
            {
                return String.Equals(a1._name, a2._name, StringComparison.OrdinalIgnoreCase)
                    && String.Equals(a1._publicKeyToken, a2._publicKeyToken, StringComparison.OrdinalIgnoreCase)
                    && String.Equals(a1._version, a2._version, StringComparison.OrdinalIgnoreCase)
                    && String.Equals(a1._culture, a2._culture, StringComparison.OrdinalIgnoreCase)
                    && String.Equals(a1._processorArchitecture, a2._processorArchitecture, StringComparison.OrdinalIgnoreCase);
            }
            return String.Equals(a1._name, a2._name, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns true if this assembly is part of the .NET Framework.
        /// </summary>
        [XmlIgnore]
        public bool IsFrameworkAssembly => IsInFramework(null, null);

        /// <summary>
        /// Returns true if this assembly is part of the given framework.
        /// identifier is “.NETFramework” or “Silverlight”, etc. and the version string looks like this: “4.5” or “v4.5”, or “v4.0.30319"
        /// If frameworkVersion is null or empty, return true if this assembly is present in any of the given framework versions
        /// If both arguments are null or empty strings, return true if this assembly is present in any of the frameworks
        /// </summary>
        [SuppressMessage("Microsoft.Globalization", "CA1307: Specify StringComparison.")]
        public bool IsInFramework(string frameworkIdentifier, string frameworkVersion)
        {
            Version version = null;
            if (!string.IsNullOrEmpty(frameworkVersion))
            {
                // CA1307:Specify StringComparison.  Suppressed since a valid string representation of a version would be parsed correctly even if the the first character is not "v".
                if (frameworkVersion.StartsWith("v"))
                {
                    System.Version.TryParse(frameworkVersion.Substring(1), out version);
                }
                else
                {
                    System.Version.TryParse(frameworkVersion, out version);
                }
            }

            if (string.IsNullOrEmpty(frameworkIdentifier) && version != null)
            {
                throw new ArgumentNullException(nameof(frameworkIdentifier));
            }

            var redistDictionary = new Dictionary<string, RedistList>();

            foreach (string moniker in ToolLocationHelper.GetSupportedTargetFrameworks())
            {
                FrameworkNameVersioning frameworkName = new FrameworkNameVersioning(moniker);
                if ((string.IsNullOrEmpty(frameworkIdentifier) || frameworkName.Identifier.Equals(frameworkIdentifier, StringComparison.OrdinalIgnoreCase)) &&
                    (version == null || frameworkName.Version == version))
                {
                    IList<string> paths = ToolLocationHelper.GetPathToReferenceAssemblies(frameworkName);

                    foreach (string path in paths)
                    {
                        if (!redistDictionary.ContainsKey(path))
                        {
                            redistDictionary.Add(path, RedistList.GetRedistListFromPath(path));
                        }
                    }
                }
            }

            string fullName = GetFullName(FullNameFlags.Default);
            foreach (RedistList list in redistDictionary.Values)
            {
                if (list != null && list.IsFrameworkAssembly(fullName))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Specifies the culture of the assembly. A blank string indicates the invariant culture.
        /// </summary>
        [XmlIgnore]
        public string Culture
        {
            get => _culture;
            set => _culture = value;
        }

        /// <summary>
        /// Returns the full name of the assembly.
        /// </summary>
        /// <param name="flags">Specifies which attributes to be included in the full name.</param>
        /// <returns>A string representation of the full name.</returns>
        public string GetFullName(FullNameFlags flags)
        {
            var sb = new StringBuilder(_name);
            if (!String.IsNullOrEmpty(_version))
            {
                sb.Append(String.Format(CultureInfo.InvariantCulture, ", Version={0}", _version));
            }
            if (!String.IsNullOrEmpty(_culture))
            {
                sb.Append(String.Format(CultureInfo.InvariantCulture, ", Culture={0}", _culture));
            }
            if (!String.IsNullOrEmpty(_publicKeyToken))
            {
                sb.Append(String.Format(CultureInfo.InvariantCulture, ", PublicKeyToken={0}", _publicKeyToken));
            }
            if (!String.IsNullOrEmpty(_processorArchitecture) && (flags & FullNameFlags.ProcessorArchitecture) != 0)
            {
                sb.Append(String.Format(CultureInfo.InvariantCulture, ", ProcessorArchitecture={0}", _processorArchitecture));
            }
            if (!String.IsNullOrEmpty(_type) && (flags & FullNameFlags.Type) != 0)
            {
                sb.Append(String.Format(CultureInfo.InvariantCulture, ", Type={0}", _type));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Specifies whether the assembly identity represents a neutral platform assembly.
        /// </summary>
        [XmlIgnore]
        public bool IsNeutralPlatform => String.IsNullOrEmpty(_processorArchitecture) || String.Equals(_processorArchitecture, "msil", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Specifies whether the assembly identity is a strong name.
        /// </summary>
        [XmlIgnore]
        public bool IsStrongName => !String.IsNullOrEmpty(_name)
                                    && !String.IsNullOrEmpty(_version)
                                    && !String.IsNullOrEmpty(_publicKeyToken);

        /// <summary>
        /// Specifies the simple name of the assembly.
        /// </summary>
        [XmlIgnore]
        public string Name
        {
            get => _name;
            set => _name = value;
        }

        /// <summary>
        /// Specifies the processor architecture of the assembly. Valid values are "msil", "x86", "ia64", "amd64".
        /// </summary>
        [XmlIgnore]
        public string ProcessorArchitecture
        {
            get => _processorArchitecture;
            set => _processorArchitecture = value;
        }

        /// <summary>
        /// Specifies the public key token of the assembly, which is the last 8 bytes of the SHA-1 hash of the public key under which the assembly is signed.
        /// </summary>
        [XmlIgnore]
        public string PublicKeyToken
        {
            get => _publicKeyToken;
            set => _publicKeyToken = value;
        }

        internal string Resolve(string[] searchPaths)
        {
            return Resolve(searchPaths, IsStrongName);
        }

        internal string Resolve(string[] searchPaths, bool specificVersion)
        {
            if (searchPaths == null)
            {
                searchPaths = new[] { ".\\" };
            }

            foreach (string searchPath in searchPaths)
            {
                string file = String.Format(CultureInfo.InvariantCulture, "{0}.dll", _name);
                string path = Path.Combine(searchPath, file);
                if (File.Exists(path) && IsEqual(this, FromFile(path), specificVersion))
                {
                    return path;
                }

                file = String.Format(CultureInfo.InvariantCulture, "{0}.manifest", _name);
                path = Path.Combine(searchPath, file);
                if (File.Exists(path) && IsEqual(this, FromManifest(path), specificVersion))
                {
                    return path;
                }
            }

            return null;
        }

        public override string ToString()
        {
            return GetFullName(FullNameFlags.All);
        }

        /// <summary>
        /// Specifies the type attribute of the assembly. Valid values are "win32" or a blank string.
        /// </summary>
        [XmlIgnore]
        public string Type
        {
            get => _type;
            set => _type = value;
        }

        /// <summary>
        /// Specifies the version of the assembly.
        /// </summary>
        [XmlIgnore]
        public string Version
        {
            get => _version;
            set => _version = value;
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
        [XmlAttribute("Version")]
        public string XmlVersion
        {
            get => _version;
            set => _version = value;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("PublicKeyToken")]
        public string XmlPublicKeyToken
        {
            get => _publicKeyToken;
            set => _publicKeyToken = value;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("Culture")]
        public string XmlCulture
        {
            get => _culture;
            set => _culture = value;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("ProcessorArchitecture")]
        public string XmlProcessorArchitecture
        {
            get => _processorArchitecture;
            set => _processorArchitecture = value;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("Type")]
        public string XmlType
        {
            get => _type;
            set => _type = value;
        }

        #endregion
    }
}
