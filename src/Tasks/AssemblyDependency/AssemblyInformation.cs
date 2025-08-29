// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
#if !FEATURE_ASSEMBLYLOADCONTEXT
using System.Linq;
using System.Runtime.InteropServices;
#endif
using System.Runtime.Versioning;
using System.Reflection;
using System.Text;

using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
#if FEATURE_ASSEMBLYLOADCONTEXT || MONO
using System.Reflection.PortableExecutable;
using System.Reflection.Metadata;
#endif
using Microsoft.Build.Tasks.AssemblyDependency;

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Collection of methods used to discover assembly metadata.
    /// Primarily stolen from manifestutility.cs AssemblyMetaDataImport class.
    /// </summary>
    internal class AssemblyInformation : DisposableBase
    {
        private AssemblyNameExtension[] _assemblyDependencies;
        private string[] _assemblyFiles;
#if !FEATURE_ASSEMBLYLOADCONTEXT
        private readonly IMetaDataDispenser _metadataDispenser;
        private readonly IMetaDataAssemblyImport _assemblyImport;
        private static Guid s_importerGuid = new Guid(((GuidAttribute)Attribute.GetCustomAttribute(typeof(IMetaDataImport), typeof(GuidAttribute), false)).Value);
        private readonly Assembly _assembly;
#endif
        private readonly string _sourceFile;
        private FrameworkName _frameworkName;

#if FEATURE_ASSEMBLYLOADCONTEXT || MONO
        private bool _metadataRead;
#endif

#if !FEATURE_ASSEMBLYLOADCONTEXT && !MONO
        private static string s_targetFrameworkAttribute = "System.Runtime.Versioning.TargetFrameworkAttribute";
#endif
#if !FEATURE_ASSEMBLYLOADCONTEXT
        // Borrowed from genman.
        private const int GENMAN_STRING_BUF_SIZE = 1024;
        private const int GENMAN_LOCALE_BUF_SIZE = 64;
        private const int GENMAN_ENUM_TOKEN_BUF_SIZE = 16; // 128 from genman seems too big.

        static AssemblyInformation()
        {
            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += ReflectionOnlyAssemblyResolve;
        }
#endif // FEATURE_ASSEMBLY_LOADFROM

        /// <summary>
        /// Construct an instance for a source file.
        /// </summary>
        /// <param name="sourceFile">The assembly.</param>
        internal AssemblyInformation(string sourceFile)
        {
            // Extra checks for PInvoke-destined data.
            ErrorUtilities.VerifyThrowArgumentNull(sourceFile, nameof(sourceFile));
            _sourceFile = sourceFile;

#if !FEATURE_ASSEMBLYLOADCONTEXT
            if (NativeMethodsShared.IsWindows)
            {
                // Create the metadata dispenser and open scope on the source file.
                _metadataDispenser = (IMetaDataDispenser)new CorMetaDataDispenser();
                _assemblyImport = (IMetaDataAssemblyImport)_metadataDispenser.OpenScope(sourceFile, 0, ref s_importerGuid);
            }
            else
            {
                _assembly = Assembly.ReflectionOnlyLoadFrom(sourceFile);
            }
#endif
        }

#if !FEATURE_ASSEMBLYLOADCONTEXT
        private static Assembly ReflectionOnlyAssemblyResolve(object sender, ResolveEventArgs args)
        {
            string[] nameParts = args.Name.Split(MSBuildConstants.CommaChar);
            Assembly assembly = null;

            if (args.RequestingAssembly != null && !string.IsNullOrEmpty(args.RequestingAssembly.Location) && nameParts.Length > 0)
            {
                var location = args.RequestingAssembly.Location;
                var newLocation = Path.Combine(Path.GetDirectoryName(location), nameParts[0].Trim() + ".dll");

                try
                {
                    if (FileSystems.Default.FileExists(newLocation))
                    {
                        assembly = Assembly.ReflectionOnlyLoadFrom(newLocation);
                    }
                }
                catch
                {
                }
            }

            // Let's try to automatically load it
            if (assembly == null)
            {
                try
                {
                    assembly = Assembly.ReflectionOnlyLoad(args.Name);
                }
                catch
                {
                }
            }

            return assembly;
        }
#endif

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
        /// <param name="assemblyMetadataCache">Cache of pre-extracted assembly metadata.</param>
        /// <param name="dependencies">Receives the list of dependencies.</param>
        /// <param name="scatterFiles">Receives the list of associated scatter files.</param>
        /// <param name="frameworkName">Gets the assembly name.</param>
        internal static void GetAssemblyMetadata(
            string path,
            ConcurrentDictionary<string, AssemblyMetadata> assemblyMetadataCache,
            out AssemblyNameExtension[] dependencies,
            out string[] scatterFiles,
            out FrameworkName frameworkName)
        {
            var import = assemblyMetadataCache?.GetOrAdd(path, p => new AssemblyMetadata(p))
                ?? new AssemblyMetadata(path);

            dependencies = import.Dependencies;
            frameworkName = import.FrameworkName;
            scatterFiles = import.ScatterFiles;
        }

        /// <summary>
        /// Given an assembly name, crack it open and retrieve the TargetFrameworkAttribute
        /// assemblies and  the list of scatter files.
        /// </summary>
        internal static FrameworkName GetTargetFrameworkAttribute(string path)
        {
            using (var import = new AssemblyInformation(path))
            {
                return import.FrameworkNameAttribute;
            }
        }

        /// <summary>
        /// Determine if an file is a winmd file or not.
        /// </summary>
        internal static bool IsWinMDFile(
            string fullPath,
            GetAssemblyRuntimeVersion getAssemblyRuntimeVersion,
            FileExists fileExists,
            out string imageRuntimeVersion,
            out bool isManagedWinmd)
        {
            imageRuntimeVersion = String.Empty;
            isManagedWinmd = false;

            if (!NativeMethodsShared.IsWindows)
            {
                return false;
            }

            // May be null or empty is the file was never resolved to a path on disk.
            if (!String.IsNullOrEmpty(fullPath) && fileExists(fullPath))
            {
                imageRuntimeVersion = getAssemblyRuntimeVersion(fullPath);
                if (!String.IsNullOrEmpty(imageRuntimeVersion))
                {
                    bool containsWindowsRuntime = imageRuntimeVersion.IndexOf(
                        "WindowsRuntime",
                        StringComparison.OrdinalIgnoreCase) >= 0;

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
            // Disabling use of System.Reflection in case of MONO, because
            // Assembly.GetCustomAttributes* for an attribute which belongs
            // to an assembly that mono cannot find, causes a crash!
            // Instead, opt for using PEReader and friends to get that info
#if !FEATURE_ASSEMBLYLOADCONTEXT && !MONO
            if (!NativeMethodsShared.IsWindows)
            {
                if (String.Equals(Environment.GetEnvironmentVariable("MONO29679"), "1", StringComparison.OrdinalIgnoreCase))
                {
                    // Getting custom attributes in CoreFx contract assemblies is busted
                    // https://bugzilla.xamarin.com/show_bug.cgi?id=29679
                    return null;
                }

                CustomAttributeData attr = null;

                foreach (CustomAttributeData a in _assembly.GetCustomAttributesData())
                {
                    try
                    {
                        if (a.AttributeType == typeof(TargetFrameworkAttribute))
                        {
                            attr = a;
                            break;
                        }
                    }
                    catch
                    {
                    }
                }

                string name = null;
                if (attr != null)
                {
                    name = (string)attr.ConstructorArguments[0].Value;
                }
                return name == null ? null : new FrameworkName(name);
            }

            FrameworkName frameworkAttribute = null;
            try
            {
                var import2 = (IMetaDataImport2)_assemblyImport;

                _assemblyImport.GetAssemblyFromScope(out uint assemblyScope);
                int hr = import2.GetCustomAttributeByName(assemblyScope, s_targetFrameworkAttribute, out IntPtr data, out uint valueLen);

                // get the AssemblyTitle
                if (hr == NativeMethodsShared.S_OK)
                {
                    // if an AssemblyTitle exists, parse the contents of the blob
                    if (NativeMethods.TryReadMetadataString(_sourceFile, data, valueLen, out string frameworkNameAttribute))
                    {
                        if (!String.IsNullOrEmpty(frameworkNameAttribute))
                        {
                            frameworkAttribute = new FrameworkName(frameworkNameAttribute);
                        }
                    }
                }
            }
            catch (Exception e) when (!ExceptionHandling.IsCriticalException(e))
            {
            }

            return frameworkAttribute;
#else
            CorePopulateMetadata();
            return _frameworkName;
#endif
        }

#if FEATURE_ASSEMBLYLOADCONTEXT || MONO
        /// <summary>
        /// Read everything from the assembly in a single stream.
        /// </summary>
        /// <returns></returns>
        private void CorePopulateMetadata()
        {
            if (_metadataRead)
            {
                return;
            }

            lock (this)
            {
                if (_metadataRead)
                {
                    return;
                }

                using (var stream = File.OpenRead(_sourceFile))
                using (var peFile = new PEReader(stream))
                {
                    bool hasMetadata = false;
                    try
                    {
                        // This can throw if the stream is too small, which means
                        // the assembly doesn't have metadata.
                        hasMetadata = peFile.HasMetadata;
                    }
                    finally
                    {
                        // If the file does not contain PE metadata, throw BadImageFormatException to preserve
                        // behavior from AssemblyName.GetAssemblyName(). RAR will deal with this correctly.
                        if (!hasMetadata)
                        {
                            throw new BadImageFormatException(string.Format(CultureInfo.CurrentCulture,
                                AssemblyResources.GetString("ResolveAssemblyReference.AssemblyDoesNotContainPEMetadata"),
                                _sourceFile));
                        }
                    }

                    var metadataReader = peFile.GetMetadataReader();

                    var assemblyReferences = metadataReader.AssemblyReferences;

                    List<AssemblyNameExtension> ret = new List<AssemblyNameExtension>(assemblyReferences.Count);

                    foreach (var handle in assemblyReferences)
                    {
                        var assemblyName = GetAssemblyName(metadataReader, handle);
                        ret.Add(new AssemblyNameExtension(assemblyName));
                    }

                    _assemblyDependencies = ret.ToArray();

                    foreach (var attrHandle in metadataReader.GetAssemblyDefinition().GetCustomAttributes())
                    {
                        var attr = metadataReader.GetCustomAttribute(attrHandle);

                        var ctorHandle = attr.Constructor;
                        if (ctorHandle.Kind != HandleKind.MemberReference)
                        {
                            continue;
                        }

                        var container = metadataReader.GetMemberReference((MemberReferenceHandle)ctorHandle).Parent;
                        if (container.Kind != HandleKind.TypeReference)
                        {
                            continue;
                        }

                        var name = metadataReader.GetTypeReference((TypeReferenceHandle)container).Name;
                        if (!string.Equals(metadataReader.GetString(name), "TargetFrameworkAttribute"))
                        {
                            continue;
                        }

                        var arguments = GetFixedStringArguments(metadataReader, attr);
                        if (arguments.Count == 1)
                        {
                            _frameworkName = new FrameworkName(arguments[0]);
                        }
                    }

                    var assemblyFilesCollection = metadataReader.AssemblyFiles;

                    List<string> assemblyFiles = new List<string>(assemblyFilesCollection.Count);

                    foreach (var fileHandle in assemblyFilesCollection)
                    {
                        assemblyFiles.Add(metadataReader.GetString(metadataReader.GetAssemblyFile(fileHandle).Name));
                    }

                    _assemblyFiles = assemblyFiles.ToArray();
                }

                _metadataRead = true;
            }
        }

        // https://github.com/dotnet/msbuild/issues/4002
        // https://github.com/dotnet/corefx/issues/34008
        //
        // We do not use AssemblyReference.GetAssemblyName() here because its behavior
        // is different from other code paths with respect to neutral culture. We will
        // get unspecified culture instead of explicitly neutral culture. This in turn
        // leads string comparisons of assembly-name-modulo-version in RAR to false
        // negatives that break its conflict resolution and binding redirect generation.
        private static AssemblyName GetAssemblyName(MetadataReader metadataReader, AssemblyReferenceHandle handle)
        {
            var entry = metadataReader.GetAssemblyReference(handle);

            var assemblyName = new AssemblyName
            {
                Name = metadataReader.GetString(entry.Name),
                Version = entry.Version,
                CultureName = metadataReader.GetString(entry.Culture)
            };

            var publicKeyOrToken = metadataReader.GetBlobBytes(entry.PublicKeyOrToken);
            if (publicKeyOrToken != null)
            {
                if (publicKeyOrToken.Length <= 8)
                {
                    assemblyName.SetPublicKeyToken(publicKeyOrToken);
                }
                else
                {
                    assemblyName.SetPublicKey(publicKeyOrToken);
                }
            }

            assemblyName.Flags = (AssemblyNameFlags)(int)entry.Flags;
            return assemblyName;
        }
#endif

        // Enabling this for MONO, because it's required by GetFrameworkName.
        // More details are in the comment for that method
#if FEATURE_ASSEMBLYLOADCONTEXT || MONO
        // This method copied from DNX source: https://github.com/aspnet/dnx/blob/e0726f769aead073af2d8cd9db47b89e1745d574/src/Microsoft.Dnx.Tooling/Utils/LockFileUtils.cs#L385
        //  System.Reflection.Metadata 1.1 is expected to have an API that helps with this.
        /// <summary>
        /// Gets the fixed (required) string arguments of a custom attribute.
        /// Only attributes that have only fixed string arguments.
        /// </summary>
        private static List<string> GetFixedStringArguments(MetadataReader reader, CustomAttribute attribute)
        {
            // TODO: Nick Guerrera (Nick.Guerrera@microsoft.com) hacked this method for temporary use.
            // There is a blob decoder feature in progress but it won't ship in time for our milestone.
            // Replace this method with the blob decoder feature when later it is availale.

            var signature = reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor).Signature;
            var signatureReader = reader.GetBlobReader(signature);
            var valueReader = reader.GetBlobReader(attribute.Value);
            var arguments = new List<string>();

            var prolog = valueReader.ReadUInt16();
            if (prolog != 1)
            {
                // Invalid custom attribute prolog
                return arguments;
            }

            var header = signatureReader.ReadSignatureHeader();
            if (header.Kind != SignatureKind.Method || header.IsGeneric)
            {
                // Invalid custom attribute constructor signature
                return arguments;
            }

            int parameterCount;
            if (!signatureReader.TryReadCompressedInteger(out parameterCount))
            {
                // Invalid custom attribute constructor signature
                return arguments;
            }

            var returnType = signatureReader.ReadSignatureTypeCode();
            if (returnType != SignatureTypeCode.Void)
            {
                // Invalid custom attribute constructor signature
                return arguments;
            }

            for (int i = 0; i < parameterCount; i++)
            {
                var signatureTypeCode = signatureReader.ReadSignatureTypeCode();
                if (signatureTypeCode == SignatureTypeCode.String)
                {
                    // Custom attribute constructor must take only strings
                    arguments.Add(valueReader.ReadSerializedString());
                }
            }

            return arguments;
        }
#endif

#if !FEATURE_ASSEMBLYLOADCONTEXT
        /// <summary>
        /// Release interface pointers on Dispose(). 
        /// </summary>
        protected override void DisposeUnmanagedResources()
        {
            if (NativeMethodsShared.IsWindows)
            {
                if (_assemblyImport != null)
                {
                    Marshal.ReleaseComObject(_assemblyImport);
                }

                if (_metadataDispenser != null)
                {
                    Marshal.ReleaseComObject(_metadataDispenser);
                }
            }
        }
#endif

        /// <summary>
        /// Given a path get the CLR runtime version of the file
        /// </summary>
        /// <param name="path">path to the file</param>
        /// <returns>The CLR runtime version or empty if the path does not exist.</returns>
        internal static string GetRuntimeVersion(string path)
        {
#if FEATURE_MSCOREE
            if (NativeMethodsShared.IsWindows)
            {
#if DEBUG
                // Just to make sure and exercise the code that uses dwLength to allocate the buffer
                // when GetRequestedRuntimeInfo fails due to insufficient buffer size.
                int bufferLength = 1;
#else
                int bufferLength = 11; // 11 is the length of a runtime version and null terminator v2.0.50727/0
#endif

                unsafe
                {
                    // Allocate an initial buffer 
                    char* runtimeVersion = stackalloc char[bufferLength];

                    // Run GetFileVersion, this should succeed using the initial buffer.
                    // It also returns the dwLength which is used if there is insufficient buffer.
                    uint hresult = NativeMethods.GetFileVersion(path, runtimeVersion, bufferLength, out int dwLength);

                    if (hresult == NativeMethodsShared.ERROR_INSUFFICIENT_BUFFER)
                    {
                        // Allocate new buffer based on the returned length.
                        char* runtimeVersion2 = stackalloc char[dwLength];
                        runtimeVersion = runtimeVersion2;

                        // Get the RuntimeVersion in this second call.
                        bufferLength = dwLength;
                        hresult = NativeMethods.GetFileVersion(path, runtimeVersion, bufferLength, out dwLength);
                    }

                    return hresult == NativeMethodsShared.S_OK ? new string(runtimeVersion, 0, dwLength - 1) : string.Empty;
                }
            }
            else
            {
                return ManagedRuntimeVersionReader.GetRuntimeVersion(path);
            }
#else
            return ManagedRuntimeVersionReader.GetRuntimeVersion(path);
#endif
        }

        /// <summary>
        /// Import assembly dependencies.
        /// </summary>
        /// <returns>The array of assembly dependencies.</returns>
        private AssemblyNameExtension[] ImportAssemblyDependencies()
        {
#if !FEATURE_ASSEMBLYLOADCONTEXT
            var asmRefs = new List<AssemblyNameExtension>();

            if (!NativeMethodsShared.IsWindows)
            {
                return _assembly.GetReferencedAssemblies().Select(a => new AssemblyNameExtension(a)).ToArray();
            }

            IntPtr asmRefEnum = IntPtr.Zero;
            var asmRefTokens = new UInt32[GENMAN_ENUM_TOKEN_BUF_SIZE];
            // Ensure the enum handle is closed.
            try
            {
                // Enum chunks of refs in 16-ref blocks until we run out.
                UInt32 fetched;
                do
                {
                    _assemblyImport.EnumAssemblyRefs(
                        ref asmRefEnum,
                        asmRefTokens,
                        (uint)asmRefTokens.Length,
                        out fetched);

                    for (uint i = 0; i < fetched; i++)
                    {
                        // Determine the length of the string to contain the name first.
                        _assemblyImport.GetAssemblyRefProps(
                            asmRefTokens[i],
                            out IntPtr pubKeyPtr,
                            out uint pubKeyBytes,
                            null,
                            0,
                            out uint asmNameLength,
                            IntPtr.Zero,
                            out _,
                            out _,
                            out uint flags);
                        // Allocate assembly name buffer.
                        var asmNameBuf = new char[asmNameLength + 1];
                        IntPtr asmMetaPtr = IntPtr.Zero;
                        // Ensure metadata structure is freed.
                        try
                        {
                            // Allocate metadata structure.
                            asmMetaPtr = AllocAsmMeta();
                            // Retrieve the assembly reference properties.
                            _assemblyImport.GetAssemblyRefProps(
                                asmRefTokens[i],
                                out pubKeyPtr,
                                out pubKeyBytes,
                                asmNameBuf,
                                (uint)asmNameBuf.Length,
                                out asmNameLength,
                                asmMetaPtr,
                                out _,
                                out _,
                                out flags);
                            // Construct the assembly name and free metadata structure.
                            AssemblyNameExtension asmName = ConstructAssemblyName(
                                asmMetaPtr,
                                asmNameBuf,
                                asmNameLength,
                                pubKeyPtr,
                                pubKeyBytes,
                                flags);
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
                {
                    _assemblyImport.CloseEnum(asmRefEnum);
                }
            }

            return asmRefs.ToArray();
#else
            CorePopulateMetadata();
            return _assemblyDependencies;
#endif
        }


        /// <summary>
        /// Import extra files. These are usually consituent members of a scatter assembly.
        /// </summary>
        /// <returns>The extra files of assembly dependencies.</returns>
        private string[] ImportFiles()
        {
#if !FEATURE_ASSEMBLYLOADCONTEXT
            var files = new List<string>();
            IntPtr fileEnum = IntPtr.Zero;
            var fileTokens = new UInt32[GENMAN_ENUM_TOKEN_BUF_SIZE];
            var fileNameBuf = new char[GENMAN_STRING_BUF_SIZE];

            // Ensure the enum handle is closed.
            try
            {
                // Enum chunks of files until we run out.
                UInt32 fetched;
                do
                {
                    _assemblyImport.EnumFiles(ref fileEnum, fileTokens, (uint)fileTokens.Length, out fetched);

                    for (uint i = 0; i < fetched; i++)
                    {
                        // Retrieve file properties.
                        _assemblyImport.GetFileProps(fileTokens[i],
                            fileNameBuf, (uint)fileNameBuf.Length, out uint fileNameLength,
                            out _, out _, out _);

                        // Add file to file list.
                        string file = new string(fileNameBuf, 0, (int)(fileNameLength - 1));
                        files.Add(file);
                    }
                } while (fetched > 0);
            }
            finally
            {
                if (fileEnum != IntPtr.Zero)
                {
                    _assemblyImport.CloseEnum(fileEnum);
                }
            }

            return files.ToArray();
#else
            CorePopulateMetadata();
            return _assemblyFiles;
#endif
        }

#if !FEATURE_ASSEMBLYLOADCONTEXT
        /// <summary>
        /// Allocate assembly metadata structure buffer.
        /// </summary>
        /// <returns>Pointer to structure</returns>
        private static IntPtr AllocAsmMeta()
        {
            ASSEMBLYMETADATA asmMeta;
            asmMeta.usMajorVersion = asmMeta.usMinorVersion = asmMeta.usBuildNumber = asmMeta.usRevisionNumber = 0;
            asmMeta.cOses = asmMeta.cProcessors = 0;
            asmMeta.rOses = asmMeta.rpProcessors = IntPtr.Zero;
            // Allocate buffer for locale.
            asmMeta.rpLocale = Marshal.AllocCoTaskMem(GENMAN_LOCALE_BUF_SIZE * 2);
            asmMeta.cchLocale = GENMAN_LOCALE_BUF_SIZE;
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
        private static AssemblyNameExtension ConstructAssemblyName(IntPtr asmMetaPtr, char[] asmNameBuf, UInt32 asmNameLength, IntPtr pubKeyPtr, UInt32 pubKeyBytes, UInt32 flags)
        {
            // Marshal the assembly metadata back to a managed type.
            ASSEMBLYMETADATA asmMeta = (ASSEMBLYMETADATA)Marshal.PtrToStructure(asmMetaPtr, typeof(ASSEMBLYMETADATA));

            // Construct the assembly name. (Note asmNameLength should/must be > 0.)
            var assemblyName = new AssemblyName
            {
                Name = new string(asmNameBuf, 0, (int)asmNameLength - 1),
                Version = new Version(
                    asmMeta.usMajorVersion,
                    asmMeta.usMinorVersion,
                    asmMeta.usBuildNumber,
                    asmMeta.usRevisionNumber)
            };

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
            var publicKey = new byte[pubKeyBytes];
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
        private static void FreeAsmMeta(IntPtr asmMetaPtr)
        {
            if (asmMetaPtr != IntPtr.Zero)
            {
                // Marshal the assembly metadata back to a managed type.
                var asmMeta = (ASSEMBLYMETADATA)Marshal.PtrToStructure(asmMetaPtr, typeof(ASSEMBLYMETADATA));
                // Free unmanaged memory.
                Marshal.FreeCoTaskMem(asmMeta.rpLocale);
                Marshal.DestroyStructure(asmMetaPtr, typeof(ASSEMBLYMETADATA));
                Marshal.FreeCoTaskMem(asmMetaPtr);
            }
        }
#endif
    }

    /// <summary>
    /// Managed implementation of a reader for getting the runtime version of an assembly
    /// </summary>
    internal static class ManagedRuntimeVersionReader
    {
        private class HeaderInfo
        {
            public uint VirtualAddress;
            public uint Size;
            public uint FileOffset;
        }

        /// <summary>
        /// Given a path get the CLR runtime version of the file.
        /// </summary>
        /// <param name="path">path to the file</param>
        /// <returns>The CLR runtime version or empty if the path does not exist or the file is not an assembly.</returns>
        public static string GetRuntimeVersion(string path)
        {
            if (!FileSystems.Default.FileExists(path))
            {
                return string.Empty;
            }

            using Stream stream = File.OpenRead(path);
            using BinaryReader reader = new BinaryReader(stream);
            return GetRuntimeVersion(reader);
        }

        /// <summary>
        /// Given a <see cref="BinaryReader"/> get the CLR runtime version of the underlying file.
        /// </summary>
        /// <param name="sr">A <see cref="BinaryReader"/> positioned at the first byte of the file.</param>
        /// <returns>The CLR runtime version or empty if the data does not represent an assembly.</returns>
        internal static string GetRuntimeVersion(BinaryReader sr)
        {
            // This algorithm for getting the runtime version is based on
            // the ECMA Standard 335: The Common Language Infrastructure (CLI)
            // http://www.ecma-international.org/publications/files/ECMA-ST/ECMA-335.pdf

            try
            {
                const uint PEHeaderPointerOffset = 0x3c;
                const uint PEHeaderSize = 20;
                const uint OptionalPEHeaderSize = 224;
                const uint OptionalPEPlusHeaderSize = 240;
                const uint SectionHeaderSize = 40;

                // The PE file format is specified in section II.25

                // A PE image starts with an MS-DOS header followed by a PE signature, followed by the PE file header,
                // and then the PE optional header followed by PE section headers.
                // There must be room for all of that.

                if (sr.BaseStream.Length < PEHeaderPointerOffset + 4 + PEHeaderSize + OptionalPEHeaderSize +
                    SectionHeaderSize)
                {
                    return string.Empty;
                }

                // The PE format starts with an MS-DOS stub of 128 bytes.
                // At offset 0x3c in the DOS header is a 4-byte unsigned integer offset to the PE
                // signature (shall be “PE\0\0”), immediately followed by the PE file header

                sr.BaseStream.Position = PEHeaderPointerOffset;
                var peHeaderOffset = sr.ReadUInt32();

                if (peHeaderOffset + 4 + PEHeaderSize + OptionalPEHeaderSize + SectionHeaderSize >=
                    sr.BaseStream.Length)
                {
                    return string.Empty;
                }

                // The PE header is specified in section II.25.2
                // Read the PE header signature

                sr.BaseStream.Position = peHeaderOffset;
                if (!ReadBytes(sr, (byte)'P', (byte)'E', 0, 0))
                {
                    return string.Empty;
                }

                // The PE header immediately follows the signature
                var peHeaderBase = peHeaderOffset + 4;

                // At offset 2 of the PE header there is the number of sections
                sr.BaseStream.Position = peHeaderBase + 2;
                var numberOfSections = sr.ReadUInt16();
                if (numberOfSections > 96)
                {
                    return string.Empty; // There can't be more than 96 sections, something is wrong
                }

                // Immediately after the PE Header is the PE Optional Header.
                // This header is optional in the general PE spec, but always
                // present in assembly files.
                // From this header we'll get the CLI header RVA, which is
                // at offset 208 for PE32, and at offset 224 for PE32+

                var optionalHeaderOffset = peHeaderBase + PEHeaderSize;

                uint cliHeaderRvaOffset;
                uint optionalPEHeaderSize;

                sr.BaseStream.Position = optionalHeaderOffset;
                var magicNumber = sr.ReadUInt16();

                if (magicNumber == 0x10b) // PE32
                {
                    optionalPEHeaderSize = OptionalPEHeaderSize;
                    cliHeaderRvaOffset = optionalHeaderOffset + 208;
                }
                else if (magicNumber == 0x20b) // PE32+
                {
                    optionalPEHeaderSize = OptionalPEPlusHeaderSize;
                    cliHeaderRvaOffset = optionalHeaderOffset + 224;
                }
                else
                {
                    return string.Empty;
                }

                // Read the CLI header RVA

                sr.BaseStream.Position = cliHeaderRvaOffset;
                var cliHeaderRva = sr.ReadUInt32();
                if (cliHeaderRva == 0)
                {
                    return string.Empty; // No CLI section
                }

                // Immediately following the optional header is the Section
                // Table, which contains a number of section headers.
                // Section headers are specified in section II.25.3

                // Each section header has the base RVA, size, and file
                // offset of the section. To find the file offset of the
                // CLI header we need to find a section that contains
                // its RVA, and the calculate the file offset using
                // the base file offset of the section.

                var sectionOffset = optionalHeaderOffset + optionalPEHeaderSize;

                // Read all section headers, we need them to make RVA to
                // offset conversions.

                var sections = new HeaderInfo[numberOfSections];
                for (int n = 0; n < numberOfSections; n++)
                {
                    // At offset 8 of the section is the section size
                    // and base RVA. At offset 20 there is the file offset
                    sr.BaseStream.Position = sectionOffset + 8;
                    var sectionSize = sr.ReadUInt32();
                    var sectionRva = sr.ReadUInt32();
                    sr.BaseStream.Position = sectionOffset + 20;
                    var sectionDataOffset = sr.ReadUInt32();
                    sections[n] = new HeaderInfo
                    {
                        VirtualAddress = sectionRva,
                        Size = sectionSize,
                        FileOffset = sectionDataOffset
                    };
                    sectionOffset += SectionHeaderSize;
                }

                uint cliHeaderOffset = RvaToOffset(sections, cliHeaderRva);

                // CLI section not found
                if (cliHeaderOffset == 0)
                {
                    return string.Empty;
                }

                // The CLI header is specified in section II.25.3.3.
                // It contains all of the runtime-specific data entries and other information.
                // From the CLI header we need to get the RVA of the metadata root,
                // which is located at offset 8.

                sr.BaseStream.Position = cliHeaderOffset + 8;
                var metadataRva = sr.ReadUInt32();

                var metadataOffset = RvaToOffset(sections, metadataRva);
                if (metadataOffset == 0)
                {
                    return string.Empty;
                }

                // The metadata root is specified in section II.24.2.1
                // The first 4 bytes contain a signature.
                // The version string is at offset 12.

                sr.BaseStream.Position = metadataOffset;
                if (!ReadBytes(sr, 0x42, 0x53, 0x4a, 0x42)) // Metadata root signature
                {
                    return string.Empty;
                }

                // Read the version string length
                sr.BaseStream.Position = metadataOffset + 12;
                var length = sr.ReadInt32();
                if (length > 255 || length <= 0 || sr.BaseStream.Position + length >= sr.BaseStream.Length)
                {
                    return string.Empty;
                }

                // Read the version string
                var v = Encoding.UTF8.GetString(sr.ReadBytes(length));

                // Per II.24.2.1, version string length is rounded up
                // to a multiple of 4. So we may read eg "4.0.30319\0\0"
                // Version.Parse works fine, but it's not pretty in the log.
                int firstNull = v.IndexOf('\0');
                if (firstNull > 0)
                {
                    v = v.Substring(0, firstNull);
                }

                return v;
            }
            catch
            {
                // Something went wrong in spite of all checks. Corrupt file?
                return string.Empty;
            }
        }

        private static bool ReadBytes(BinaryReader r, params byte[] bytes)
        {
            foreach (byte b in bytes)
            {
                if (b != r.ReadByte())
                {
                    return false;
                }
            }

            return true;
        }

        private static uint RvaToOffset(HeaderInfo[] sections, uint rva)
        {
            foreach (var s in sections)
            {
                if (rva >= s.VirtualAddress && rva < s.VirtualAddress + s.Size)
                {
                    return s.FileOffset + (rva - s.VirtualAddress);
                }
            }
            return 0;
        }
    }
}
