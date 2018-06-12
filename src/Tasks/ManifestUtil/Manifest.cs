// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks.Deployment.ManifestUtilities
{
    /// <summary>
    /// Describes base functionality common to all supported manifest types.
    /// </summary>
    [ComVisible(false)]
    public abstract class Manifest
    {
        private AssemblyIdentity _assemblyIdentity;
        private AssemblyReference[] _assemblyReferences;
        private string _description;
        private FileReference[] _fileReferences;
        private string _sourcePath;
        private Stream _inputStream;
        private FileReferenceCollection _fileReferenceList;
        private AssemblyReferenceCollection _assemblyReferenceList;
        private readonly OutputMessageCollection _outputMessages = new OutputMessageCollection();
        private bool _treatUnfoundNativeAssembliesAsPrerequisites;
        private bool _readOnly;

        protected internal Manifest() // only internal classes can extend this class
        {
        }

        /// <summary>
        /// Specifies the identity of the manifest.
        /// </summary>
        [XmlIgnore]
        public AssemblyIdentity AssemblyIdentity
        {
            get => _assemblyIdentity ?? (_assemblyIdentity = new AssemblyIdentity());
            set => _assemblyIdentity = value;
        }

        /// <summary>
        /// Specifies the set of assemblies referenced by the manifest.
        /// </summary>
        [XmlIgnore]
        public AssemblyReferenceCollection AssemblyReferences => _assemblyReferenceList ??
                                                                 (_assemblyReferenceList = new AssemblyReferenceCollection(_assemblyReferences));

        private void CollectionToArray()
        {
            if (_assemblyReferenceList != null)
            {
                _assemblyReferences = _assemblyReferenceList.ToArray();
                _assemblyReferenceList = null;
            }
            if (_fileReferenceList != null)
            {
                _fileReferences = _fileReferenceList.ToArray();
                _fileReferenceList = null;
            }
        }

        /// <summary>
        /// Specifies a textual description for the manifest.
        /// </summary>
        [XmlIgnore]
        public string Description
        {
            get => _description;
            set => _description = value;
        }

        /// <summary>
        /// Identifies an assembly reference which is the entry point of the application.
        /// </summary>
        [XmlIgnore]
        public virtual AssemblyReference EntryPoint
        {
            get { return null; }
            set { }
        }

        /// <summary>
        /// Specifies the set of files referenced by the manifest.
        /// </summary>
        [XmlIgnore]
        public FileReferenceCollection FileReferences => _fileReferenceList ?? (_fileReferenceList = new FileReferenceCollection(_fileReferences));

        /// <summary>
        /// The input stream from which the manifest was read.
        /// Used by ManifestWriter to reconstitute input which is not represented in the object representation.
        /// </summary>
        [XmlIgnore]
        public Stream InputStream
        {
            get => _inputStream;
            set => _inputStream = value;
        }

        internal virtual void OnAfterLoad()
        {
        }

        internal virtual void OnBeforeSave()
        {
            CollectionToArray();
            SortFiles();
        }

        /// <summary>
        /// Contains a collection of current error and warning messages.
        /// </summary>
        [XmlIgnore]
        public OutputMessageCollection OutputMessages => _outputMessages;

        /// <summary>
        /// Specifies whether the manifest is operating in read-only or read-write mode.
        /// If only using to read a manifest then set this flag to true.
        /// If using to write a new manifest then set this flag to false.
        /// The default is false.
        /// This flag provides additional context for the manifest generator, and affects how some error messages are reported.
        /// </summary>
        [XmlIgnore]
        public bool ReadOnly
        {
            get => _readOnly;
            set => _readOnly = value;
        }

        private static bool ResolveAssembly(AssemblyReference a, string[] searchPaths)
        {
            if (a == null)
            {
                return false;
            }

            a.ResolvedPath = ResolvePath(a.SourcePath, searchPaths);
            if (!String.IsNullOrEmpty(a.ResolvedPath))
            {
                return true;
            }

            if (a.AssemblyIdentity != null)
            {
                a.ResolvedPath = a.AssemblyIdentity.Resolve(searchPaths);
                if (!String.IsNullOrEmpty(a.ResolvedPath))
                {
                    return true;
                }
            }

            a.ResolvedPath = ResolvePath(a.TargetPath, searchPaths);
            if (!String.IsNullOrEmpty(a.ResolvedPath))
            {
                return true;
            }

            return false;
        }

        private static bool ResolveFile(BaseReference f, string[] searchPaths)
        {
            if (f == null)
            {
                return false;
            }

            f.ResolvedPath = ResolvePath(f.SourcePath, searchPaths);
            if (!String.IsNullOrEmpty(f.ResolvedPath))
            {
                return true;
            }

            f.ResolvedPath = ResolvePath(f.TargetPath, searchPaths);
            if (!String.IsNullOrEmpty(f.ResolvedPath))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Locates all specified assembly and file references by searching in the same directory as the loaded manifest, or in the current directory.
        /// The location of each referenced assembly and file is required for hash computation and assembly identity resolution.
        /// Any resulting errors or warnings are reported in the OutputMessages collection.
        /// </summary>
        public void ResolveFiles()
        {
            string defaultDir = String.Empty;
            if (!String.IsNullOrEmpty(_sourcePath))
            {
                defaultDir = Path.GetDirectoryName(_sourcePath);
            }

            if (!Path.IsPathRooted(defaultDir))
            {
                defaultDir = Path.Combine(Directory.GetCurrentDirectory(), defaultDir);
            }
            string[] searchPaths = { defaultDir };
            ResolveFiles(searchPaths);
        }

        /// <summary>
        /// Locates all specified assembly and file references by searching in the specified directories.
        /// The location of each referenced assembly and file is required for hash computation and assembly identity resolution.
        /// Any resulting errors or warnings are reported in the OutputMessages collection.
        /// </summary>
        /// <param name="searchPaths">An array of strings specify directories to search.</param>
        public void ResolveFiles(string[] searchPaths)
        {
            if (searchPaths == null)
                throw new ArgumentNullException(nameof(searchPaths));
            CollectionToArray();
            ResolveFiles_1(searchPaths);
            ResolveFiles_2(searchPaths);
        }

        private void ResolveFiles_1(string[] searchPaths)
        {
            if (_assemblyReferences != null)
            {
                foreach (AssemblyReference a in _assemblyReferences)
                {
                    if (!a.IsPrerequisite || a.AssemblyIdentity == null)
                    {
                        if (!ResolveAssembly(a, searchPaths))
                        {
                            if (_treatUnfoundNativeAssembliesAsPrerequisites && a.ReferenceType == AssemblyReferenceType.NativeAssembly)
                            {
                                a.IsPrerequisite = true;
                            }
                            else
                            {
                                // When we're only reading a manifest (i.e. from ResolveNativeReference task), it's
                                // very useful to report what manifest has the unresolvable reference. However, when
                                // we're generating a new manifest (i.e. from GenerateApplicationManifest task)
                                // reporting the manifest is awkward and sometimes looks like a bug.
                                // So we use the ReadOnly flag to tell the difference between the two cases...
                                if (_readOnly)
                                {
                                    OutputMessages.AddErrorMessage("GenerateManifest.ResolveFailedInReadOnlyMode", a.ToString(), ToString());
                                }
                                else
                                {
                                    OutputMessages.AddErrorMessage("GenerateManifest.ResolveFailedInReadWriteMode", a.ToString());
                                }
                            }
                        }
                    }
                }
            }
        }

        private void ResolveFiles_2(string[] searchPaths)
        {
            if (_fileReferences != null)
            {
                foreach (FileReference f in _fileReferences)
                {
                    if (!ResolveFile(f, searchPaths))
                    {
                        // When we're only reading a manifest (i.e. from ResolveNativeReference task), it's
                        // very useful to report what manifest has the unresolvable reference. However, when
                        // we're generating a new manifest (i.e. from GenerateApplicationManifest task)
                        // reporting the manifest is awkward and sometimes looks like a bug.
                        // So we use the ReadOnly flag to tell the difference between the two cases...
                        if (_readOnly)
                        {
                            OutputMessages.AddErrorMessage("GenerateManifest.ResolveFailedInReadOnlyMode", f.ToString(), this.ToString());
                        }
                        else
                        {
                            OutputMessages.AddErrorMessage("GenerateManifest.ResolveFailedInReadWriteMode", f.ToString());
                        }
                    }
                }
            }
        }

        private static string ResolvePath(string path, string[] searchPaths)
        {
            if (String.IsNullOrEmpty(path))
            {
                return null;
            }
            if (Path.IsPathRooted(path))
            {
                if (File.Exists(path))
                {
                    return path;
                }
                return null;
            }

            if (searchPaths == null)
            {
                return null;
            }
            foreach (string searchPath in searchPaths)
            {
                if (!String.IsNullOrEmpty(searchPath))
                {
                    string resolvedPath = Path.Combine(searchPath, path);
                    resolvedPath = Path.GetFullPath(resolvedPath);
                    if (File.Exists(resolvedPath))
                    {
                        return resolvedPath;
                    }
                }
            }
            return null;
        }

        private void SortFiles()
        {
            CollectionToArray();
            var comparer = new ReferenceComparer();
            if (_assemblyReferences != null)
            {
                Array.Sort(_assemblyReferences, comparer);
            }
            if (_fileReferences != null)
            {
                Array.Sort(_fileReferences, comparer);
            }
        }

        /// <summary>
        /// Specifies the location where the manifest was loaded or saved.
        /// </summary>
        [XmlIgnore]
        public string SourcePath
        {
            get => _sourcePath;
            set => _sourcePath = value;
        }

        public override string ToString()
        {
            return !String.IsNullOrEmpty(_sourcePath) ? _sourcePath : AssemblyIdentity.ToString();
        }

        internal bool TreatUnfoundNativeAssembliesAsPrerequisites
        {
            get => _treatUnfoundNativeAssembliesAsPrerequisites;
            set => _treatUnfoundNativeAssembliesAsPrerequisites = value;
        }

        internal static void UpdateEntryPoint(string inputPath, string outputPath, string updatedApplicationPath, string applicationManifestPath, string targetFrameworkVersion)
        {
            var document = new XmlDocument();
            var xrSettings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore };
            using (XmlReader xr = XmlReader.Create(inputPath, xrSettings))
            {
                document.Load(xr);
            }
            XmlNamespaceManager nsmgr = XmlNamespaces.GetNamespaceManager(document.NameTable);
            AssemblyIdentity appManifest = AssemblyIdentity.FromManifest(applicationManifestPath);

            // update path to application manifest            
            XmlNode codeBaseNode = null;
            foreach (string xpath in XPaths.codebasePaths)
            {
                codeBaseNode = document.SelectSingleNode(xpath, nsmgr);
                if (codeBaseNode != null)
                {
                    break;
                }
            }

            if (codeBaseNode == null)
            {
                throw new InvalidOperationException(String.Format(System.Globalization.CultureInfo.InvariantCulture, "XPath not found: {0}", XPaths.codebasePaths[0]));
            }

            codeBaseNode.Value = updatedApplicationPath;

            // update Public key token of application manifest
            XmlNode publicKeyTokenNode = ((XmlAttribute)codeBaseNode).OwnerElement.SelectSingleNode(XPaths.dependencyPublicKeyTokenAttribute, nsmgr);
            if (publicKeyTokenNode == null)
            {
                throw new InvalidOperationException(String.Format(System.Globalization.CultureInfo.InvariantCulture, "XPath not found: {0}", XPaths.dependencyPublicKeyTokenAttribute));
            }

            publicKeyTokenNode.Value = appManifest.PublicKeyToken;

            // update hash of application manifest
            Util.GetFileInfo(applicationManifestPath, targetFrameworkVersion, out string hash, out long size);

            // Hash node may not be present with optional signing
            XmlNode hashNode = ((XmlAttribute)codeBaseNode).OwnerElement.SelectSingleNode(XPaths.hashElement, nsmgr);
            if (hashNode != null)
            {
                ((XmlElement)hashNode).InnerText = hash;
            }

            // update file size of application manifest
            XmlAttribute sizeAttribute = ((XmlAttribute)codeBaseNode).OwnerElement.Attributes[XmlUtil.TrimPrefix(XPaths.fileSizeAttribute)];
            if (sizeAttribute == null)
            {
                throw new InvalidOperationException(String.Format(System.Globalization.CultureInfo.InvariantCulture, "XPath not found: {0}", XPaths.fileSizeAttribute));
            }

            sizeAttribute.Value = size.ToString(System.Globalization.CultureInfo.InvariantCulture);

            document.Save(outputPath);
        }

        private void UpdateAssemblyReference(AssemblyReference a, string targetFrameworkVersion)
        {
            if (a.IsVirtual)
            {
                return;
            }

            if (a.AssemblyIdentity == null)
            {
                switch (a.ReferenceType)
                {
                    case AssemblyReferenceType.ClickOnceManifest:
                        a.AssemblyIdentity = AssemblyIdentity.FromManifest(a.ResolvedPath);
                        break;
                    case AssemblyReferenceType.ManagedAssembly:
                        a.AssemblyIdentity = AssemblyIdentity.FromManagedAssembly(a.ResolvedPath);
                        break;
                    case AssemblyReferenceType.NativeAssembly:
                        a.AssemblyIdentity = AssemblyIdentity.FromNativeAssembly(a.ResolvedPath);
                        break;
                    default:
                        a.AssemblyIdentity = AssemblyIdentity.FromFile(a.ResolvedPath);
                        break;
                }
            }

            if (!a.IsPrerequisite)
            {
                UpdateFileReference(a, targetFrameworkVersion);
            }

            // If unspecified assembly type then let's figure out what it actually is...
            if (a.ReferenceType == AssemblyReferenceType.Unspecified)
            {
                // a ClickOnce deployment manifest can only refer to a ClickOnce application manifest...
                if (this is DeployManifest)
                {
                    a.ReferenceType = AssemblyReferenceType.ClickOnceManifest;
                }
                // otherwise it can only be either a managed or a native assembly, but we can only tell if we have the path...
                else if (!String.IsNullOrEmpty(a.ResolvedPath))
                {
                    if (PathUtil.IsNativeAssembly(a.ResolvedPath))
                    {
                        a.ReferenceType = AssemblyReferenceType.NativeAssembly;
                    }
                    else
                    {
                        a.ReferenceType = AssemblyReferenceType.ManagedAssembly;
                    }
                }
                // there's one other way we can tell, Type="win32" references are always native...
                else if (a.AssemblyIdentity != null && String.Equals(a.AssemblyIdentity.Type, "win32", StringComparison.OrdinalIgnoreCase))
                {
                    a.ReferenceType = AssemblyReferenceType.NativeAssembly;
                }
            }
        }

        private static void UpdateFileReference(BaseReference f, string targetFrameworkVersion)
        {
            if (String.IsNullOrEmpty(f.ResolvedPath))
            {
                throw new FileNotFoundException(null, f.SourcePath);
            }

            string hash;
            long size;
            if (string.IsNullOrEmpty(targetFrameworkVersion))
            {
                Util.GetFileInfo(f.ResolvedPath, out hash, out size);
            }
            else
            {
                Util.GetFileInfo(f.ResolvedPath, targetFrameworkVersion, out hash, out size);
            }
            f.Hash = hash;
            f.Size = size;
            if (String.IsNullOrEmpty(f.TargetPath))
            {
                if (!String.IsNullOrEmpty(f.SourcePath))
                {
                    f.TargetPath = BaseReference.GetDefaultTargetPath(f.SourcePath);
                }
                else
                {
                    f.TargetPath = BaseReference.GetDefaultTargetPath(Path.GetFileName(f.ResolvedPath));
                }
            }
        }

        /// <summary>
        /// Updates file information for each referenced assembly and file.
        /// The file information includes a hash computation and a file size for each referenced file and assembly.
        /// Also, the assembly identity is obtained for any referenced assemblies with an unspecified assembly identity.
        /// Any resulting errors or warnings are reported in the OutputMessages collection.
        /// </summary>
        public void UpdateFileInfo()
        {
            UpdateFileInfoImpl(null);
        }

        public void UpdateFileInfo(string targetFrameworkVersion)
        {
            UpdateFileInfoImpl(targetFrameworkVersion);
        }

        /// <summary>
        /// Implementation of UpdateFileInfo
        /// </summary>
        /// <param name="targetFrameworkVersion">null, if not TFV.  If no TFV, it will use sha256 signature algorithm.</param>
        private void UpdateFileInfoImpl(string targetFrameworkVersion)
        {
            if (_assemblyReferences != null)
            {
                foreach (AssemblyReference a in _assemblyReferences)
                {
                    if (!String.IsNullOrEmpty(a.ResolvedPath)) // only check resolved items...
                    {
                        try
                        {
                            UpdateAssemblyReference(a, targetFrameworkVersion);
                            if (a.AssemblyIdentity == null)
                            {
                                BadImageFormatException exception = new BadImageFormatException(null, a.ResolvedPath);
                                OutputMessages.AddErrorMessage("GenerateManifest.General", exception.Message);
                            }
                        }
                        catch (Exception e)
                        {
                            OutputMessages.AddErrorMessage("GenerateManifest.General", e.Message);
                        }
                    }
                }
            }
            if (_fileReferences != null)
            {
                foreach (FileReference f in _fileReferences)
                {
                    if (!String.IsNullOrEmpty(f.ResolvedPath)) // only check resolved items...
                    {
                        try
                        {
                            UpdateFileReference(f, targetFrameworkVersion);
                        }
                        catch (Exception e)
                        {
                            OutputMessages.AddErrorMessage("GenerateManifest.General", e.Message);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Performs various checks to verify the validity of the manifest.
        /// Any resulting errors or warnings are reported in the OutputMessages collection.
        /// </summary>
        public virtual void Validate()
        {
            ValidateReferences();
        }

        private void ValidateReferences()
        {
            if (AssemblyReferences.Count <= 1)
            {
                return;
            }

            var identityList = new Dictionary<string, NGen<bool>>();
            foreach (AssemblyReference assembly in AssemblyReferences)
            {
                if (assembly.AssemblyIdentity != null)
                {
                    // Check for two or more assemblies with the same identity...
                    string identity = assembly.AssemblyIdentity.GetFullName(AssemblyIdentity.FullNameFlags.All);
                    string key = identity.ToLowerInvariant();
                    if (!identityList.ContainsKey(key))
                    {
                        identityList.Add(key, false);
                    }
                    else if (identityList[key] == false)
                    {
                        OutputMessages.AddWarningMessage("GenerateManifest.DuplicateAssemblyIdentity", identity);
                        identityList[key] = true; // only warn once per identity
                    }
                }

                // Check that resolved assembly identity matches filename...
                if (!assembly.IsPrerequisite)
                {
                    if (assembly.AssemblyIdentity != null)
                    {
                        if (!String.Equals(
                            assembly.AssemblyIdentity.Name,
                            Path.GetFileNameWithoutExtension(assembly.TargetPath),
                            StringComparison.OrdinalIgnoreCase))
                        {
                            OutputMessages.AddWarningMessage("GenerateManifest.IdentityFileNameMismatch", assembly.ToString(), assembly.AssemblyIdentity.Name, assembly.AssemblyIdentity.Name + Path.GetExtension(assembly.TargetPath));
                        }
                    }
                }
            }
        }

        protected void ValidatePlatform()
        {
            foreach (AssemblyReference assembly in AssemblyReferences)
            {
                if (IsMismatchedPlatform(assembly))
                {
                    OutputMessages.AddWarningMessage("GenerateManifest.PlatformMismatch", assembly.ToString());
                }
            }
        }

        // Determines whether the platform of the specified assembly reference is mismatched with the applicaion's platform.
        private bool IsMismatchedPlatform(AssemblyReference assembly)
        {
            // Never flag the "Microsoft.CommonLanguageRuntime" dependency as a mismatch...
            if (assembly.IsVirtual)
            {
                return false;
            }
            // Can't tell anything if either of these are not resolved...
            if (AssemblyIdentity == null || assembly.AssemblyIdentity == null)
            {
                return false;
            }

            if (AssemblyIdentity.IsNeutralPlatform)
            {
                // If component is a native assembly then it is non-platform neutral by definition, so always flag as a mismatch...
                if (assembly.ReferenceType == AssemblyReferenceType.NativeAssembly)
                {
                    return true;
                }
                // Otherwise flag component as a mismatch only if it's not also platform neutral...
                return !assembly.AssemblyIdentity.IsNeutralPlatform;
            }
            else
            {
                // We want the application platform for the entry point to always match the setting for the whole application,
                // but the dependencies do not necessarily have to match...
                if (assembly != EntryPoint)
                {
                    // If application IS NOT platform neutral but the component is, then component shouldn't be flagged as a mismatch...
                    if (assembly.AssemblyIdentity.IsNeutralPlatform)
                    {
                        return false;
                    }
                }

                // Either we are looking at the entry point assembly or the assembly is not platform neutral. 
                // We need to compare the application's platform to the component's platform,
                // if they don't match then flag component as a mismatch...
                return !String.Equals(AssemblyIdentity.ProcessorArchitecture, assembly.AssemblyIdentity.ProcessorArchitecture, StringComparison.OrdinalIgnoreCase);
            }
        }

        #region " XmlSerializer "

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlElement("AssemblyIdentity")]
        public AssemblyIdentity XmlAssemblyIdentity
        {
            get => _assemblyIdentity;
            set => _assemblyIdentity = value;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlArray("AssemblyReferences")]
        public AssemblyReference[] XmlAssemblyReferences
        {
            get => _assemblyReferences;
            set => _assemblyReferences = value;
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
        [XmlArray("FileReferences")]
        public FileReference[] XmlFileReferences
        {
            get => _fileReferences;
            set => _fileReferences = value;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlAttribute("Schema")]
        public string XmlSchema
        {
            get { return Util.Schema; }
            set { }
        }

        #endregion


        #region " ReferenceComparer "

        private class ReferenceComparer : IComparer
        {
            public int Compare(object x, object y)
            {
                if (x == null || y == null)
                {
                    Debug.Fail("Comparing null objects");
                    return 0;
                }
                if (!(x is BaseReference) || !(y is BaseReference))
                {
                    Debug.Fail("Comparing objects that are not BaseReferences");
                    return 0;
                }

                BaseReference xRef = x as BaseReference;
                BaseReference yRef = y as BaseReference;

                if (xRef.SortName == null || yRef.SortName == null)
                {
                    Debug.Fail("Objects do not have a SortName");
                    return 0;
                }

                return xRef.SortName.CompareTo(yRef.SortName);
            }
        }

        #endregion
    }
}
