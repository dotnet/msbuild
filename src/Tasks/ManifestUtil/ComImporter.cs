// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Versioning;
#if FEATURE_WINDOWSINTEROP
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Resources;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.System.Ole;
#endif

#nullable disable

namespace Microsoft.Build.Tasks.Deployment.ManifestUtilities
{
    [SupportedOSPlatform("windows")]
    internal class ComImporter
    {
#if FEATURE_WINDOWSINTEROP
        private readonly OutputMessageCollection _outputMessages;
        private readonly string _outputDisplayName;
        private readonly ResourceManager _resources = new ResourceManager("Microsoft.Build.Tasks.Core.Strings.ManifestUtilities", System.Reflection.Assembly.GetExecutingAssembly());

        // These must be defined in sorted order!
        private static readonly string[] s_knownImplementedCategories =
        {
            "{02496840-3AC4-11cf-87B9-00AA006C8166}", // CATID_VBFormat
            "{02496841-3AC4-11cf-87B9-00AA006C8166}", // CATID_VBGetControl
            "{40FC6ED5-2438-11CF-A3DB-080036F12502}",
        };
        private static readonly string[] s_knownSubKeys =
        {
            "Control",
            "Programmable",
            "ToolboxBitmap32",
            "TypeLib",
            "Version",
            "VersionIndependentProgID",
        };
#endif

        public unsafe ComImporter(string path, OutputMessageCollection outputMessages, string outputDisplayName)
        {
#if FEATURE_WINDOWSINTEROP
            _outputMessages = outputMessages;
            _outputDisplayName = outputDisplayName;

            // ComImporter relies on Windows-only type-library COM APIs. This guard short-circuits non-Windows
            // runs and raises the platform-compatibility analyzer floor to windows6.1 for the CsWin32 calls
            // below. The sole caller, FileReference.ImportComComponent, is [SupportedOSPlatform("windows")].
            if (!NativeMethodsShared.IsWindows)
            {
                return;
            }

            if (PInvoke.SfcIsFileProtected(default, path))
            {
                outputMessages.AddWarningMessage("GenerateManifest.ComImport", outputDisplayName, _resources.GetString("ComImporter.ProtectedFile"));
            }

            using ComScope<ITypeLib> typeLib = new(null);
            if (PInvoke.LoadTypeLibEx(path, REGKIND.REGKIND_NONE, typeLib).Failed)
            {
                outputMessages.AddErrorMessage("GenerateManifest.ComImport", outputDisplayName, _resources.GetString("ComImporter.TypeLibraryLoadFailure"));
                Success = false;
                return;
            }

            TLIBATTR* pLibAttr;
            typeLib.Pointer->GetLibAttr(&pLibAttr).ThrowOnFailure();
            Guid tlbid = pLibAttr->guid;
            try
            {
                // Only the help file is needed; GetDocumentation accepts null for any out-param the caller doesn't use.
                using BSTR helpFile = default;
                typeLib.Pointer->GetDocumentation(-1, null, null, null, &helpFile).ThrowOnFailure();
                TypeLib = new TypeLib(
                    tlbid,
                    new Version(pLibAttr->wMajorVerNum, pLibAttr->wMinorVerNum),
                    Util.FilterNonprintableChars(helpFile.ToString()),
                    (int)pLibAttr->lcid,
                    (int)pLibAttr->wLibFlags);
            }
            finally
            {
                typeLib.Pointer->ReleaseTLibAttr(pLibAttr);
            }

            var comClassList = new List<ComClass>();
            uint count = typeLib.Pointer->GetTypeInfoCount();
            for (uint i = 0; i < count; ++i)
            {
                TYPEKIND tkind;
                typeLib.Pointer->GetTypeInfoType(i, &tkind).ThrowOnFailure();
                if (tkind != TYPEKIND.TKIND_COCLASS)
                {
                    continue;
                }

                using ComScope<ITypeInfo> typeInfo = new(null);
                typeLib.Pointer->GetTypeInfo(i, typeInfo).ThrowOnFailure();

                TYPEATTR* pTypeAttr;
                typeInfo.Pointer->GetTypeAttr(&pTypeAttr).ThrowOnFailure();
                Guid clsid = pTypeAttr->guid;
                typeInfo.Pointer->ReleaseTypeAttr(pTypeAttr);

                ClassInfo info = GetRegisteredClassInfo(clsid);
                if (info == null)
                {
                    continue;
                }

                // Only the doc string is needed; GetDocumentation accepts null for any out-param the caller doesn't use.
                using BSTR docString = default;
                typeLib.Pointer->GetDocumentation((int)i, null, &docString, null, null).ThrowOnFailure();
                comClassList.Add(new ComClass(tlbid, clsid, info.Progid, info.ThreadingModel, Util.FilterNonprintableChars(docString.ToString())));
            }

            if (comClassList.Count == 0)
            {
                outputMessages.AddErrorMessage("GenerateManifest.ComImport", outputDisplayName, _resources.GetString("ComImporter.NoRegisteredClasses"));
                Success = false;
                return;
            }

            ComClasses = comClassList.ToArray();
            Success = true;
#endif
        }

#if FEATURE_WINDOWSINTEROP
        private void CheckForUnknownSubKeys(RegistryKey key)
        {
            CheckForUnknownSubKeys(key, []);
        }

