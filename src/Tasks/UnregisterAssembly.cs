// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if FEATURE_APPDOMAIN

using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;
using System.Runtime.InteropServices.ComTypes;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Registers a managed assembly for COM interop (equivalent of regasm.exe functionality, but this code
    /// doesn't actually call the exe).
    /// </summary>
    public class UnregisterAssembly : AppDomainIsolatedTaskExtension
    {
        #region Properties

        public ITaskItem[] Assemblies { get; set; }

        public ITaskItem[] TypeLibFiles { get; set; }

        /// <summary>
        /// The cache file for Register/UnregisterAssembly. Necessary for UnregisterAssembly to do the proper clean up.
        /// </summary>
        public ITaskItem AssemblyListFile { get; set; }

        #endregion

        #region ITask members

        /// <summary>
        /// Task entry point
        /// </summary>
        /// <returns></returns>
        public override bool Execute()
        {
            AssemblyRegistrationCache cacheFile;

            if (AssemblyListFile != null)
            {
                cacheFile = (AssemblyRegistrationCache)StateFileBase.DeserializeCache(AssemblyListFile.ItemSpec, Log, typeof(AssemblyRegistrationCache));

                // no cache file, nothing to do. In case there was a problem reading the cache file, we can't do anything anyway.
                if (cacheFile == null)
                {
                    StateFileBase.DeleteFile(AssemblyListFile.ItemSpec, Log);
                    return true;
                }
            }
            else
            {
                if (Assemblies == null)
                {
                    Log.LogErrorWithCodeFromResources("UnregisterAssembly.AssemblyPathOrStateFileIsRequired", GetType().Name);
                    return false;
                }

                // TypeLibFiles isn't [Required], but if it is specified, it must have the same length as Assemblies
                if (TypeLibFiles != null && TypeLibFiles.Length != Assemblies.Length)
                {
                    Log.LogErrorWithCodeFromResources("General.TwoVectorsMustHaveSameLength", Assemblies.Length, TypeLibFiles.Length, "Assemblies", "TypeLibFiles");
                    return false;
                }

                cacheFile = new AssemblyRegistrationCache();

                for (int i = 0; i < Assemblies.Length; i++)
                {
                    // if the type lib path is not supplied, generate default one
                    if (TypeLibFiles != null && TypeLibFiles[i] != null && TypeLibFiles[i].ItemSpec.Length > 0)
                    {
                        cacheFile.AddEntry(Assemblies[i].ItemSpec, TypeLibFiles[i].ItemSpec);
                    }
                    else
                    {
                        cacheFile.AddEntry(Assemblies[i].ItemSpec, Path.ChangeExtension(Assemblies[i].ItemSpec, ".tlb"));
                    }
                }
            }

            bool taskReturnValue = true;

            try
            {
                for (int i = 0; i < cacheFile.Count; i++)
                {
                    cacheFile.GetEntry(i, out string assemblyPath, out string typeLibraryPath);

                    try
                    {
                        // If one of assemblies failed to unregister, the whole task failed.
                        // We still process the rest of assemblies though.
                        if (!Unregister(assemblyPath, typeLibraryPath))
                        {
                            taskReturnValue = false;
                        }
                    }
                    catch (ArgumentException ex) // assembly path has invalid chars in it
                    {
                        Log.LogErrorWithCodeFromResources("General.InvalidAssemblyName", assemblyPath, ex.Message);
                        taskReturnValue = false;
                    }
#if _DEBUG
                    catch (Exception e)
                    {
                        Debug.Assert(false, "Unexpected exception in AssemblyRegistration.Execute. " + 
                            "Please log a MSBuild bug specifying the steps to reproduce the problem. " +
                            e.Message);
                        throw;
                    }
#endif
                }
            }
            finally
            {
                if (AssemblyListFile != null)
                {
                    StateFileBase.DeleteFile(AssemblyListFile.ItemSpec, Log);
                }
            }

            return taskReturnValue;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Helper unregistration method
        /// </summary>
        private bool Unregister(string assemblyPath, string typeLibPath)
        {
            ErrorUtilities.VerifyThrowArgumentNull(typeLibPath, "typeLibPath");

            Log.LogMessageFromResources(MessageImportance.Low, "UnregisterAssembly.UnregisteringAssembly", assemblyPath);

            if (File.Exists(assemblyPath))
            {
                try
                {
                    // Load the specified assembly. 
                    Assembly asm = Assembly.UnsafeLoadFrom(assemblyPath);

                    var comRegistrar = new RegistrationServices();

                    try
                    {
                        s_unregisteringLock.WaitOne();

                        // Unregister the assembly
                        if (!comRegistrar.UnregisterAssembly(asm))
                        {
                            // If the assembly doesn't contain any types that could be registered for COM interop, 
                            // warn the user about it
                            Log.LogWarningWithCodeFromResources("UnregisterAssembly.NoValidTypes", assemblyPath);
                        }
                    }
                    finally
                    {
                        s_unregisteringLock.ReleaseMutex();
                    }
                }
                catch (ArgumentNullException e)
                {
                    Log.LogErrorWithCodeFromResources("UnregisterAssembly.CantUnregisterAssembly", assemblyPath, e.Message);
                    return false;
                }
                catch (InvalidOperationException e)
                {
                    Log.LogErrorWithCodeFromResources("UnregisterAssembly.CantUnregisterAssembly", assemblyPath, e.Message);
                    return false;
                }
                catch (TargetInvocationException e)
                {
                    Log.LogErrorWithCodeFromResources("UnregisterAssembly.CantUnregisterAssembly", assemblyPath, e.Message);
                    return false;
                }
                catch (IOException e)
                {
                    Log.LogErrorWithCodeFromResources("UnregisterAssembly.CantUnregisterAssembly", assemblyPath, e.Message);
                    return false;
                }
                catch (TypeLoadException e)
                {
                    Log.LogErrorWithCodeFromResources("UnregisterAssembly.CantUnregisterAssembly", assemblyPath, e.Message);
                    return false;
                }
                catch (UnauthorizedAccessException e)
                {
                    Log.LogErrorWithCodeFromResources("UnregisterAssembly.UnauthorizedAccess", assemblyPath, e.Message);
                    return false;
                }
                catch (BadImageFormatException)
                {
                    Log.LogErrorWithCodeFromResources("General.InvalidAssembly", assemblyPath);
                    return false;
                }
                catch (SecurityException e) // running as normal user
                {
                    Log.LogErrorWithCodeFromResources("UnregisterAssembly.UnauthorizedAccess", assemblyPath, e.Message);
                    return false;
                }
            }
            else
            {
                Log.LogWarningWithCodeFromResources("UnregisterAssembly.UnregisterAsmFileDoesNotExist", assemblyPath);
            }

            Log.LogMessageFromResources(MessageImportance.Low, "UnregisterAssembly.UnregisteringTypeLib", typeLibPath);

            if (File.Exists(typeLibPath))
            {
                try
                {
                    ITypeLib typeLibrary = (ITypeLib)NativeMethods.LoadTypeLibEx(typeLibPath, (int)NativeMethods.REGKIND.REGKIND_NONE);

                    // Get the library attributes so we can unregister it
                    IntPtr pTlibAttr = IntPtr.Zero;

                    try
                    {
                        typeLibrary.GetLibAttr(out pTlibAttr);
                        if (pTlibAttr != IntPtr.Zero)
                        {
                            // Unregister the type library
                            System.Runtime.InteropServices.ComTypes.TYPELIBATTR tlibattr = (System.Runtime.InteropServices.ComTypes.TYPELIBATTR)Marshal.PtrToStructure(pTlibAttr, typeof(System.Runtime.InteropServices.ComTypes.TYPELIBATTR));
                            NativeMethods.UnregisterTypeLib(ref tlibattr.guid, tlibattr.wMajorVerNum, tlibattr.wMinorVerNum, tlibattr.lcid, tlibattr.syskind);
                        }
                    }
                    finally
                    {
                        typeLibrary.ReleaseTLibAttr(pTlibAttr);
                        Marshal.ReleaseComObject(typeLibrary);
                    }
                }
                catch (COMException ex)
                {
                    // if the typelib to be unregistered is not registered, then we don't have anything left to do
                    if (ex.ErrorCode == NativeMethods.TYPE_E_REGISTRYACCESS)
                    {
                        Log.LogWarningWithCodeFromResources("UnregisterAssembly.UnregisterTlbFileNotRegistered", typeLibPath);
                    }
                    // if the typelib can't be loaded (say because it's not a valid typelib file) we should report an error
                    else if (ex.ErrorCode == NativeMethods.TYPE_E_CANTLOADLIBRARY)
                    {
                        Log.LogErrorWithCodeFromResources("UnregisterAssembly.UnregisterTlbCantLoadFile", typeLibPath);
                        return false;
                    }

                    // rethrow other exceptions
                    else
                    {
#if _DEBUG
                        Debug.Assert(false, "Unexpected exception in UnregisterAssembly.DoExecute. " + 
                            "Please log a MSBuild bug specifying the steps to reproduce the problem.");
#endif
                        throw;
                    }
                }
            }
            else
            {
                Log.LogMessageFromResources(MessageImportance.Low, "UnregisterAssembly.UnregisterTlbFileDoesNotExist", typeLibPath);
            }

            return true;
        }

        #endregion

        #region Data
        private static readonly Mutex s_unregisteringLock = new Mutex(false, unregisteringLockName);
        private const string unregisteringLockName = "MSBUILD_V_3_5_UNREGISTER_LOCK";
        #endregion
    }
}
#endif
