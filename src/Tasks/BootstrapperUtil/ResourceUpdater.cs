// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
#if FEATURE_WINDOWSINTEROP
using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Build.Framework;
using Windows.Win32;
using Windows.Win32.Foundation;
#endif

namespace Microsoft.Build.Tasks.Deployment.Bootstrapper
{
    internal class ResourceUpdater
    {
#if FEATURE_WINDOWSINTEROP
        private const int ERROR_SHARING_VIOLATION = -2147024864;
#endif
        private readonly List<StringResource> _stringResources = new List<StringResource>();
        private readonly List<FileResource> _fileResources = new List<FileResource>();

        public void AddStringResource(int type, string name, string data)
        {
            _stringResources.Add(new StringResource(type, name, data));
        }

        public void AddFileResource(string filename, string key)
        {
            _fileResources.Add(new FileResource(filename, key));
        }

        public bool UpdateResources(string filename, BuildResults results)
        {
#if FEATURE_WINDOWSINTEROP
#pragma warning disable CA1416 // Win32 API guarded by FEATURE_WINDOWSINTEROP; bootstrapper resource updates are Windows-only.
            bool returnValue = true;
            int beginUpdateRetries = 20;    // Number of retries
            const int beginUpdateRetryInterval = 100; // In milliseconds
            bool endUpdate = false; // Only call EndUpdateResource() if this is true

            // Directory.GetCurrentDirectory() has previously been set to the project location
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), filename);

            if (_stringResources.Count == 0 && _fileResources.Count == 0)
            {
                return true;
            }
            HANDLE hUpdate = HANDLE.Null;

            try
            {
                hUpdate = PInvoke.BeginUpdateResource(filePath, false);
                while (hUpdate == HANDLE.Null && Marshal.GetHRForLastWin32Error() == ERROR_SHARING_VIOLATION && beginUpdateRetries > 0) // If it equals 0x80070020 (ERROR_SHARING_VIOLATION), sleep & retry
                {
                    // This warning can be useful for debugging, but shouldn't be displayed to an actual user
                    // results.AddMessage(BuildMessage.CreateMessage(BuildMessageSeverity.Warning, "GenerateBootstrapper.General", String.Format("Unable to begin updating resource for {0} with error {1:X}, trying again after short sleep", filename, Marshal.GetHRForLastWin32Error())));
                    hUpdate = PInvoke.BeginUpdateResource(filePath, false);
                    beginUpdateRetries--;
                    Thread.Sleep(beginUpdateRetryInterval);
                }
                // If after all that we still failed, throw a build error
                if (hUpdate == HANDLE.Null)
                {
                    results.AddMessage(BuildMessage.CreateMessage(BuildMessageSeverity.Error, "GenerateBootstrapper.General",
                        $"Unable to begin updating resource for {filename} with error {Marshal.GetHRForLastWin32Error():X}"));
                    return false;
                }

                endUpdate = true;

                foreach (StringResource resource in _stringResources)
                {
                    byte[] data = StringToByteArray(resource.Data);

                    if (!UpdateResource(hUpdate, resource.Type, resource.Name, data))
                    {
                        results.AddMessage(BuildMessage.CreateMessage(BuildMessageSeverity.Error, "GenerateBootstrapper.General",
                            $"Unable to update resource for {filename} with error {Marshal.GetHRForLastWin32Error():X}"));
                        return false;
                    }
                }

                if (_fileResources.Count > 0)
                {
                    int index = 0;
                    byte[] countArray = StringToByteArray(_fileResources.Count.ToString("G", CultureInfo.InvariantCulture));
                    if (!UpdateResource(hUpdate, 42, "COUNT", countArray))
                    {
                        results.AddMessage(BuildMessage.CreateMessage(BuildMessageSeverity.Error, "GenerateBootstrapper.General",
                            $"Unable to update count resource for {filename} with error {Marshal.GetHRForLastWin32Error():X}"));
                        return false;
                    }

                    foreach (FileResource resource in _fileResources)
                    {
                        // Read in the file data
                        int fileLength;
                        byte[] fileContent;
                        using (FileStream fs = File.OpenRead(resource.Filename))
                        {
                            fileLength = (int)fs.Length;
                            fileContent = new byte[fileLength];
                            fs.ReadFromStream(fileContent, 0, fileLength);
                        }

                        // Update the resources to include this file's data
                        string dataName = string.Format(CultureInfo.InvariantCulture, "FILEDATA{0}", index);

                        if (!UpdateResource(hUpdate, 42, dataName, fileContent.AsSpan(0, fileLength)))
                        {
                            results.AddMessage(BuildMessage.CreateMessage(BuildMessageSeverity.Error, "GenerateBootstrapper.General",
                                $"Unable to update data resource for {filename} with error {Marshal.GetHRForLastWin32Error():X}"));
                            return false;
                        }

                        // Add this file's key to the resources
                        string keyName = string.Format(CultureInfo.InvariantCulture, "FILEKEY{0}", index);
                        byte[] data = StringToByteArray(resource.Key);
                        if (!UpdateResource(hUpdate, 42, keyName, data))
                        {
                            results.AddMessage(BuildMessage.CreateMessage(BuildMessageSeverity.Error, "GenerateBootstrapper.General",
                                $"Unable to update key resource for {filename} with error {Marshal.GetHRForLastWin32Error():X}"));
                            return false;
                        }

                        index++;
                    }
                }
            }
            finally
            {
                if (endUpdate && !PInvoke.EndUpdateResource(hUpdate, false))
                {
                    results.AddMessage(BuildMessage.CreateMessage(BuildMessageSeverity.Error, "GenerateBootstrapper.General",
                        $"Unable to finish updating resource for {filename} with error {Marshal.GetHRForLastWin32Error():X}"));
                    returnValue = false;
                }
            }

