// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if FEATURE_APPDOMAIN

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Registers a managed assembly for COM interop (equivalent of regasm.exe functionality, but this code doesn't actually call the exe).
    /// </summary>
    /// <comment>ITypeLibExporterNotifySink is necessary for the ITypeLibConverter.ConvertAssemblyToTypeLib call.</comment>
    public class RegisterAssembly : AppDomainIsolatedTaskExtension, ITypeLibExporterNotifySink
    {
        #region Properties

        [Required]
        public ITaskItem[] Assemblies
        {
            get
            {
                ErrorUtilities.VerifyThrowArgumentNull(_assemblies, nameof(Assemblies));
                return _assemblies;
            }
            set => _assemblies = value;
        }

        private ITaskItem[] _assemblies;

        [Output]
        public ITaskItem[] TypeLibFiles { get; set; }

        public bool CreateCodeBase { get; set; }

        /// <summary>
        /// The cache file for Register/UnregisterAssembly. Necessary for UnregisterAssembly to do the proper clean up.
        /// </summary>
        public ITaskItem AssemblyListFile { get; set; }

        #endregion

        #region ITask members

        /// <summary>
        /// Task entry point
        /// </summary>
        public override bool Execute()
        {
            // TypeLibFiles isn't [Required], but if it is specified, it must have the same length as Assemblies
            if ((TypeLibFiles != null) && (TypeLibFiles.Length != Assemblies.Length))
            {
                Log.LogErrorWithCodeFromResources("General.TwoVectorsMustHaveSameLength", Assemblies.Length, TypeLibFiles.Length, "Assemblies", "TypeLibFiles");
                return false;
            }

            if (TypeLibFiles == null)
            {
                TypeLibFiles = new ITaskItem[Assemblies.Length];
            }

            AssemblyRegistrationCache cacheFile = null;

            if ((AssemblyListFile != null) && (AssemblyListFile.ItemSpec.Length > 0))
            {
                cacheFile = (AssemblyRegistrationCache)StateFileBase.DeserializeCache(AssemblyListFile.ItemSpec, Log, typeof(AssemblyRegistrationCache)) ??
                            new AssemblyRegistrationCache();
            }

            bool taskReturnValue = true;

            try
            {
                for (int i = 0; i < Assemblies.Length; i++)
                {
                    try
                    {
                        string tlbPath;

                        // if the type lib path is not supplied, generate default one
                        if ((TypeLibFiles[i] != null) && (TypeLibFiles[i].ItemSpec.Length > 0))
                        {
                            tlbPath = TypeLibFiles[i].ItemSpec;
                        }
                        else
                        {
                            tlbPath = Path.ChangeExtension(Assemblies[i].ItemSpec, ".tlb");
                            TypeLibFiles[i] = new TaskItem(tlbPath);
                        }

                        // If one of assemblies failed to register, the whole task failed.
                        // We still process the rest of assemblies though.
                        if (!Register(Assemblies[i].ItemSpec, tlbPath))
                        {
                            taskReturnValue = false;
                        }
                        else
                        {
                            cacheFile?.AddEntry(Assemblies[i].ItemSpec, tlbPath);
                        }
                    }
                    catch (ArgumentException ex) // assembly path has invalid chars in it
                    {
                        Log.LogErrorWithCodeFromResources("General.InvalidAssemblyName", Assemblies[i], ex.Message);
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
                cacheFile?.SerializeCache(AssemblyListFile.ItemSpec, Log);
            }

            return taskReturnValue;
        }

        #endregion

        #region ITypeLibExporterNotifySink methods

        private bool _typeLibExportFailed;

        /// <summary>
        /// Callback method for reporting type library export events
        /// </summary>
        public void ReportEvent(ExporterEventKind kind, int code, string msg)
        {
            // if we get an error, log it and remember we should fail ExportTypeLib
            if (kind == ExporterEventKind.ERROR_REFTOINVALIDASSEMBLY)
            {
                Log.LogError(msg);
                _typeLibExportFailed = true;
            }
            // if it's just a warning, log it and proceed
            else if (kind == ExporterEventKind.NOTIF_CONVERTWARNING)
            {
                Log.LogWarning(msg);
            }
            // it's just a status message (type xxx converted etc.), log it at lowest possible priority
            else if (kind == ExporterEventKind.NOTIF_TYPECONVERTED)
            {
                Log.LogMessage(MessageImportance.Low, msg);
            }
            else
            {
                Debug.Assert(false, "Unknown ImporterEventKind value");
                Log.LogMessage(MessageImportance.Low, msg);
            }
        }

        /// <summary>
        ///  Callback method for finding type libraries for given assemblies. If we are here, it means
        ///  the type library we're looking for is not in the current directory and it's not registered.
        ///  Currently we assume that all dependent type libs are already registered.
        /// </summary>
        /// <comment>
        ///  In theory, we could automatically register dependent assemblies for COM interop and return
        ///  a newly created typelib here. However, one danger of such approach is the following scenario:
        ///  The user creates several projects registered for COM interop, all of them referencing assembly A.
        ///  The first project that happens to be built will register assembly A for COM interop, creating
        ///  a type library in its output directory and registering it. The other projects will then refer to that
        ///  type library, since it's already registered. If then for some reason the first project is deleted
        ///  from disk, the typelib for assembly A goes away too, and all the other projects, built five years ago,
        ///  suddenly stop working.
        /// </comment>
        public object ResolveRef(Assembly assemblyToResolve)
        {
            ErrorUtilities.VerifyThrowArgumentNull(assemblyToResolve, nameof(assemblyToResolve));

            Log.LogErrorWithCodeFromResources("RegisterAssembly.AssemblyNotRegisteredForComInterop", assemblyToResolve.GetName().FullName);
            _typeLibExportFailed = true;
            return null;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Helper registration method
        /// </summary>
        private bool Register(string assemblyPath, string typeLibPath)
        {
            ErrorUtilities.VerifyThrowArgumentNull(typeLibPath, nameof(typeLibPath));

            Log.LogMessageFromResources(MessageImportance.Low, "RegisterAssembly.RegisteringAssembly", assemblyPath);

            if (!File.Exists(assemblyPath))
            {
                Log.LogErrorWithCodeFromResources("RegisterAssembly.RegisterAsmFileDoesNotExist", assemblyPath);
                return false;
            }

            ITypeLib typeLib = null;

            try
            {
                // Load the specified assembly. 
                Assembly asm = Assembly.UnsafeLoadFrom(assemblyPath);

                var comRegistrar = new RegistrationServices();

                // Register the assembly
                if (!comRegistrar.RegisterAssembly(asm, CreateCodeBase ? AssemblyRegistrationFlags.SetCodeBase : AssemblyRegistrationFlags.None))
                {
                    // If the assembly doesn't contain any types that could be registered for COM interop, 
                    // warn the user about it.  
                    Log.LogWarningWithCodeFromResources("RegisterAssembly.NoValidTypes", assemblyPath);
                }

                // Even if there aren't any types that could be registered for COM interop,
                // regasm still creates and tries to register the type library, so we should too.
                Log.LogMessageFromResources(MessageImportance.Low, "RegisterAssembly.RegisteringTypeLib", typeLibPath);

                // only regenerate the type lib if necessary
                if ((!File.Exists(typeLibPath)) ||
                    (File.GetLastWriteTime(typeLibPath) < File.GetLastWriteTime(assemblyPath)))
                {
                    // Regenerate the type library
                    try
                    {
                        // if export failed the error message is already logged, so just exit
                        if (!ExportTypeLib(asm, typeLibPath))
                        {
                            return false;
                        }
                    }
                    catch (COMException ex)
                    {
                        Log.LogErrorWithCodeFromResources("RegisterAssembly.CantExportTypeLib", assemblyPath, ex.Message);
                        return false;
                    }
                }
                else
                {
                    Log.LogMessageFromResources(MessageImportance.Low, "RegisterAssembly.TypeLibUpToDate", typeLibPath);
                }

                // Also register the type library
                try
                {
                    typeLib = (ITypeLib)NativeMethods.LoadTypeLibEx(typeLibPath, (int)NativeMethods.REGKIND.REGKIND_NONE);

                    // if we got here, load must have succeeded
                    NativeMethods.RegisterTypeLib(typeLib, typeLibPath, null);
                }
                catch (COMException ex)
                {
                    Log.LogErrorWithCodeFromResources("RegisterAssembly.CantRegisterTypeLib", typeLibPath, ex.Message);
                    return false;
                }
            }
            catch (ArgumentNullException e)
            {
                Log.LogErrorWithCodeFromResources("RegisterAssembly.CantRegisterAssembly", assemblyPath, e.Message);
                return false;
            }
            catch (InvalidOperationException e)
            {
                Log.LogErrorWithCodeFromResources("RegisterAssembly.CantRegisterAssembly", assemblyPath, e.Message);
                return false;
            }
            catch (TargetInvocationException e)
            {
                Log.LogErrorWithCodeFromResources("RegisterAssembly.CantRegisterAssembly", assemblyPath, e.Message);
                return false;
            }
            catch (IOException e)
            {
                Log.LogErrorWithCodeFromResources("RegisterAssembly.CantRegisterAssembly", assemblyPath, e.Message);
                return false;
            }
            catch (TypeLoadException e)
            {
                Log.LogErrorWithCodeFromResources("RegisterAssembly.CantRegisterAssembly", assemblyPath, e.Message);
                return false;
            }
            catch (UnauthorizedAccessException e)
            {
                Log.LogErrorWithCodeFromResources("RegisterAssembly.UnauthorizedAccess", assemblyPath, e.Message);
                return false;
            }
            catch (BadImageFormatException)
            {
                Log.LogErrorWithCodeFromResources("General.InvalidAssembly", assemblyPath);
                return false;
            }
            catch (SecurityException e)
            {
                Log.LogErrorWithCodeFromResources("RegisterAssembly.CantRegisterAssembly", assemblyPath, e.Message);
                return false;
            }
            finally
            {
                if (typeLib != null)
                {
                    Marshal.ReleaseComObject(typeLib);
                }
            }

            return true;
        }

        /// <summary>
        ///  Helper method - exports a type library for an assembly. Returns true if succeeded.
        /// </summary>
        private bool ExportTypeLib(Assembly asm, string typeLibFileName)
        {
            _typeLibExportFailed = false;
            ITypeLib convertedTypeLib = null;

            try
            {
                // Create a converter and run the conversion
                ITypeLibConverter tlbConverter = new TypeLibConverter();
                convertedTypeLib = (ITypeLib)tlbConverter.ConvertAssemblyToTypeLib(asm, typeLibFileName, 0, this);

                if (convertedTypeLib == null || _typeLibExportFailed)
                {
                    return false;
                }

                // Persist the type library
                UCOMICreateITypeLib createTypeLib = (UCOMICreateITypeLib)convertedTypeLib;

                createTypeLib.SaveAllChanges();
            }
            finally
            {
                if (convertedTypeLib != null)
                {
                    Marshal.ReleaseComObject(convertedTypeLib);
                }
            }

            return !_typeLibExportFailed;
        }

        #endregion
    }
}
#endif
