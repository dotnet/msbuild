// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Collections;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Build.Shared;
using System.Text;
using System.Runtime.Versioning;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Collection of methods used to discover assembly metadata.
    /// Primarily stolen from manifestutility.cs AssemblyMetaDataImport class.
    /// </summary>
    internal class AssemblyInformation : DisposableBase
    {
        private AssemblyNameExtension[] _assemblyDependencies = null;
        private string[] _assemblyFiles = null;
        private IMetaDataDispenser _metadataDispenser = null;
        private IMetaDataAssemblyImport _assemblyImport = null;
        private static Guid s_importerGuid = new Guid(((GuidAttribute)Attribute.GetCustomAttribute(typeof(IMetaDataImport), typeof(GuidAttribute), false)).Value);
        private string _sourceFile;
        private FrameworkName _frameworkName;
        private static string s_targetFrameworkAttribute = "System.Runtime.Versioning.TargetFrameworkAttribute";

        // Borrowed from genman.
        private const int GENMAN_STRING_BUF_SIZE = 1024;
        private const int GENMAN_LOCALE_BUF_SIZE = 64;
        private const int GENMAN_ENUM_TOKEN_BUF_SIZE = 16; // 128 from genman seems too big.

        /// <summary>
        /// Construct an instance for a source file.
        /// </summary>
        /// <param name="sourceFile">The assembly.</param>
        internal AssemblyInformation(string sourceFile)
        {
            // Extra checks for PInvoke-destined data.
            ErrorUtilities.VerifyThrowArgumentNull(sourceFile, "sourceFile");
            _sourceFile = sourceFile;

            // Create the metadata dispenser and open scope on the source file.
            _metadataDispenser = (IMetaDataDispenser)new CorMetaDataDispenser();
            _assemblyImport = (IMetaDataAssemblyImport)_metadataDispenser.OpenScope(sourceFile, 0, ref s_importerGuid);
        }

        /// <summary>
        /// Get the dependencies.
        /// </summary>
        /// <value></value>
        public AssemblyNameExtension[] Dependencies
        {
            get
            {
                if (_assemblyDependencies == null)
                {
                    lock (this)
                    {
                        if (_assemblyDependencies == null)
                        {
                            _assemblyDependencies = ImportAssemblyDependencies();
                        }
                    }
                }

                return _assemblyDependencies;
            }
        }

        /// <summary>
        /// Get the scatter files from the assembly metadata. 
        /// </summary>
        /// <value></value>
        public string[] Files
        {
            get
            {
                if (_assemblyFiles == null)
                {
                    lock (this)
                    {
                        if (_assemblyFiles == null)
                        {
                            _assemblyFiles = ImportFiles();
                        }
                    }
                }

                return _assemblyFiles;
            }
        }

        /// <summary>
        /// What was the framework name that the assembly was built against.
        /// </summary>
        public FrameworkName FrameworkNameAttribute
        {
            get
            {
                if (_frameworkName == null)
                {
                    lock (this)
                    {
                        if (_frameworkName == null)
                        {
                            _frameworkName = GetFrameworkName();
                        }
                    }
                }

                return _frameworkName;
            }
        }

        /// <summary>
        /// Given an assembly name, crack it open and retrieve the list of dependent 
        /// assemblies and  the list of scatter files.
        /// </summary>
        /// <param name="path">Path to the assembly.</param>
        /// <param name="dependencies">Receives the list of dependencies.</param>
        /// <param name="scatterFiles">Receives the list of associated scatter files.</param>
        internal static void GetAssemblyMetadata
        (
            string path,
            out AssemblyNameExtension[] dependencies,
            out string[] scatterFiles,
            out FrameworkName frameworkName
        )
        {
            AssemblyInformation import = null;
            using (import = new AssemblyInformation(path))
            {
                dependencies = import.Dependencies;
                scatterFiles = import.Files;
                frameworkName = import.FrameworkNameAttribute;
            }
        }

        /// <summary>
        /// Given an assembly name, crack it open and retrieve the TargetFrameworkAttribute
        /// assemblies and  the list of scatter files.
        /// </summary>
        internal static FrameworkName GetTargetFrameworkAttribute(string path)
        {
            using (AssemblyInformation import = new AssemblyInformation(path))
            {
                return import.FrameworkNameAttribute;
            }
        }

        /// <summary>
        /// Determine if an file is a winmd file or not.
        /// </summary>
        internal static bool IsWinMDFile(string fullPath, GetAssemblyRuntimeVersion getAssemblyRuntimeVersion, FileExists fileExists, out string imageRuntimeVersion, out bool isManagedWinmd)
        {
            imageRuntimeVersion = String.Empty;
            isManagedWinmd = false;

            // May be null or empty is the file was never resolved to a path on disk.
            if (!String.IsNullOrEmpty(fullPath) && fileExists(fullPath))
            {
                imageRuntimeVersion = getAssemblyRuntimeVersion(fullPath);
                if (!String.IsNullOrEmpty(imageRuntimeVersion))
                {
                    bool containsWindowsRuntime = imageRuntimeVersion.IndexOf("WindowsRuntime", StringComparison.OrdinalIgnoreCase) >= 0;

                    if (containsWindowsRuntime)
                    {
                        isManagedWinmd = imageRuntimeVersion.IndexOf("CLR", StringComparison.OrdinalIgnoreCase) >= 0;
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Get the framework name from the assembly.
        /// </summary>
        private FrameworkName GetFrameworkName()
        {
            FrameworkName frameworkAttribute = null;
            try
            {
                IMetaDataImport2 import2 = (IMetaDataImport2)_assemblyImport;
                IntPtr data = IntPtr.Zero;
                UInt32 valueLen = 0;
                string frameworkNameAttribute = null;
                UInt32 assemblyScope;

                _assemblyImport.GetAssemblyFromScope(out assemblyScope);
                int hr = import2.GetCustomAttributeByName(assemblyScope, s_targetFrameworkAttribute, out data, out valueLen);

                // get the AssemblyTitle
                if (hr == NativeMethodsShared.S_OK)
                {
                    // if an AssemblyTitle exists, parse the contents of the blob
                    if (NativeMethods.TryReadMetadataString(_sourceFile, data, valueLen, out frameworkNameAttribute))
                    {
                        if (!String.IsNullOrEmpty(frameworkNameAttribute))
                        {
                            frameworkAttribute = new FrameworkName(frameworkNameAttribute);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                if (ExceptionHandling.IsCriticalException(e))
                {
                    throw;
                }
            }

            return frameworkAttribute;
        }

        /// <summary>
        /// Release interface pointers on Dispose(). 
        /// </summary>
        protected override void DisposeUnmanagedResources()
        {
            if (_assemblyImport != null)
                Marshal.ReleaseComObject(_assemblyImport);
            if (_metadataDispenser != null)
                Marshal.ReleaseComObject(_metadataDispenser);
        }

        /// <summary>
        /// Given a path get the CLR runtime version of the file
        /// </summary>
        /// <param name="path">path to the file</param>
        /// <returns>The CLR runtime version or empty if the path does not exist.</returns>
        internal static string GetRuntimeVersion(string path)
        {
            StringBuilder runtimeVersion = null;
            uint hresult = 0;
            uint actualBufferSize = 0;
#if _DEBUG
            // Just to make sure and exercise the code that doubles the size
            // every time GetRequestedRuntimeInfo fails due to insufficient buffer size.
            int bufferLength = 1;
#else
            int bufferLength = 11; // 11 is the length of a runtime version and null terminator v2.0.50727/0
#endif
            do
            {
                runtimeVersion = new StringBuilder(bufferLength);
                hresult = NativeMethods.GetFileVersion(path, runtimeVersion, bufferLength, out actualBufferSize);
                bufferLength = bufferLength * 2;
            } while (hresult == NativeMethodsShared.ERROR_INSUFFICIENT_BUFFER);

            if (hresult == NativeMethodsShared.S_OK && runtimeVersion != null)
            {
                return runtimeVersion.ToString();
            }
            else
            {
                return String.Empty;
            }
        }


        /// <summary>
        /// Import assembly dependencies.
        /// </summary>
        /// <returns>The array of assembly dependencies.</returns>
        private AssemblyNameExtension[] ImportAssemblyDependencies()
        {
            ArrayList asmRefs = new ArrayList();
            IntPtr asmRefEnum = IntPtr.Zero;
            UInt32[] asmRefTokens = new UInt32[GENMAN_ENUM_TOKEN_BUF_SIZE];
            UInt32 fetched;
            // Ensure the enum handle is closed.
            try
            {
                // Enum chunks of refs in 16-ref blocks until we run out.
                do
                {
                    _assemblyImport.EnumAssemblyRefs(ref asmRefEnum, asmRefTokens, (uint)asmRefTokens.Length, out fetched);

                    for (uint i = 0; i < fetched; i++)
                    {
                        // Determine the length of the string to contain the name first.
                        IntPtr hashDataPtr, pubKeyPtr;
                        UInt32 hashDataLength, pubKeyBytes, asmNameLength, flags;
                        _assemblyImport.GetAssemblyRefProps(asmRefTokens[i], out pubKeyPtr, out pubKeyBytes, null, 0, out asmNameLength, IntPtr.Zero, out hashDataPtr, out hashDataLength, out flags);
                        // Allocate assembly name buffer.
                        char[] asmNameBuf = new char[asmNameLength + 1];
                        IntPtr asmMetaPtr = IntPtr.Zero;
                        // Ensure metadata structure is freed.
                        try
                        {
                            // Allocate metadata structure.
                            asmMetaPtr = AllocAsmMeta();
                            // Retrieve the assembly reference properties.
                            _assemblyImport.GetAssemblyRefProps(asmRefTokens[i], out pubKeyPtr, out pubKeyBytes, asmNameBuf, (uint)asmNameBuf.Length, out asmNameLength, asmMetaPtr, out hashDataPtr, out hashDataLength, out flags);
                            // Construct the assembly name and free metadata structure.
                            AssemblyNameExtension asmName = ConstructAssemblyName(asmMetaPtr, asmNameBuf, asmNameLength, pubKeyPtr, pubKeyBytes, flags);
                            // Add the assembly name to the reference list.
                            asmRefs.Add(asmName);
                        }
                        finally
                        {
                            FreeAsmMeta(asmMetaPtr);
                        }
                    }
                } while (fetched > 0);
            }
            finally
            {
                if (asmRefEnum != IntPtr.Zero)
                    _assemblyImport.CloseEnum(asmRefEnum);
            }

            return (AssemblyNameExtension[])asmRefs.ToArray(typeof(AssemblyNameExtension));
        }

        /// <summary>
        /// Import extra files. These are usually consituent members of a scatter assembly.
        /// </summary>
        /// <returns>The extra files of assembly dependencies.</returns>
        private string[] ImportFiles()
        {
            ArrayList files = new ArrayList();
            IntPtr fileEnum = IntPtr.Zero;
            UInt32[] fileTokens = new UInt32[GENMAN_ENUM_TOKEN_BUF_SIZE];
            char[] fileNameBuf = new char[GENMAN_STRING_BUF_SIZE];
            UInt32 fetched;

            // Ensure the enum handle is closed.
            try
            {
                // Enum chunks of files until we run out.
                do
                {
                    _assemblyImport.EnumFiles(ref fileEnum, fileTokens, (uint)fileTokens.Length, out fetched);

                    for (uint i = 0; i < fetched; i++)
                    {
                        IntPtr hashDataPtr;
                        UInt32 fileNameLength, hashDataLength, fileFlags;

                        // Retrieve file properties.
                        _assemblyImport.GetFileProps(fileTokens[i],
                            fileNameBuf, (uint)fileNameBuf.Length, out fileNameLength,
                            out hashDataPtr, out hashDataLength, out fileFlags);

                        // Add file to file list.
                        string file = new string(fileNameBuf, 0, (int)(fileNameLength - 1));
                        files.Add(file);
                    }
                } while (fetched > 0);
            }
            finally
            {
                if (fileEnum != IntPtr.Zero)
                    _assemblyImport.CloseEnum(fileEnum);
            }

            return (string[])files.ToArray(typeof(string));
        }

        /// <summary>
        /// Allocate assembly metadata structure buffer.
        /// </summary>
        /// <returns>Pointer to structure</returns>
        private IntPtr AllocAsmMeta()
        {
            ASSEMBLYMETADATA asmMeta;
            asmMeta.usMajorVersion = asmMeta.usMinorVersion = asmMeta.usBuildNumber = asmMeta.usRevisionNumber = 0;
            asmMeta.cOses = asmMeta.cProcessors = 0;
            asmMeta.rOses = asmMeta.rpProcessors = IntPtr.Zero;
            // Allocate buffer for locale.
            asmMeta.rpLocale = Marshal.AllocCoTaskMem(GENMAN_LOCALE_BUF_SIZE * 2);
            asmMeta.cchLocale = (uint)GENMAN_LOCALE_BUF_SIZE;
            // Convert to unmanaged structure.
            int size = Marshal.SizeOf<ASSEMBLYMETADATA>();
            IntPtr asmMetaPtr = Marshal.AllocCoTaskMem(size);
            Marshal.StructureToPtr(asmMeta, asmMetaPtr, false);

            return asmMetaPtr;
        }

        /// <summary>
        /// Construct assembly name. 
        /// </summary>
        /// <param name="asmMetaPtr">Assembly metadata structure</param>
        /// <param name="asmNameBuf">Buffer containing the name</param>
        /// <param name="asmNameLength">Length of that buffer</param>
        /// <param name="pubKeyPtr">Pointer to public key</param>
        /// <param name="pubKeyBytes">Count of bytes in public key.</param>
        /// <param name="flags">Extra flags</param>
        /// <returns>The assembly name.</returns>
        private AssemblyNameExtension ConstructAssemblyName(IntPtr asmMetaPtr, char[] asmNameBuf, UInt32 asmNameLength, IntPtr pubKeyPtr, UInt32 pubKeyBytes, UInt32 flags)
        {
            // Marshal the assembly metadata back to a managed type.
            ASSEMBLYMETADATA asmMeta = (ASSEMBLYMETADATA)Marshal.PtrToStructure(asmMetaPtr, typeof(ASSEMBLYMETADATA));

            // Construct the assembly name. (Note asmNameLength should/must be > 0.)
            AssemblyName assemblyName = new AssemblyName();
            assemblyName.Name = new string(asmNameBuf, 0, (int)asmNameLength - 1);
            assemblyName.Version = new Version(asmMeta.usMajorVersion, asmMeta.usMinorVersion, asmMeta.usBuildNumber, asmMeta.usRevisionNumber);


            // Set culture info.
            string locale = Marshal.PtrToStringUni(asmMeta.rpLocale);
            if (locale.Length > 0)
            {
                assemblyName.CultureInfo = CultureInfo.CreateSpecificCulture(locale);
            }
            else
            {
                assemblyName.CultureInfo = CultureInfo.CreateSpecificCulture(String.Empty);
            }


            // Set public key or PKT.
            byte[] publicKey = new byte[pubKeyBytes];
            Marshal.Copy(pubKeyPtr, publicKey, 0, (int)pubKeyBytes);
            if ((flags & (uint)CorAssemblyFlags.afPublicKey) != 0)
            {
                assemblyName.SetPublicKey(publicKey);
            }
            else
            {
                assemblyName.SetPublicKeyToken(publicKey);
            }

            assemblyName.Flags = (AssemblyNameFlags)flags;
            return new AssemblyNameExtension(assemblyName);
        }

        /// <summary>
        /// Free the assembly metadata structure.
        /// </summary>
        /// <param name="asmMetaPtr">The pointer.</param>
        private void FreeAsmMeta(IntPtr asmMetaPtr)
        {
            if (asmMetaPtr != IntPtr.Zero)
            {
                // Marshal the assembly metadata back to a managed type.
                ASSEMBLYMETADATA asmMeta = (ASSEMBLYMETADATA)Marshal.PtrToStructure(asmMetaPtr, typeof(ASSEMBLYMETADATA));
                // Free unmanaged memory.
                Marshal.FreeCoTaskMem(asmMeta.rpLocale);
                Marshal.DestroyStructure(asmMetaPtr, typeof(ASSEMBLYMETADATA));
                Marshal.FreeCoTaskMem(asmMetaPtr);
            }
        }
    }
}