            return returnValue;
#pragma warning restore CA1416
#else
            results.AddMessage(BuildMessage.CreateMessage(
                BuildMessageSeverity.Error,
                "GenerateBootstrapper.General",
                $"Unable to update resources for {filename}: bootstrapper resource updates require Windows interop support, which is not available in this build."));
            return false;
#endif
        }

#if FEATURE_WINDOWSINTEROP
        /// <summary>
        /// Calls UpdateResource with an integer resource type (MAKEINTRESOURCE) and a string name.
        /// </summary>
        [System.Runtime.Versioning.SupportedOSPlatform("windows5.0")]
        private static unsafe bool UpdateResource(HANDLE hUpdate, int lpType, string lpName, ReadOnlySpan<byte> data)
        {
            // MAKEINTRESOURCE: Windows treats pointer values below 0x10000 as integer resource IDs.
            System.Diagnostics.Debug.Assert(lpType is >= 0 and < 0x10000, "MAKEINTRESOURCE values must be below 64K.");

            fixed (char* pName = lpName)
            {
                fixed (byte* pData = data)
                {
                    return PInvoke.UpdateResource(
                        hUpdate,
                        lpType: (PCWSTR)(char*)lpType,
                        lpName: pName,
                        wLanguage: 0,
                        lpData: pData,
                        cb: (uint)data.Length);
                }
            }
        }

        private static byte[] StringToByteArray(string str)
        {
            byte[] strBytes = System.Text.Encoding.Unicode.GetBytes(str);
            byte[] data = new byte[strBytes.Length + 2];
            strBytes.CopyTo(data, 0);
            data[data.Length - 2] = 0;
            data[data.Length - 1] = 0;
            return data;
        }
#endif

        private class StringResource
        {
            public readonly int Type;
            public readonly string Name;
            public readonly string Data;

            public StringResource(int type, string name, string data)
            {
                Type = type;
                Name = name;
                Data = data;
            }
        }

        private class FileResource
        {
            public readonly string Filename;
            public readonly string Key;

            public FileResource(string filename, string key)
            {
                Filename = filename;
                Key = key;
            }
        }
    }
}
