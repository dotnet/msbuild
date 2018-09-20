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
    /// in a PE image. It currently only works on Windows, because it
    /// requires various kernel32 APIs.
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

        private enum LoadLibraryFlags : uint
        {
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
                                              IntPtr lpType,
                                              IntPtr lParam);

        private delegate bool EnumResNameProc(IntPtr hModule,
                                              IntPtr lpType,
                                              IntPtr lpName,
                                              IntPtr lParam);

        private delegate bool EnumResLangProc(IntPtr hModule,
                                              IntPtr lpType,
                                              IntPtr lpName,
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
                                                     IntPtr lpType,
                                                     EnumResNameProc lpEnumFunc,
                                                     IntPtr lParam);

        [DllImport("kernel32.dll", SetLastError=true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumResourceLanguages(IntPtr hModule,
                                                         IntPtr lpType,
                                                         IntPtr lpName,
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

        /// <summary>
        /// Holds the native handle for the resource update.
        /// </summary>
        private IntPtr hUpdate = IntPtr.Zero;

        /// <summary>
        /// Create a resource updater for the given PE file. This will
        /// acquire a native resource update handle for the file,
        /// preparing it for updates. Resources can be added to this
        /// updater, which will queue them for update. The target PE
        /// file will not be modified until Update() is called, after
        /// which the ResourceUpdater can not be used for further
        /// updates.
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
        /// Add all resources from a source PE file. It is assumed
        /// that the input is a valid PE file. If it is not, an
        /// exception will be thrown. This will not modify the target
        /// until Update() is called.
        /// Throws an InvalidOperationException if Update() was already called.
        /// </summary>
        public ResourceUpdater AddResourcesFromPEImage(string peFile)
        {
            if (hUpdate == IntPtr.Zero)
            {
                ThrowExceptionForInvalidUpdate();
            }

            // Using both flags lets the OS loader decide how to load
            // it most efficiently. Either mode will prevent other
            // processes from modifying the module while it is loaded.
            IntPtr hModule = LoadLibraryEx(peFile, IntPtr.Zero,
                                           LoadLibraryFlags.LOAD_LIBRARY_AS_DATAFILE_EXCLUSIVE | LoadLibraryFlags.LOAD_LIBRARY_AS_IMAGE_RESOURCE);
            if (hModule == IntPtr.Zero)
            {
                ThrowExceptionForLastWin32Error();
            }

            try
            {
                var enumTypesCallback = new EnumResTypeProc(EnumTypesCallback);
                if (!EnumResourceTypes(hModule, enumTypesCallback, IntPtr.Zero))
                {
                    ThrowExceptionForLastWin32Error();
                }
            }
            finally
            {
                if (!FreeLibrary(hModule))
                {
                    ThrowExceptionForLastWin32Error();
                }
            }

            return this;
        }

        private const ushort LangID_LangNeutral_SublangNeutral = 0;

        private static bool IsIntResource(IntPtr lpType)
        {
            return ((uint)lpType >> 16) == 0;
        }

        /// <summary>
        /// Add a language-neutral integer resource from a byte[] with
        /// a particular type and name. This will not modify the
        /// target until Update() is called.
        /// Throws an InvalidOperationException if Update() was already called.
        /// </summary>
        public ResourceUpdater AddResource(byte[] data, IntPtr lpType, IntPtr lpName)
        {
            if (hUpdate == IntPtr.Zero)
            {
                ThrowExceptionForInvalidUpdate();
            }

            if (!IsIntResource(lpType) || !IsIntResource(lpName))
            {
                throw new ArgumentException("AddResource can only be used with integer resource types.");
            }

            if (!UpdateResource(hUpdate, lpType, lpName, LangID_LangNeutral_SublangNeutral, data, (uint)data.Length))
            {
                ThrowExceptionForLastWin32Error();
            }

            return this;
        }

        /// <summary>
        /// Write the pending resource updates to the target PE
        /// file. After this, the ResourceUpdater no longer maintains
        /// an update handle, and can not be used for further updates.
        /// Throws an InvalidOperationException if Update() was already called.
        /// </summary>
        public void Update()
        {
            if (hUpdate == IntPtr.Zero)
            {
                ThrowExceptionForInvalidUpdate();
            }

            try
            {
                if (!EndUpdateResource(hUpdate, false))
                {
                    ThrowExceptionForLastWin32Error();
                }
            }
            finally
            {
                hUpdate = IntPtr.Zero;
            }
        }


        private bool EnumTypesCallback(IntPtr hModule, IntPtr lpType, IntPtr lParam)
        {
            var enumNamesCallback = new EnumResNameProc(EnumNamesCallback);
            if (!EnumResourceNames(hModule, lpType, enumNamesCallback, lParam))
            {
                ThrowExceptionForLastWin32Error();
            }

            return true;
        }

        private bool EnumNamesCallback(IntPtr hModule, IntPtr lpType, IntPtr lpName, IntPtr lParam)
        {
            var enumLanguagesCallback = new EnumResLangProc(EnumLanguagesCallback);
            if (!EnumResourceLanguages(hModule, lpType, lpName, enumLanguagesCallback, lParam))
            {
                ThrowExceptionForLastWin32Error();
            }

            return true;
        }

        private bool EnumLanguagesCallback(IntPtr hModule, IntPtr lpType, IntPtr lpName, ushort wLang, IntPtr lParam)
        {
            IntPtr hResource = FindResourceEx(hModule, lpType, lpName, wLang);
            if (hResource == IntPtr.Zero)
            {
                ThrowExceptionForLastWin32Error();
            }

            // hResourceLoadedq is just a handle to the resource,
            // which can be used to get the resource data
            IntPtr hResourceLoaded = LoadResource(hModule, hResource);
            if (hResourceLoaded == IntPtr.Zero)
            {
                ThrowExceptionForLastWin32Error();
            }

            // This doesn't actually lock memory. It just retrieves a
            // pointer to the resource data. The pointer is valid
            // until the module is unloaded.
            IntPtr lpResourceData = LockResource(hResourceLoaded);
            if (lpResourceData == IntPtr.Zero)
            {
                throw new Exception("Failed to lock resource.");
             }

            if (!UpdateResource(hUpdate, lpType, lpName, wLang, lpResourceData, SizeofResource(hModule, hResource)))
            {
                ThrowExceptionForLastWin32Error();
            }

            return true;
        }

        private static void ThrowExceptionForInvalidUpdate()
        {
            throw new InvalidOperationException("Update handle is invalid. This instance may not be used for further updates.");
        }

        private static void ThrowExceptionForLastWin32Error()
        {
            Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
        }
    }
}
