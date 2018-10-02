// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Resources;
using System.Runtime.InteropServices;

namespace Microsoft.Build.Tasks.Deployment.ManifestUtilities
{
    internal class ComImporter
    {
        private readonly OutputMessageCollection _outputMessages;
        private readonly string _outputDisplayName;
        private readonly ResourceManager _resources = new ResourceManager("Microsoft.Build.Tasks.Core.Strings.ManifestUtilities", System.Reflection.Assembly.GetExecutingAssembly());

        // These must be defined in sorted order!
        private static readonly string[] s_knownImplementedCategories =
        {
            "{02496840-3AC4-11cf-87B9-00AA006C8166}", //CATID_VBFormat
            "{02496841-3AC4-11cf-87B9-00AA006C8166}", //CATID_VBGetControl
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

        public ComImporter(string path, OutputMessageCollection outputMessages, string outputDisplayName)
        {
            _outputMessages = outputMessages;
            _outputDisplayName = outputDisplayName;

            if (NativeMethods.SfcIsFileProtected(IntPtr.Zero, path) != 0)
                outputMessages.AddWarningMessage("GenerateManifest.ComImport", outputDisplayName, _resources.GetString("ComImporter.ProtectedFile"));

            object obj = null;
            try { NativeMethods.LoadTypeLibEx(path, NativeMethods.RegKind.RegKind_None, out obj); }
            catch (COMException) { }

#pragma warning disable 618
            UCOMITypeLib tlib = (UCOMITypeLib)obj;
            if (tlib != null)
            {
                IntPtr typeLibAttrPtr = IntPtr.Zero;
                tlib.GetLibAttr(out typeLibAttrPtr);
                var typeLibAttr = (TYPELIBATTR)Marshal.PtrToStructure(typeLibAttrPtr, typeof(TYPELIBATTR));
                tlib.ReleaseTLibAttr(typeLibAttrPtr);
                Guid tlbid = typeLibAttr.guid;

                tlib.GetDocumentation(-1, out _, out string docString, out _, out string helpFile);
                string helpdir = Util.FilterNonprintableChars(helpFile); //Path.GetDirectoryName(helpFile);

                TypeLib = new TypeLib(tlbid, new Version(typeLibAttr.wMajorVerNum, typeLibAttr.wMinorVerNum), helpdir, typeLibAttr.lcid, Convert.ToInt32(typeLibAttr.wLibFlags, CultureInfo.InvariantCulture));

                var comClassList = new List<ComClass>();
                int count = tlib.GetTypeInfoCount();
                for (int i = 0; i < count; ++i)
                {
                    tlib.GetTypeInfoType(i, out TYPEKIND tkind);
                    if (tkind == TYPEKIND.TKIND_COCLASS)
                    {
                        tlib.GetTypeInfo(i, out UCOMITypeInfo tinfo);

                        IntPtr tinfoAttrPtr = IntPtr.Zero;
                        tinfo.GetTypeAttr(out tinfoAttrPtr);
                        TYPEATTR tinfoAttr = (TYPEATTR)Marshal.PtrToStructure(tinfoAttrPtr, typeof(TYPEATTR));
                        tinfo.ReleaseTypeAttr(tinfoAttrPtr);
                        Guid clsid = tinfoAttr.guid;

                        tlib.GetDocumentation(i, out _, out docString, out _, out helpFile);
                        string description = Util.FilterNonprintableChars(docString);

                        ClassInfo info = GetRegisteredClassInfo(clsid);
                        if (info == null)
                        {
                            continue;
                        }

                        comClassList.Add(new ComClass(tlbid, clsid, info.Progid, info.ThreadingModel, description));
                    }
                }
                if (comClassList.Count > 0)
                {
                    ComClasses = comClassList.ToArray();
                    Success = true;
                }
                else
                {
                    outputMessages.AddErrorMessage("GenerateManifest.ComImport", outputDisplayName, _resources.GetString("ComImporter.NoRegisteredClasses"));
                    Success = false;
                }
            }
            else
            {
                outputMessages.AddErrorMessage("GenerateManifest.ComImport", outputDisplayName, _resources.GetString("ComImporter.TypeLibraryLoadFailure"));
                Success = false;
            }
#pragma warning restore 618
        }

        private void CheckForUnknownSubKeys(RegistryKey key)
        {
            CheckForUnknownSubKeys(key, Array.Empty<string>());
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
            CheckForUnknownValues(key, Array.Empty<string>());
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
            RegistryKey userKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\CLASSES\\CLSID");
            if (GetRegisteredClassInfo(userKey, clsid, ref info))
            {
                return info;
            }
            RegistryKey machineKey = Registry.ClassesRoot.OpenSubKey("CLSID");
            if (GetRegisteredClassInfo(machineKey, clsid, ref info))
            {
                return info;
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
                    CheckForUnknownValues(subKey, new string[] { "ThreadingModel" });
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

        public bool Success { get; } = true;

        public ComClass[] ComClasses { get; }
        public TypeLib TypeLib { get; }

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
    }
}