        private void CheckForUnknownSubKeys(RegistryKey key, string[] knownNames)
        {
            if (key.SubKeyCount > 0)
            {
                foreach (string name in key.GetSubKeyNames())
                {
                    if (Array.BinarySearch(knownNames, name, StringComparer.OrdinalIgnoreCase) < 0)
                    {
                        _outputMessages.AddWarningMessage("GenerateManifest.ComImport", _outputDisplayName, String.Format(CultureInfo.CurrentCulture, _resources.GetString("ComImporter.SubKeyNotImported"), key.Name + "\\" + name));
                    }
                }
            }
        }

        private void CheckForUnknownValues(RegistryKey key)
        {
            CheckForUnknownValues(key, []);
        }

        private void CheckForUnknownValues(RegistryKey key, string[] knownNames)
        {
            if (key.ValueCount > 0)
            {
                foreach (string name in key.GetValueNames())
                {
                    if (!String.IsNullOrEmpty(name) && Array.BinarySearch(
                            knownNames,
                            name,
                            StringComparer.OrdinalIgnoreCase) < 0)
                    {
                        _outputMessages.AddWarningMessage("GenerateManifest.ComImport", _outputDisplayName, String.Format(CultureInfo.CurrentCulture, _resources.GetString("ComImporter.ValueNotImported"), key.Name + "\\@" + name));
                    }
                }
            }
        }

        private ClassInfo GetRegisteredClassInfo(Guid clsid)
        {
            ClassInfo info = null;

            using (RegistryKey userKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\CLASSES\\CLSID"))
            {
                if (GetRegisteredClassInfo(userKey, clsid, ref info))
                {
                    return info;
                }
            }

            using (RegistryKey machineKey = Registry.ClassesRoot.OpenSubKey("CLSID"))
            {
                if (GetRegisteredClassInfo(machineKey, clsid, ref info))
                {
                    return info;
                }
            }

            // Check Wow6432Node of HKCR incase the COM reference is to a 32-bit binary.
            if (Environment.Is64BitProcess)
            {
                using (RegistryKey classesRootKey = RegistryKey.OpenBaseKey(RegistryHive.ClassesRoot, RegistryView.Registry32))
                {
                    using (RegistryKey clsidKey = classesRootKey.OpenSubKey("CLSID"))
                    {
                        if (GetRegisteredClassInfo(clsidKey, clsid, ref info))
                        {
                            return info;
                        }
                    }
                }
            }

            return null;
        }

        private bool GetRegisteredClassInfo(RegistryKey rootKey, Guid clsid, ref ClassInfo info)
        {
            if (rootKey == null)
            {
                return false;
            }

            string sclsid = clsid.ToString("B");
            RegistryKey classKey = rootKey.OpenSubKey(sclsid);
            if (classKey == null)
            {
                return false;
            }

            bool succeeded = true;
            string registeredPath = null;
            string threadingModel = null;
            string progid = null;

            string[] subKeyNames = classKey.GetSubKeyNames();
            foreach (string subKeyName in subKeyNames)
            {
                RegistryKey subKey = classKey.OpenSubKey(subKeyName);
                if (String.Equals(subKeyName, "InProcServer32", StringComparison.OrdinalIgnoreCase))
                {
                    registeredPath = (string)subKey.GetValue(null);
                    threadingModel = (string)subKey.GetValue("ThreadingModel");
                    CheckForUnknownSubKeys(subKey);
                    CheckForUnknownValues(subKey, ["ThreadingModel"]);
                }
                else if (String.Equals(subKeyName, "ProgID", StringComparison.OrdinalIgnoreCase))
                {
                    progid = (string)subKey.GetValue(null);
                    CheckForUnknownSubKeys(subKey);
                    CheckForUnknownValues(subKey);
                }
                else if (String.Equals(subKeyName, "LocalServer32", StringComparison.OrdinalIgnoreCase))
                {
                    _outputMessages.AddWarningMessage("GenerateManifest.ComImport", _outputDisplayName, String.Format(CultureInfo.CurrentCulture, _resources.GetString("ComImporter.LocalServerNotSupported"), classKey.Name + "\\LocalServer32"));
                }
                else if (String.Equals(subKeyName, "Implemented Categories", StringComparison.OrdinalIgnoreCase))
                {
                    CheckForUnknownSubKeys(subKey, s_knownImplementedCategories);
                    CheckForUnknownValues(subKey);
                }
                else
                {
                    if (Array.BinarySearch(s_knownSubKeys, subKeyName, StringComparer.OrdinalIgnoreCase) < 0)
                    {
                        _outputMessages.AddWarningMessage("GenerateManifest.ComImport", _outputDisplayName, String.Format(CultureInfo.CurrentCulture, _resources.GetString("ComImporter.SubKeyNotImported"), classKey.Name + "\\" + subKeyName));
                    }
                }
            }

            if (String.IsNullOrEmpty(registeredPath))
            {
                _outputMessages.AddErrorMessage("GenerateManifest.ComImport", _outputDisplayName, String.Format(CultureInfo.CurrentCulture, _resources.GetString("ComImporter.MissingValue"), classKey.Name + "\\InProcServer32", "(Default)"));
                succeeded = false;
            }

            info = new ClassInfo(progid, threadingModel);
            return succeeded;
        }
#endif

        public bool Success { get; } = true;

        public ComClass[] ComClasses { get; }
        public TypeLib TypeLib { get; }

#if FEATURE_WINDOWSINTEROP
        private class ClassInfo
        {
            internal readonly string Progid;
            internal readonly string ThreadingModel;
            internal ClassInfo(string progid, string threadingModel)
            {
                Progid = progid;
                ThreadingModel = threadingModel;
            }
        }
#endif
    }
}
