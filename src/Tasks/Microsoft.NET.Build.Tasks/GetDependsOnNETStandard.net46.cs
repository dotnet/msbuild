// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET46
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.NET.Build.Tasks
{
    public partial class GetDependsOnNETStandard
    {
        private static Guid s_importerGuid = new Guid("7DAC8207-D3AE-4c75-9B67-92801A497D44");

        internal static bool GetFileDependsOnNETStandard(string filePath)
        {
            // Ported from Microsoft.Build.Tasks.AssemblyInformation
            if (Environment.OSVersion.Platform == PlatformID.Unix || Environment.OSVersion.Platform == PlatformID.MacOSX)
            {
                // on Unix/Mac (mono) use ReflectionOnlyLoadFrom to examine dependencies
                // known issue: since this doesn't create an isolated app domain this will fail if more than one
                // assembly with the same name is analyzed by this task.  The same issue exists in ResolveAssemblyReferences
                // so we are not attempting to fix it here.
                var assembly = Assembly.ReflectionOnlyLoadFrom(filePath);

                foreach(var referencedAssembly in assembly.GetReferencedAssemblies())
                {
                    if (referencedAssembly.Name.Equals(NetStandardAssemblyName, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }
            else
            {
                // on Windows use CLR's unmanaged metadata API.
                // Create the metadata dispenser and open scope on the source file.
                IMetaDataDispenser metadataDispenser = (IMetaDataDispenser)new CorMetaDataDispenser();
                IMetaDataAssemblyImport assemblyImport = (IMetaDataAssemblyImport)metadataDispenser.OpenScope(filePath, 0, ref s_importerGuid);

                IntPtr asmRefEnum = IntPtr.Zero;
                UInt32[] asmRefTokens = new UInt32[16];
                UInt32 fetched;
                // Ensure the enum handle is closed.
                try
                {
                    // Enum chunks of refs in 16-ref blocks until we run out.
                    do
                    {
                        assemblyImport.EnumAssemblyRefs(
                            ref asmRefEnum,
                            asmRefTokens,
                            (uint)asmRefTokens.Length,
                            out fetched);

                        for (uint i = 0; i < fetched; i++)
                        {
                            // Determine the length of the string to contain the name first.
                            IntPtr hashDataPtr, pubKeyPtr;
                            UInt32 hashDataLength, pubKeyBytes, asmNameLength, flags;
                            assemblyImport.GetAssemblyRefProps(
                                asmRefTokens[i],
                                out pubKeyPtr,
                                out pubKeyBytes,
                                null,
                                0,
                                out asmNameLength,
                                IntPtr.Zero,
                                out hashDataPtr,
                                out hashDataLength,
                                out flags);

                            // Allocate assembly name buffer.
                            StringBuilder assemblyNameBuffer = new StringBuilder((int)asmNameLength + 1);

                            // Retrieve the assembly reference properties.
                            assemblyImport.GetAssemblyRefProps(
                                asmRefTokens[i],
                                out pubKeyPtr,
                                out pubKeyBytes,
                                assemblyNameBuffer,
                                (uint)assemblyNameBuffer.Capacity,
                                out asmNameLength,
                                IntPtr.Zero,
                                out hashDataPtr,
                                out hashDataLength,
                                out flags);

                            var assemblyName = assemblyNameBuffer.ToString();

                            if (assemblyName.Equals(NetStandardAssemblyName, StringComparison.Ordinal))
                            {
                                return true;
                            }
                        }
                    } while (fetched > 0);
                }
                finally
                {
                    if (asmRefEnum != IntPtr.Zero)
                    {
                        assemblyImport.CloseEnum(asmRefEnum);
                    }
                }
            }
            return false;
        }

        #region Interop
        
        [ComImport]
        [Guid("E5CB7A31-7512-11d2-89CE-0080C792E5D8")]
        [TypeLibType(TypeLibTypeFlags.FCanCreate)]
        [ClassInterface(ClassInterfaceType.None)]
        internal class CorMetaDataDispenser
        {
        }

        [ComImport]
        [Guid("809c652e-7396-11d2-9771-00a0c9b4d50c")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown /*0x0001*/)]
        [TypeLibType(TypeLibTypeFlags.FRestricted /*0x0200*/)]
        internal interface IMetaDataDispenser
        {
            [return: MarshalAs(UnmanagedType.Interface)]
            object DefineScope([In] ref Guid rclsid, [In] UInt32 dwCreateFlags, [In] ref Guid riid);

            [return: MarshalAs(UnmanagedType.Interface)]
            object OpenScope([In][MarshalAs(UnmanagedType.LPWStr)]  string szScope, [In] UInt32 dwOpenFlags, [In] ref Guid riid);

            [return: MarshalAs(UnmanagedType.Interface)]
            object OpenScopeOnMemory([In] IntPtr pData, [In] UInt32 cbData, [In] UInt32 dwOpenFlags, [In] ref Guid riid);
        }
        
        [ComImport]
        [Guid("EE62470B-E94B-424e-9B7C-2F00C9249F93")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IMetaDataAssemblyImport
        {
            void GetAssemblyProps(UInt32 mdAsm, out IntPtr pPublicKeyPtr, out UInt32 ucbPublicKeyPtr, out UInt32 uHashAlg, StringBuilder strName, UInt32 cchNameIn, out UInt32 cchNameRequired, IntPtr amdInfo, out UInt32 dwFlags);
            void GetAssemblyRefProps(UInt32 mdAsmRef, out IntPtr ppbPublicKeyOrToken, out UInt32 pcbPublicKeyOrToken, StringBuilder strName, UInt32 cchNameIn, out UInt32 pchNameOut, IntPtr amdInfo, out IntPtr ppbHashValue, out UInt32 pcbHashValue, out UInt32 pdwAssemblyRefFlags);
            void GetFileProps([In] UInt32 mdFile, StringBuilder strName, UInt32 cchName, out UInt32 cchNameRequired, out IntPtr bHashData, out UInt32 cchHashBytes, out UInt32 dwFileFlags);
            void GetExportedTypeProps();
            void GetManifestResourceProps();
            void EnumAssemblyRefs([In, Out] ref IntPtr phEnum, [MarshalAs(UnmanagedType.LPArray), Out] UInt32[] asmRefs, System.UInt32 asmRefCount, out System.UInt32 iFetched);
            void EnumFiles([In, Out] ref IntPtr phEnum, [MarshalAs(UnmanagedType.LPArray), Out] UInt32[] fileRefs, System.UInt32 fileRefCount, out System.UInt32 iFetched);
            void EnumExportedTypes();
            void EnumManifestResources();
            void GetAssemblyFromScope(out UInt32 mdAsm);
            void FindExportedTypeByName();
            void FindManifestResourceByName();
            // PreserveSig because this method is an exception that 
            // actually returns void, not HRESULT.
            [PreserveSig]
            void CloseEnum([In] IntPtr phEnum);
            void FindAssembliesByName();
        }
        #endregion
    }
}

#endif