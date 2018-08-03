// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace Microsoft.Build.Tasks.Deployment.Bootstrapper
{
    internal class ResourceUpdater
    {
        private const int ERROR_SHARING_VIOLATION = -2147024864;
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
            IntPtr hUpdate = IntPtr.Zero;

            try
            {
                hUpdate = NativeMethods.BeginUpdateResourceW(filePath, false);
                while (IntPtr.Zero == hUpdate && Marshal.GetHRForLastWin32Error() == ERROR_SHARING_VIOLATION && beginUpdateRetries > 0) // If it equals 0x80070020 (ERROR_SHARING_VIOLATION), sleep & retry
                {
                    // This warning can be useful for debugging, but shouldn't be displayed to an actual user
                    // results.AddMessage(BuildMessage.CreateMessage(BuildMessageSeverity.Warning, "GenerateBootstrapper.General", String.Format("Unable to begin updating resource for {0} with error {1:X}, trying again after short sleep", filename, Marshal.GetHRForLastWin32Error())));
                    hUpdate = NativeMethods.BeginUpdateResourceW(filePath, false);
                    beginUpdateRetries--;
                    Thread.Sleep(beginUpdateRetryInterval);
                }
                // If after all that we still failed, throw a build error
                if (IntPtr.Zero == hUpdate)
                {
                    results.AddMessage(BuildMessage.CreateMessage(BuildMessageSeverity.Error, "GenerateBootstrapper.General",
                        $"Unable to begin updating resource for {filename} with error {Marshal.GetHRForLastWin32Error():X}"));
                    return false;
                }

                endUpdate = true;

                if (hUpdate != IntPtr.Zero)
                {
                    foreach (StringResource resource in _stringResources)
                    {
                        byte[] data = StringToByteArray(resource.Data);

                        if (!NativeMethods.UpdateResourceW(hUpdate, (IntPtr)resource.Type, resource.Name, 0, data, data.Length))
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
                        if (!NativeMethods.UpdateResourceW(hUpdate, (IntPtr)42, "COUNT", 0, countArray, countArray.Length))
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

                                fs.Read(fileContent, 0, fileLength);
                            }

                            // Update the resources to include this file's data
                            string dataName = string.Format(CultureInfo.InvariantCulture, "FILEDATA{0}", index);

                            if (!NativeMethods.UpdateResourceW(hUpdate, (IntPtr)42, dataName, 0, fileContent, fileLength))
                            {
                                results.AddMessage(BuildMessage.CreateMessage(BuildMessageSeverity.Error, "GenerateBootstrapper.General",
                                    $"Unable to update data resource for {filename} with error {Marshal.GetHRForLastWin32Error():X}"));
                                return false;
                            }

                            // Add this file's key to the resources
                            string keyName = string.Format(CultureInfo.InvariantCulture, "FILEKEY{0}", index);
                            byte[] data = StringToByteArray(resource.Key);
                            if (!NativeMethods.UpdateResourceW(hUpdate, (IntPtr)42, keyName, 0, data, data.Length))
                            {
                                results.AddMessage(BuildMessage.CreateMessage(BuildMessageSeverity.Error, "GenerateBootstrapper.General",
                                    $"Unable to update key resource for {filename} with error {Marshal.GetHRForLastWin32Error():X}"));
                                return false;
                            }

                            index++;
                        }
                    }
                }
            }
            finally
            {
                if (endUpdate && !NativeMethods.EndUpdateResource(hUpdate, false))
                {
                    results.AddMessage(BuildMessage.CreateMessage(BuildMessageSeverity.Error, "GenerateBootstrapper.General",
                        $"Unable to finish updating resource for {filename} with error {Marshal.GetHRForLastWin32Error():X}"));
                    returnValue = false;
                }
            }

            return returnValue;
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
