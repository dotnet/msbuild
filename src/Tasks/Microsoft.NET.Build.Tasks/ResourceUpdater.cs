// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// Provides methods for modifying the embedded native resources
    /// in a PE image.
    /// </summary>
    public class ResourceUpdater
    {
        //
        // Native methods for updating resources
        //

        [DllImport("kernel32.dll", SetLastError=true)]
        private static extern IntPtr BeginUpdateResource(string pFileName,
                                                         [MarshalAs(UnmanagedType.Bool)]bool bDeleteExistingResources);

        // Update a resource with data from an IntPtr
        [DllImport("kernel32.dll", SetLastError=true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UpdateResource(IntPtr hUpdate,
                                                  IntPtr lpType,
                                                  IntPtr lpName,
                                                  ushort wLanguage,
                                                  IntPtr lpData,
                                                  uint cbData);

        // Update a resource with data from a managed byte[]
        [DllImport("kernel32.dll", SetLastError=true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UpdateResource(IntPtr hUpdate,
                                                  IntPtr lpType,
                                                  IntPtr lpName,
                                                  ushort wLanguage,
                                                  [MarshalAs(UnmanagedType.LPArray, SizeParamIndex=5)] byte[] lpData,
                                                  uint cbData);

        [DllImport("kernel32.dll", SetLastError=true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EndUpdateResource(IntPtr hUpdate,
                                                     bool fDiscard);

        //
        // Native methods used to read resources from a PE file
        //

        // Loading and freeing PE files

        // TODO: use safe handle implementation?
        private enum LoadLibraryFlags : uint
        {
            None = 0,
            LOAD_LIBRARY_AS_DATAFILE_EXCLUSIVE = 0x00000040,
            LOAD_LIBRARY_AS_IMAGE_RESOURCE = 0x00000020
        }

        [DllImport("kernel32.dll", SetLastError=true)]
        private static extern IntPtr LoadLibraryEx(string lpFileName,
                                                   IntPtr hReservedNull,
                                                   LoadLibraryFlags dwFlags);

        [DllImport("kernel32.dll", SetLastError=true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FreeLibrary(IntPtr hModule);

        // Enumerating resources

        private delegate bool EnumResTypeProc(IntPtr hModule,
                                                     ushort lpType,
                                                     IntPtr lParam);

        private delegate bool EnumResNameProc(IntPtr hModule,
                                                     ushort lpType,
                                                     ushort lpName,
                                                     IntPtr lParam);

        private delegate bool EnumResLangProc(IntPtr hModule,
                                                     ushort lpType,
                                                     ushort lpName,
                                                     ushort wLang,
                                                     IntPtr lParam);

        [DllImport("kernel32.dll",SetLastError=true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumResourceTypes(IntPtr hModule,
                                                     EnumResTypeProc lpEnumFunc,
                                                     IntPtr lParam);

        [DllImport("kernel32.dll", SetLastError=true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumResourceNames(IntPtr hModule,
                                                     ushort lpType,
                                                     EnumResNameProc lpEnumFunc,
                                                     IntPtr lParam);

        [DllImport("kernel32.dll", SetLastError=true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumResourceLanguages(IntPtr hModule,
                                                         ushort lpType,
                                                         ushort lpName,
                                                         EnumResLangProc lpEnumFunc,
                                                         IntPtr lParam);

        // Querying and loading resources

        [DllImport("kernel32.dll", SetLastError=true)]
        private static extern IntPtr FindResourceEx(IntPtr hModule,
                                                    IntPtr lpType,
                                                    IntPtr lpName,
                                                    ushort wLanguage);

        [DllImport("kernel32.dll", SetLastError=true)]
        private static extern IntPtr LoadResource(IntPtr hModule,
                                                  IntPtr hResInfo);

        [DllImport("kernel32.dll")] // does not call SetLastError
        private static extern IntPtr LockResource(IntPtr hResData);

        [DllImport("kernel32.dll", SetLastError=true)]
        private static extern uint SizeofResource(IntPtr hModule,
                                                  IntPtr hResInfo);

        public const ushort LangID_LangNeutral_SublangNeutral = 0;

        /// <summary>
        /// Holds the native handle for the resource update.
        /// </summary>
        private IntPtr hUpdate = IntPtr.Zero;

        /// <summary>
        /// Create a resource updater for the given PE file. This will
        /// acquire a native resource update handle for the file,
        /// preparing it for updates. Resources can be added to this
        /// updater, which will queue them for update. The target PE
        /// file will not be modified until Update() is called.
        /// </summary>
        public ResourceUpdater(string peFile)
        {
            hUpdate = BeginUpdateResource(peFile, false);
            if (hUpdate == IntPtr.Zero)
            {
                ThrowExceptionForLastWin32Error();
            }
        }

        /// <summary>
        /// Add all resources from a source PE file. This will not
        /// modify the target until Update() is called.
        /// </summary>
        public ResourceUpdater AddResourcesFrom(string peFile)
        {
            // TODO: check that they're both valid PE files

            // TODO: produce something if we're not on windows

            // TODO: if it has no resources? maybe do a lazy beginupdateresource instead.

            // Using both flags lets the OS loader decide how to load
            // it most efficiently. Either mode will prevent other
            // processes from modifying the module while it is loaded.
            IntPtr hModule = LoadLibraryEx(peFile, IntPtr.Zero,
                                           LoadLibraryFlags.LOAD_LIBRARY_AS_DATAFILE_EXCLUSIVE | LoadLibraryFlags.LOAD_LIBRARY_AS_IMAGE_RESOURCE);
            if (hModule == IntPtr.Zero)
            {
                ThrowExceptionForLastWin32Error();
            }

            var enumTypesCallback = new EnumResTypeProc(EnumTypesCallback);
            if (!EnumResourceTypes(hModule, enumTypesCallback, IntPtr.Zero))
            {
                ThrowExceptionForLastWin32Error();
            }

            if (!FreeLibrary(hModule))
            {
                ThrowExceptionForLastWin32Error();
            }

            return this;
        }

        /// <summary>
        /// Add a language-neutral resource from a byte[] with a
        /// particular type and name. This will not modify the target
        /// until Update() is called.
        /// </summary>
        public ResourceUpdater AddResource(byte[] data, IntPtr lpType, IntPtr lpName)
        {
            //if (!UpdateResource(hUpdate, lpType, lpName, LangID_LangNeutral_SublangNeutral, data, data.Length))
            //{
                //ThrowExceptionForLastWin32Error();
            //}

            return this;
        }

        /// <summary>
        /// Write the pending resource updates to the target PE file.
        /// </summary>
        public void Update()
        {
            if (!EndUpdateResource(hUpdate, false))
            {
                ThrowExceptionForLastWin32Error();
            }
        }


        private bool EnumTypesCallback(IntPtr hModule, ushort lpType, IntPtr lParam)
        {
            // Console.WriteLine("resource type " + lpType);
            // TODO: what if the lpType is a string identifier?
            var enumNamesCallback = new EnumResNameProc(EnumNamesCallback);
            if (!EnumResourceNames(hModule, lpType, enumNamesCallback, lParam))
            {
                ThrowExceptionForLastWin32Error();
            }

            return true;
        }

        private bool EnumNamesCallback(IntPtr hModule, ushort lpType, ushort lpName, IntPtr lParam)
        {
            // Console.WriteLine("resource name " + lpName);
            // TODO: what if name is a string rather than an int?
            var enumLanguagesCallback = new EnumResLangProc(EnumLanguagesCallback);
            if (!EnumResourceLanguages(hModule, lpType, lpName, enumLanguagesCallback, lParam))
            {
                ThrowExceptionForLastWin32Error();
            }

            return true;
        }

        private bool EnumLanguagesCallback(IntPtr hModule, ushort lpType, ushort lpName, ushort wLang, IntPtr lParam)
        {
            IntPtr hResource = FindResourceEx(hModule, (IntPtr)lpType, (IntPtr)lpName, wLang);
            if (hResource == IntPtr.Zero)
            {
                ThrowExceptionForLastWin32Error();
            }

            IntPtr hResourceLoaded = LoadResource(hModule, hResource);
            if (hResourceLoaded == IntPtr.Zero)
            {
                ThrowExceptionForLastWin32Error();
            }

            IntPtr lpResourceData = LockResource(hResourceLoaded);
            if (lpResourceData == IntPtr.Zero)
            {
                // TODO: better exception
                throw new Exception("failed to lock resource");
             }

            if (!UpdateResource(hUpdate, (IntPtr)lpType, (IntPtr)lpName, wLang, lpResourceData, SizeofResource(hModule, hResource)))
            {
                ThrowExceptionForLastWin32Error();
            }
            // TODO: cast ushort to intptr?

            return true;
        }

        private static void ThrowExceptionForLastWin32Error()
        {
            Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
        }
    }
}
